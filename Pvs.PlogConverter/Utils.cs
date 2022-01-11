//  2006-2008 (c) Viva64.com Team
//  2008-2020 (c) OOO "Program Verification Systems"
//  2020-2022 (c) PVS-Studio LLC
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using ProgramVerificationSystems.PVSStudio.CommonTypes;
using ProgramVerificationSystems.PVSStudio;
using System.Data;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading;
using Newtonsoft.Json;

namespace ProgramVerificationSystems.PlogConverter
{
    /// <summary>
    ///     Utilities
    /// </summary>
    internal static class Utils
    {
        public const string SourceTreeRootMarker = ApplicationSettings.SourceTreeRootMarker;
        private static readonly Encoding DefaultEncoding = Encoding.UTF8;
        public readonly static string PlogExtension;
        public readonly static string JsonLogExtension;

        static Utils()
        {
            PlogExtension = ReflectionUtils.GetAttributes<DescriptionAttribute, LogRenderType>(LogRenderType.Plog).First().Description;
            JsonLogExtension = ReflectionUtils.GetAttributes<DescriptionAttribute, LogRenderType>(LogRenderType.JSON).First().Description;
        }

        public static string GetDescription<T>(T genObject)
        {
            var memberInfos = typeof(T).GetMember(genObject.ToString());
            if (memberInfos.Length < 1)
                return string.Empty;

            var descriptionAttributes =
                (DescriptionAttribute[])memberInfos[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
            return descriptionAttributes.Length == 1 ? descriptionAttributes[0].Description : string.Empty;
        }

        public static T[] GetEnumValues<T>()
        {
            return typeof(T).IsEnum ? (T[])Enum.GetValues(typeof(T)) : new T[0];
        }

        public static IEnumerable<ErrorInfoAdapter> GetErrors(string plogFilename, out string solutionName)
        {
            string logExtention = Path.GetExtension(plogFilename);

            if (logExtention.Equals(PlogExtension, StringComparison.OrdinalIgnoreCase))
            {
                var xmlText = File.ReadAllText(plogFilename, DefaultEncoding);
                return GetErrorsFromXml(out solutionName, xmlText);
            }
            else if (logExtention.Equals(JsonLogExtension, StringComparison.OrdinalIgnoreCase))
            {
                solutionName = String.Empty;
                var jsonText = File.ReadAllText(plogFilename, DefaultEncoding);
                return GetErrorsFromJson(jsonText);
            }
            else
            {
                return GetErrorsFromUnparsed(plogFilename, out solutionName);
            }
        }

        private static IEnumerable<ErrorInfoAdapter> GetErrorsFromUnparsed(string plogFilename, out string solutionName)
        {
            var plogTable = new DataTable();
            var solutionPathsTable = new DataTable();
            DataTableUtils.CreatePVSDataTable(plogTable, solutionPathsTable, plogTable.DefaultView);
            var plogSet = new DataSet();
            plogSet.Tables.Add(solutionPathsTable);
            plogSet.Tables.Add(plogTable);

            var signaller = new ManualResetEventSlim(false);
            try
            {
                var tasksQueue = new ConcurrentQueue<Task<IEnumerable<ErrorInfo>>>();
                Exception processingException = null;
                var processingThread = new Thread(() =>
                {
                    try
                    {
                        while (true)
                        {
                            Task<IEnumerable<ErrorInfo>> currentTask;
                            while (tasksQueue.TryDequeue(out currentTask))
                            {
                                foreach (var error in currentTask.Result)
                                    DataTableUtils.AppendErrorInfoToDataTable(plogTable, error);
                            }

                            if (signaller.IsSet && tasksQueue.Count == 0)
                                return;

                            Thread.Sleep(1000);
                        }
                    }
                    catch (Exception e)
                    {
                        Volatile.Write<Exception>(ref processingException, e);
                    }
                });

                processingThread.Start();

                const int linesToRead = 10000;
                const int maxTasksCount = 1000000 / linesToRead; //will consume approximately 1GB of memory
                StringBuilder totalLinesCache = new StringBuilder(linesToRead * 350); //we assume that an average PVS-Studio output line is 350 chars long
                IList<String> preprocessedFilesDependencies;
                Func<bool> isProcessingExceptionPresent = () => Volatile.Read<Exception>(ref processingException) != null;

                using (StreamReader logfileStream = new StreamReader(plogFilename))
                {
                    String currentLine;
                    int linesRead = 0;
                    bool isEncoded = false;

                    while (true)
                    {
                        if (isProcessingExceptionPresent())
                            break;

                        currentLine = logfileStream.ReadLine();
                        if (currentLine == null || linesRead >= linesToRead)
                        {
                            linesRead = 0;
                            var linesText = totalLinesCache.ToString();
                            totalLinesCache.Clear();

                            Encoding currentIncodingLocal = logfileStream.CurrentEncoding;
                            bool isIncodedLocal = isEncoded;

                            var errorsTask = new Task<IEnumerable<ErrorInfo>>(() => ErrorInfoExtentions.ProcessAnalyzerOutput(String.Empty,
                                                                                                                              String.Empty,
                                                                                                                              String.Empty,
                                                                                                                              linesText,
                                                                                                                              isIncodedLocal,
                                                                                                                              currentIncodingLocal,
                                                                                                                              null,
                                                                                                                              out preprocessedFilesDependencies,
                                                                                                                              String.Empty));
                            errorsTask.Start();
                            tasksQueue.Enqueue(errorsTask);

                            while (!isProcessingExceptionPresent() && tasksQueue.Count >= maxTasksCount)
                                Thread.Sleep(1000);

                            if (currentLine == null)
                            {
                                signaller.Set();
                                break;
                            }
                        }

                        if (String.IsNullOrWhiteSpace(currentLine) || currentLine.StartsWith("#"))
                            continue;

                        if (currentLine.StartsWith(CppAnalyzerDecoder.EncodeMarker))
                        {
                            isEncoded = true;
                            continue;
                        }

                        totalLinesCache.Append(currentLine + Environment.NewLine);
                        linesRead++;
                    }
                }

                processingThread.Join();

                if (isProcessingExceptionPresent())
                    throw Volatile.Read<Exception>(ref processingException);
            }
            finally
            {
                signaller.Set();
            }

            return GetErrorsFromXml(out solutionName, plogSet.GetXml());
        }

        private static IEnumerable<ErrorInfoAdapter> GetErrorsFromXml(out string solutionName, string xmlText)
        {
            // todo: Add plog version check for a subsequent upgrade
            var plogXmlDocument = new XmlDocument();
            plogXmlDocument.LoadXml(xmlText);
            var solPathNodeList = plogXmlDocument.GetElementsByTagName(DataColumnNames.SolutionPath);
            if (solPathNodeList.Count > 0)
                solutionName = solPathNodeList[0].FirstChild != null ? (solPathNodeList[0].FirstChild.Value ?? string.Empty) : string.Empty;
            else
                solutionName = String.Empty;

            var messagesElements = plogXmlDocument.GetElementsByTagName(DataTableNames.MessageTableName);
            return messagesElements.Cast<object>().Select((o, elIndex) => GetErrorInfo(messagesElements, elIndex)).ToList();
        }

        private static IEnumerable<ErrorInfoAdapter> GetErrorsFromJson(string jsonText)
        {
            JsonPvsReport jsonPvsReport = JsonConvert.DeserializeObject<JsonPvsReport>(jsonText);
            List<ErrorInfoAdapter> errorInfoAdapters = new List<ErrorInfoAdapter>();

            foreach(JsonPvsReport.Warning warning in jsonPvsReport.Warnings)
                errorInfoAdapters.Add(new ErrorInfoAdapter(JsonPvsReport.Warning.WarningToErrorInfo(warning)));

            return errorInfoAdapters;
        }

        public static IEnumerable<ErrorInfoAdapter> FilterErrors(IEnumerable<ErrorInfoAdapter> errors, 
                                        IDictionary<AnalyzerType, ISet<uint>> levelMap,
                                        IEnumerable<string> disabledErrorCodes)
        {
            if (levelMap != null && levelMap.Count > 0)
                errors = errors.Filter(levelMap);

            if (disabledErrorCodes != null && disabledErrorCodes.Count() > 0)
                errors = errors.Filter(disabledErrorCodes);
            
            return errors;
        }

        private static ErrorInfoAdapter GetErrorInfo(XmlNodeList messagesElements, int elIndex)
        {
            var messageNodes = messagesElements[elIndex].ChildNodes;
            var errorInfo = new ErrorInfoAdapter();

            for (var j = 0; j < messageNodes.Count; j++)
            {
                SetErrorValue(messageNodes[j], errorInfo);
            }

            return errorInfo;
        }

        private static void SetErrorValue(XmlNode messageNode, ErrorInfoAdapter errorInfo)
        {
            XmlNodeList childNodes = messageNode.ChildNodes;
            string firstChildContent = childNodes.Count > 0 ? childNodes.Item(0).Value : string.Empty;
            switch (messageNode.Name)
            {
                case DataColumnNames.ErrorListAnalyzer:
                    errorInfo.ErrorInfo.AnalyzerType = (AnalyzerType)Enum.Parse(typeof(AnalyzerType), firstChildContent);
                    break;
                case DataColumnNames.ErrorListCodeCurrent:
                    errorInfo.ErrorInfo.CodeCurrent = Convert.ToUInt32(firstChildContent);
                    break;
                case DataColumnNames.ErrorListCodeNext:
                    errorInfo.ErrorInfo.CodeNext = Convert.ToUInt32(firstChildContent);
                    break;
                case DataColumnNames.ErrorListCodePrev:
                    errorInfo.ErrorInfo.CodePrev = Convert.ToUInt32(firstChildContent);
                    break;
                case DataColumnNames.ErrorListErrorCode:
                    errorInfo.ErrorInfo.ErrorCode = firstChildContent;
                    break;
                case DataColumnNames.ErrorListFalseAlarm:
                    errorInfo.ErrorInfo.FalseAlarmMark = Convert.ToBoolean(firstChildContent);
                    break;
                case DataColumnNames.ErrorListFile:
                    errorInfo.ErrorInfo.FileName = firstChildContent;
                    break;
                case DataColumnNames.ErrorListRetired:
                    errorInfo.ErrorInfo.IsRetired = Convert.ToBoolean(firstChildContent);
                    break;
                case DataColumnNames.ErrorListLevel:
                    errorInfo.ErrorInfo.Level = Convert.ToUInt32(firstChildContent);
                    break;
                case DataColumnNames.ErrorListLine:
                    errorInfo.ErrorInfo.LineNumber = Convert.ToInt32(firstChildContent);
                    break;
                case DataColumnNames.ErrorListPositions:
                    // OuterXml is needed because the xml string for XmlTextReader must have only one root element
                    var xmlReader = new XmlTextReader(new StringReader(messageNode.OuterXml));
                    // Reading the root elemnet 'Positions'
                    xmlReader.Read();
                    ((IXmlSerializable)errorInfo.ErrorInfo.Positions).ReadXml(xmlReader);
                    break;
                case DataColumnNames.ErrorListMessage:
                    errorInfo.ErrorInfo.Message = firstChildContent;
                    break;
                case DataColumnNames.ErrorListTrial:
                    errorInfo.ErrorInfo.TrialMode = Convert.ToBoolean(firstChildContent);
                    break;
                case DataColumnNames.ErrorListCwe:
                    errorInfo.ErrorInfo.CweId = (firstChildContent.StartsWith(ErrorInfo.CWEPrefix) && firstChildContent.Length >= (ErrorInfo.CWEPrefix.Length + 1)) ?
                                                  Convert.ToUInt32(firstChildContent.Substring(ErrorInfo.CWEPrefix.Length))
                                                : default(uint);
                    break;
                case DataColumnNames.ErrorListMisra: // For compatibility with xml version 6 and below
                case DataColumnNames.ErrorListSast:
                    errorInfo.ErrorInfo.SastId = firstChildContent;
                    break;
                // Additional values
                case DataColumnNames.ErrorListFavIcon:
                    errorInfo.FavIcon = Convert.ToBoolean(firstChildContent);
                    break;
                case DataColumnNames.ErrorListOrder:
                    errorInfo.DefaultOrder = Convert.ToInt32(firstChildContent);
                    break;
                case DataColumnNames.ErrorListProject:
                    errorInfo.ErrorInfo.ProjectNames = firstChildContent.Split(DataTableUtils.ProjectNameSeparator).ToList();
                    break;
                case DataColumnNames.ErrorListAnalyzedSourceFiles:
                    errorInfo.ErrorInfo.AnalyzedSourceFiles = firstChildContent.Split(DataTableUtils.AnalyzedSourceFileSeparator).ToList();
                    break;
            }
        }

        public static bool TryParseLevelFilters(IEnumerable<string> analyzerLevels,
            IDictionary<AnalyzerType, ISet<uint>> analyzerLevelFilterMap, out string errorMessage)
        {
            foreach (
                var dtcTypeLevel in
                    analyzerLevels.Select(
                        levelFilter => levelFilter.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries)))
            {
                if (dtcTypeLevel.Length != 2)
                {
                    errorMessage = "Level filter was not specified";
                    return false;
                }

                var analyzerTypeString = dtcTypeLevel[0];
                bool success;
                var analyzerType = analyzerTypeString.ShortNameToType(out success);

                if (success)
                {
                    var levels = dtcTypeLevel[1];
                    if (string.IsNullOrWhiteSpace(levels))
                    {
                        errorMessage = "Levels are not set";
                        return false;
                    }

                    var typeLevels = levels.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (typeLevels.Length == 0)
                    {
                        errorMessage = "No levels were specified";
                        return false;
                    }

                    var parsedLevels = new HashSet<uint>();
                    foreach (var typeLevel in typeLevels)
                    {
                        uint parsedLevel;
                        if (uint.TryParse(typeLevel, out parsedLevel))
                        {
                            parsedLevels.Add(parsedLevel);
                        }
                        else
                        {
                            errorMessage = string.Format("Incorrect level: '{0}'. Level must be an integer value",
                                typeLevel);
                            return false;
                        }
                    }

                    analyzerLevelFilterMap.TryGetValue(analyzerType, out ISet<uint> cachedLevels);
                    parsedLevels.UnionWith(cachedLevels ?? Enumerable.Empty<uint>());

                    analyzerLevelFilterMap[analyzerType] = parsedLevels;
                }
                else
                {
                    errorMessage = AvailableShortAnalyzerNames();
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        private static string AvailableShortAnalyzerNames()
        {
            var shortNamesBuilder = new StringBuilder("Incorrect analyzer type was specified. Possible values are: ");
            shortNamesBuilder.Append(String.Join(", ", GetAllShortNames()));
            return shortNamesBuilder.ToString();
        }

        private static string[] GetAllShortNames()
        {
            return
                Enum.GetValues(typeof(AnalyzerType))
                    .Cast<AnalyzerType>()
                    .Select(availableType => availableType.GetShortName())
                    .ToArray();
        }

        private static string GetShortName(this AnalyzerType analyzerType)
        {
            return
                analyzerType.GetType()
                    .GetField(analyzerType.ToString())
                    .GetCustomAttributes(typeof(XmlEnumAttribute), false)
                    .Cast<XmlEnumAttribute>()
                    .First()
                    .Name;
        }

        private static AnalyzerType ShortNameToType(this string shortName, out bool success)
        {
            if (string.IsNullOrWhiteSpace(shortName))
            {
                success = false;
                return AnalyzerType.Unknown;
            }

            shortName = shortName.ToLower();
            var shortNames = GetAllShortNames().Select(name => name.ToLower()).ToArray();
            var foundIndex = Array.FindIndex(shortNames, currentShortName => currentShortName == shortName);
            if (foundIndex == -1)
            {
                success = false;
                return AnalyzerType.Unknown;
            }

            var analyzerTypes = Enum.GetValues(typeof(AnalyzerType)).Cast<AnalyzerType>().ToArray();
            if (analyzerTypes.Length != shortNames.Length)
                throw new Exception("Not all analyzer types have a short name");

            success = true;
            return analyzerTypes[foundIndex];
        }

        public static bool TryParseEnumValues<T>(IEnumerable<string> unparsedEnumerators, ISet<T> enumerators, out string errorMessage) where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum)
            {
                errorMessage = $"{typeof(T).Name} is not an enumeration.";
                return false;
            }

            foreach (var unparsedEnumerator in unparsedEnumerators)
            {
                T enumerator;
                if (Enum.TryParse(unparsedEnumerator, true, out enumerator))
                {
                    enumerators.Add(enumerator);
                }
                else
                {
                    errorMessage = string.Format("Cannot parse render type with a name {0}", unparsedEnumerator);
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        public static bool IsValidFilename(string testName)
        {
            var containsABadCharacter = new Regex("["
                  + Regex.Escape(new string(Path.GetInvalidPathChars())) + "]");
            return !containsABadCharacter.IsMatch(testName);
        }
    }

    public static class ErrorsFilters
    {
        /// <summary>
        ///     Exclude errors marked as false alarms
        /// </summary>
        /// <param name="errors">Errors</param>
        /// <returns>Errors without false alarms</returns>
        public static List<ErrorInfoAdapter> ExcludeFalseAlarms(this IEnumerable<ErrorInfoAdapter> errors, LogRenderType renderType)
        {
            if (renderType == LogRenderType.MisraCompliance)
                return errors.ToList();

            return errors.Where(error => !error.ErrorInfo.FalseAlarmMark).ToList();
        }

        public static List<ErrorInfoAdapter> FixTrialMessages(this IEnumerable<ErrorInfoAdapter> errors)
        {
            errors.ToList().ForEach(ei =>
            {
                if (ei.ErrorInfo.TrialMode)
                    ei.ErrorInfo.FileName = "TRIAL RESTRICTION";
            });
            
            return errors.ToList();
        }

        /// <summary>
        ///     Filter errors by disabled error codes
        /// </summary>
        /// <param name="errors">Errors</param>
        /// <param name="disabledErrorCodes">Disabled error codes</param>
        /// <returns>Filtered errors</returns>
        public static IEnumerable<ErrorInfoAdapter> Filter(this IEnumerable<ErrorInfoAdapter> errors,
            IEnumerable<string> disabledErrorCodes)
        {
            var errorCodes = disabledErrorCodes as string[] ?? disabledErrorCodes.ToArray();
            return (from error in errors
                    let errorCode = error.ErrorInfo.ErrorCode
                    let found =
                        errorCodes.Any(
                            disabledErrorCode =>
                                string.Equals(errorCode, disabledErrorCode, StringComparison.CurrentCultureIgnoreCase))
                    where !found
                    select error).ToList();
        }

        /// <summary>
        ///     Filter errors by analyzer types and levels
        /// </summary>
        /// <param name="errors">Errors</param>
        /// <param name="analyzerLevelMap">Filter map</param>
        /// <returns>Filtered errors</returns>
        public static IEnumerable<ErrorInfoAdapter> Filter(this IEnumerable<ErrorInfoAdapter> errors,
            IDictionary<AnalyzerType, ISet<uint>> analyzerLevelMap)
        {
            var transformedErrors = errors.GroupByAnalyzerType()
                    .FilterByAnalyzerTypes(analyzerLevelMap.Keys.ToArray())
                    .FilterByLevels(analyzerLevelMap)
                    .Transform();

            var renewMessage = errors.FirstOrDefault(e => e.ErrorInfo.AnalyzerType == AnalyzerType.Unknown && e.ErrorInfo.ErrorCode.Equals(ErrorInfo.RenewLicenseMessageCode));
            if (renewMessage != null)
                transformedErrors.Add(renewMessage);

            return transformedErrors;
        }

        private static List<ErrorInfoAdapter> Transform(
            this IDictionary<AnalyzerType, IList<ErrorInfoAdapter>> groupedErrors)
        {
            var filtered = new List<ErrorInfoAdapter>();
            foreach (var groupedError in groupedErrors)
            {
                filtered.AddRange(groupedError.Value);
            }

            return filtered;
        }

        private static IDictionary<AnalyzerType, IList<ErrorInfoAdapter>> FilterByLevels(
            this IDictionary<AnalyzerType, IList<ErrorInfoAdapter>> groupedErrors,
            IDictionary<AnalyzerType, ISet<uint>> analyzerLevelMap)
        {
            var filteredByLevels = new Dictionary<AnalyzerType, IList<ErrorInfoAdapter>>();

            foreach (var analyzerType in groupedErrors.Keys.ToArray())
            {
                var acceptedLevels = analyzerLevelMap[analyzerType];
                var analyzerErrors = groupedErrors[analyzerType];
                var filteredErrors =
                    analyzerErrors.Where(analyzerError => acceptedLevels.Contains(analyzerError.ErrorInfo.Level))
                        .ToList();
                if (filteredErrors.Count > 0)
                {
                    filteredByLevels.Add(analyzerType, filteredErrors);
                }
            }

            return filteredByLevels;
        }

        private static Dictionary<AnalyzerType, IList<ErrorInfoAdapter>> GroupByAnalyzerType(
            this IEnumerable<ErrorInfoAdapter> errors)
        {
            var groupedErrors = new Dictionary<AnalyzerType, IList<ErrorInfoAdapter>>();

            foreach (var error in errors)
            {
                var analyzerType = error.ErrorInfo.AnalyzerType;
                if (!groupedErrors.ContainsKey(analyzerType))
                {
                    groupedErrors.Add(analyzerType, new List<ErrorInfoAdapter>());
                }

                groupedErrors[analyzerType].Add(error);
            }

            return groupedErrors;
        }

        private static Dictionary<AnalyzerType, T> FilterByAnalyzerTypes<T>(
            this Dictionary<AnalyzerType, T> groupedErrors,
            AnalyzerType[] acceptedTypes)
        {
            var analyzerTypes = groupedErrors.Keys.ToArray();
            ISet<AnalyzerType> unnecessaryAnalyzerTypes = new HashSet<AnalyzerType>();
            foreach (var analyzerType in from analyzerType in analyzerTypes
                                         let found = acceptedTypes.Any(acceptedType => analyzerType == acceptedType)
                                         where !found
                                         select analyzerType)
            {
                unnecessaryAnalyzerTypes.Add(analyzerType);
            }

            foreach (var unnecessaryAnalyzerType in unnecessaryAnalyzerTypes)
            {
                groupedErrors.Remove(unnecessaryAnalyzerType);
            }

            return groupedErrors;
        }
    }
}