//  2006-2008 (c) Viva64.com Team
//  2008-2020 (c) OOO "Program Verification Systems"
//  2020 (c) PVS-Studio LLC
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
                             var plogRenderer = renderFactory.GetRenderService(acceptedRenderTypes[closureIndex],
                             (renderType, path) =>
                                DefaultWriter.WriteLine("{0} output was saved to {1}",
                                                        Enum.GetName(typeof(LogRenderType), renderType),
                                                        path));

                             filteredErrorsByRenderTypes.Add(plogRenderer.Errors.Count());
                             plogRenderer.Render();
                        });
                }

                Task.WaitAll(renderTasks);

                if (renderFactory.Logger != null && renderFactory.Logger.ErrorCode != 0)
                {
                    return (int)ConverterRunState.RenderException;
                }
                
                return (int)((   parsedArgs.IndicateWarnings
                              && filteredErrorsByRenderTypes.Any(errorsByRenderType => errorsByRenderType != 0)) ? ConverterRunState.OutputLogNotEmpty 
                                                                                                                 : ConverterRunState.Success);
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

            if (string.IsNullOrWhiteSpace(converterOptions.SrcRoot) && converterOptions.TransformationMode == TransformationMode.toRelative)
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

            parsedArgs.RenderTypes = renderTypes;
            parsedArgs.ErrorCodeMappings = errorCodeMappings;
            parsedArgs.DisabledErrorCodes = converterOptions.DisabledErrorCodes;
            parsedArgs.SettingsPath = converterOptions.SettingsPath;
            parsedArgs.OutputNameTemplate = converterOptions.OutputNameTemplate;
            parsedArgs.IndicateWarnings = converterOptions.IndicateWarnings;

            errorMessage = string.Empty;
            return true;
        }
    }
}