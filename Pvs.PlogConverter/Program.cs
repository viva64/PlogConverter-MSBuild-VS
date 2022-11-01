//  2006-2008 (c) Viva64.com Team
//  2008-2020 (c) OOO "Program Verification Systems"
//  2020-2022 (c) PVS-Studio LLC
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ProgramVerificationSystems.PVSStudio.CommonTypes;
using CmdParser = CommandLine.Parser;
using ProgramVerificationSystems.PVSStudio;
using System.Collections.Concurrent;

namespace ProgramVerificationSystems.PlogConverter
{
    internal static class Program
    {
        public static readonly TextWriter DefaultWriter = Console.Out;
        public static readonly TextWriter ErrortWriter = Console.Error;
        private static readonly string DefaultOutputFolder = Environment.CurrentDirectory;
        private static readonly string NewLine = Environment.NewLine;

        private static ILogger _logger = new DefaultLogger();
        public static ILogger Logger { get { return _logger; } private set { _logger = value; } }

        private static int Main(string[] args)
        {
            try
            {
                // Accepting command-line arguments
                var parsedArgs = new ParsedArguments { RenderInfo = new RenderInfo() };
                var success = AcceptArguments(args, ref parsedArgs, out string errorMessage, out IList<string> warningMessages);
                if (!success)
                {
                    ErrortWriter.WriteLine(errorMessage);
                    return (int)ConverterRunState.IncorrectArguments;
                }
                else if (!string.IsNullOrWhiteSpace(errorMessage))
                {
                    DefaultWriter.WriteLine(errorMessage);
                    return (int)ConverterRunState.Success;
                }

                if (warningMessages.Any())
                    warningMessages.ToList().ForEach(msg => DefaultWriter.WriteLine(msg));

                var renderFactory = new PlogRenderFactory(parsedArgs, Logger);

                var acceptedRenderTypes = parsedArgs.RenderTypes != null && parsedArgs.RenderTypes.Count > 0
                    ? parsedArgs.RenderTypes.ToArray()
                    : Utils.GetEnumValues<LogRenderType>();

                ConcurrentBag<int> filteredErrorsByRenderTypes = new ConcurrentBag<int>();

                var renderTasks = new Task[acceptedRenderTypes.Length];
                for (var index = 0; index < renderTasks.Length; index++)
                {
                    var closureIndex = index;
                    renderTasks[index] =
                        Task.Factory.StartNew(() =>
                        {
                             var renderType = acceptedRenderTypes[closureIndex];
                             var plogRenderer = renderFactory.GetRenderService(renderType,
                             (rt, path) =>
                                DefaultWriter.WriteLine("{0} output was saved to {1}",
                                                        Enum.GetName(typeof(LogRenderType), rt),
                                                        path));
                             
                             if (  !plogRenderer.IsSupportRenderType() 
                                 && parsedArgs.RenderInfo.TransformationMode == TransformationMode.toRelative)
                             {
                                DefaultWriter.WriteLine("Error: the \'{0}\' format doesn't support relative root",
                                                        Enum.GetName(typeof(LogRenderType), renderType));
                                renderFactory.Logger.ErrorCode = (int)ConverterRunState.UnsupportedPathTransofrmation;
                                return;
                             }

                             if (!ExcludeUtils.IsExcludePathsSupported)
                             {
                                DefaultWriter.WriteLine("Warning: Filtering by file is only available for paths without the SourceTreeRoot marker.\n" +
                                                        "To filter by file for the report, specify the root directory via the '-r' flag. \n");
                             }

                             filteredErrorsByRenderTypes.Add(plogRenderer.Errors.Count());
                             plogRenderer.Render();
                        });
                }

                Task.WaitAll(renderTasks);

                var rc = renderFactory.Logger.ErrorCode;
                if (rc == (int)ConverterRunState.UnsupportedPathTransofrmation)
                {
                    return rc;
                }

                if (rc != 0)
                {
                    return (int)ConverterRunState.RenderException;
                }

                if (   parsedArgs.IndicateWarnings
                    && filteredErrorsByRenderTypes.Any(errorsByRenderType => errorsByRenderType != 0))
                {
                    return (int)ConverterRunState.OutputLogNotEmpty;
                }

                return (int)ConverterRunState.Success;
            }
            catch (AggregateException aggrEx)
            {
                var baseEx = aggrEx.GetBaseException();
                Logger.LogError(baseEx.ToString());
                return (int)ConverterRunState.GeneralException;
            }
            catch (Exception ex)
            {
                if (ex is IOException || 
                    ex is UnauthorizedAccessException ||
                    ex is SettingsLoadException)
                    Logger.LogError(ex.Message);
                else
                    Logger.LogError(ex.ToString());

                return (int)ConverterRunState.GeneralException;
            }
        }
        
        private static bool IsHelpArgumentOnly(string[] args)
        {
            return args != null && args.Length == 1 && args[0] == "--help";
        }

        private static bool AcceptArguments(string[] args, ref ParsedArguments parsedArgs, out string errorMessage, out IList<string> warningMessages)
        {
            errorMessage = string.Empty;
            warningMessages = new List<string>();

            var converterOptions = new CmdConverterOptions();
            using (var parser = new CmdParser(parsingSettings => { parsingSettings.IgnoreUnknownArguments = false; }))
            {
                if (!parser.ParseArgumentsStrict(args, converterOptions, () => 
                                                 {
                                                    if (!IsHelpArgumentOnly(args))
                                                        Logger.LogError(NewLine + "Incorrect command line arguments." + NewLine);
                                                 }))
                {
                    errorMessage = converterOptions.GetUsage();
                    return IsHelpArgumentOnly(args);
                }
            }
                            
            if (converterOptions.PlogPaths.Count == 0)
            {
                errorMessage = string.Format("No input target was specified.{0}{1}{2}", NewLine, NewLine, converterOptions.GetUsage());
                return false;
            }
            
            foreach (var plogPath in converterOptions.PlogPaths)
            {
                if (!File.Exists(plogPath))
                {
                    errorMessage = string.Format("File '{0}' does not exist.{1}", plogPath, NewLine);
                    return false;
                }

                parsedArgs.RenderInfo.Logs.Add(plogPath);
            }

            parsedArgs.RenderInfo.OutputDir = converterOptions.OutputPath ?? DefaultOutputFolder;
            parsedArgs.RenderInfo.GRP = converterOptions.GRP;
            parsedArgs.RenderInfo.MisraDeviations = converterOptions.MisraDeviations;
            parsedArgs.RenderInfo.NoHelp = converterOptions.NoHelp;

            if (!Directory.Exists(parsedArgs.RenderInfo.OutputDir))
            {
                errorMessage = string.Format("Output directory '{0}' does not exist.{1}", converterOptions.OutputPath, NewLine);
                return false;
            }

            // Output directory represents a root drive
            string outputDirRoot = Path.GetPathRoot(parsedArgs.RenderInfo.OutputDir);
            if (outputDirRoot.Equals(parsedArgs.RenderInfo.OutputDir, StringComparison.InvariantCultureIgnoreCase) &&
                !outputDirRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                parsedArgs.RenderInfo.OutputDir += Path.DirectorySeparatorChar;
            }

            parsedArgs.RenderInfo.SrcRoot = converterOptions.SrcRoot;

            if (ArgmumetsContainsTransformMode(args) && !ArgmumetsContainsSrcRoot(args))
            {
                errorMessage = "You should setup --srcRoot option";
                return false;
            }

            parsedArgs.RenderInfo.TransformationMode = converterOptions.TransformationMode;

            // Getting a map for levels by the analyzer type
            IDictionary<AnalyzerType, ISet<uint>> analyzerLevelFilterMap = new Dictionary<AnalyzerType, ISet<uint>>();
            if (converterOptions.AnalyzerLevelFilter != null && converterOptions.AnalyzerLevelFilter.Count > 0 &&
                !Utils.TryParseLevelFilters(converterOptions.AnalyzerLevelFilter, analyzerLevelFilterMap,
                    out errorMessage))
            {
                return false;
            }

            parsedArgs.LevelMap = analyzerLevelFilterMap;

            parsedArgs.ExcludePaths = new List<string>();
            if (converterOptions.excludePaths != null && converterOptions.excludePaths.Count != 0)
            {
                foreach (string excludePath in converterOptions.excludePaths)
                {
                    string path = excludePath.Trim();

                    if (!Utils.IsMask(path))
                    {
                        path = Path.GetFullPath(path);
                    }                   
                    parsedArgs.ExcludePaths.Add(path);
                }
            }

            // Getting render types
            ISet<LogRenderType> renderTypes = new HashSet<LogRenderType>();
            if (   converterOptions.PlogRenderTypes != null
                && converterOptions.PlogRenderTypes.Count > 0
                && !Utils.TryParseEnumValues<LogRenderType>(converterOptions.PlogRenderTypes, renderTypes,
                    out errorMessage))
            {
                return false;
            }

            // Gettings error code mappings
            ISet<ErrorCodeMapping> errorCodeMappings = new HashSet<ErrorCodeMapping>();
            if (   converterOptions.ErrorCodeMapping != null
                && converterOptions.ErrorCodeMapping.Count > 0
                && !Utils.TryParseEnumValues<ErrorCodeMapping>(converterOptions.ErrorCodeMapping, errorCodeMappings,
                    out errorMessage))
            {
                return false;
            }

            if (parsedArgs.LevelMap.Any())
            {
                if (errorCodeMappings.Contains(ErrorCodeMapping.MISRA) && !parsedArgs.LevelMap.Any(item => item.Key == AnalyzerType.MISRA))
                    warningMessages.Add(string.Format("MISRA mapping is specified, but MISRA rules group is not enabled. Check the '-{0}' flag.", CmdConverterOptions.AnalyzerLevelFilter_ShortName));

                if (errorCodeMappings.Contains(ErrorCodeMapping.OWASP) && !parsedArgs.LevelMap.Any(item => item.Key == AnalyzerType.OWASP))
                    warningMessages.Add(string.Format("OWASP mapping is specified, but OWASP rules group is not enabled. Check the '-{0}' flag.", CmdConverterOptions.AnalyzerLevelFilter_ShortName));

                if (errorCodeMappings.Contains(ErrorCodeMapping.AUTOSAR) && !parsedArgs.LevelMap.Any(item => item.Key == AnalyzerType.AUTOSAR))
                    warningMessages.Add(string.Format("AUTOSAR mapping is specified, but AUTOSAR rules group is not enabled. Check the '-{0}' flag.", CmdConverterOptions.AnalyzerLevelFilter_ShortName));
            }

            // Check if provided outputNameTemplate is a valid file name
            if (!string.IsNullOrWhiteSpace(converterOptions.OutputNameTemplate) && !Utils.IsValidFilename(converterOptions.OutputNameTemplate))
            {
                errorMessage = String.Format("Template \"{0}\" is not a valid file name.{1}", converterOptions.OutputNameTemplate, NewLine);
                return false;
            }

            // Check if settings path is exists
            if (!String.IsNullOrWhiteSpace(converterOptions.SettingsPath) && !File.Exists(converterOptions.SettingsPath))
            {
                errorMessage = string.Format("Settings file '{0}' does not exist.", converterOptions.SettingsPath);
                return false;
            }

            if (renderTypes.Any(e => e != LogRenderType.MisraCompliance))
            {
                if (!String.IsNullOrEmpty(parsedArgs.RenderInfo.GRP))
                    Logger.Log("The use of the 'grp' flag is valid only for the 'MisraCompliance' format. Otherwise, it will be ignored.");

                if (!String.IsNullOrEmpty(parsedArgs.RenderInfo.MisraDeviations))
                    Logger.Log("The use of the 'misraDeviations' flag is valid only for the 'MisraCompliance' format. Otherwise, it will be ignored.");
            }


            if (renderTypes.Count == 0)
                parsedArgs.RenderInfo.AllLogRenderType = true;

            parsedArgs.RenderTypes = renderTypes;
            parsedArgs.ErrorCodeMappings = errorCodeMappings;
            parsedArgs.DisabledErrorCodes = converterOptions.DisabledErrorCodes;
            parsedArgs.SettingsPath = converterOptions.SettingsPath;
            parsedArgs.OutputNameTemplate = converterOptions.OutputNameTemplate;
            parsedArgs.IndicateWarnings = converterOptions.IndicateWarnings || converterOptions.IndicateWarningsDeprecated;

            errorMessage = string.Empty;
            return true;
        }
        private static bool ArgmumetsContainsSrcRoot(string[] args)
        {
            return ArgumentsContainsOption(args, nameof(CmdConverterOptions.SrcRoot));
        }
        private static bool ArgmumetsContainsTransformMode(string[] args)
        {
            return ArgumentsContainsOption(args, nameof(CmdConverterOptions.TransformationMode));
        }
        private static bool ArgumentsContainsOption(string[] args, string option)
        {
            string longName = ((CommandLine.BaseOptionAttribute)typeof(CmdConverterOptions).GetProperty(option).GetCustomAttributes(typeof(CommandLine.OptionAttribute), true)[0]).LongName;
            char? shortName = ((CommandLine.BaseOptionAttribute)typeof(CmdConverterOptions).GetProperty(option).GetCustomAttributes(typeof(CommandLine.OptionAttribute), true)[0]).ShortName;

            string optionLongName = "--" + longName;
            string optionShortName = shortName != null ? "-" + shortName : string.Empty;

            return args.Contains(optionLongName) || args.Contains(optionShortName);
        }
    }
}