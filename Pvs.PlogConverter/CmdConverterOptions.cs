//  2006-2008 (c) Viva64.com Team
//  2008-2020 (c) OOO "Program Verification Systems"
//  2020 (c) PVS-Studio LLC
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;
using System;
using ProgramVerificationSystems.PVSStudio.CommonTypes;
using ProgramVerificationSystems.PVSStudio;
using System.ComponentModel;
using System.Text;

namespace ProgramVerificationSystems.PlogConverter
{
    public enum ConverterRunState : int
    {
        [Description("Conversion finished successfully;")]
        Success = 0,
        [Description("Errors were encountered during generation of one of the output files;")]
        RenderException = 1,
        [Description("Output contains non-suppressed warnings after filtration. This exit code will be generated only when using converter with --indicate-warnings (-w) flag;")]
        OutputLogNotEmpty = 2,
        [Description("General (nonspecific) error in the converter's operation, a possible handled exception;")]
        GeneralException = 3,
        [Description("Some of the command line arguments passed to the tool were incorrect;")]
        IncorrectArguments = 4,
    }

    /// <summary>
    ///     Type for command line options
    /// </summary>
    public class CmdConverterOptions : CommandLineArguments<ConverterRunState>
    {
        /// <summary>
        ///     Absolute path(s) to plog-file(s)
        /// </summary>
        /// <example>PlogConverter.exe c:\example\your_plog1.plog c:\example\your_plog2.plog</example>
        [ValueList(typeof(List<string>))]
        public IList<string> PlogPaths { get; set; }

        /// <summary>
        ///     Destination directory for output files
        /// </summary>
        /// <example>--outputDir=c:\dest</example>
        [Option('o', "outputDir", Required = false, HelpText = "Output directory for the generated files.")]
        public string OutputPath { get; set; }

        /// <summary>
        ///     Root path for source files
        /// </summary>
        /// <example>--srcRoot=c:\projects\solutionfolder</example>
        [Option('r', "srcRoot", Required = false, DefaultValue = "", HelpText = "Root path for your source files. " 
            + "Transforms relative paths from input logs (starting with |?| marker) to absolute paths in output logs.")]
        public string SrcRoot { get; set; }

        [Option('R', "pathTransformationMode", Required = false, DefaultValue = TransformationMode.toAbsolute, HelpText = "Trasformation mode: toAbsolute - transfort to absolute path, " +
            "toRelative - transform to relative with source root (--srcRoot option).")]
        public TransformationMode TransformationMode { get; set; }

        public const char AnalyzerLevelFilter_ShortName = 'a';
        public const string AnalyzerLevelFilter_LongName = "analyzer";

        /// <summary>
        ///     Filter by analyzer type with a list of levels
        /// </summary>
        /// <example>--analyzer=GA:1,2;64:1,2,3</example>
        [OptionList(AnalyzerLevelFilter_ShortName, AnalyzerLevelFilter_LongName, Separator = ';', Required = false,
            HelpText = "Specifies analyzer(s) and level(s) to be used for filtering, i.e. GA:1,2;64:1;OP:1,2,3;CS:1;MISRA:1,2")]
        public IList<string> AnalyzerLevelFilter { get; set; }

        /// <summary>
        ///     Render types
        /// </summary>
        /// <example>--renderTypes=Html,Totals,Txt,Csv,Plog</example>
        [OptionList('t', "renderTypes", Separator = ',', Required = false,
            HelpText = "Render types for output. Supported renderers: Html,FullHtml,Totals,Txt,Csv,Tasks,Plog,TeamCity,Sarif,JSON")]
        public IList<string> PlogRenderTypes { get; set; }

        /// <summary>
        ///     Error codes to disable
        /// </summary>
        /// <example>--excludedCodes=V101,V102,...V200</example>
        [OptionList('d', "excludedCodes", Separator = ',', Required = false, HelpText = "Error codes to disable, i.e. V101,V102,V103")]
        public IList<string> DisabledErrorCodes { get; set; }

        /// <summary>
        ///     Path to PVS-Studio settings file
        /// </summary>
        /// <example>--settings=c:\Users\Settings.xml</example>
        [Option('s', "settings", Required = false, DefaultValue = "", HelpText = "Path to PVS-Studio settings file. Can be used to specify additional disabled error codes." 
            + " Note that other setting values (excluded directories, for example) are ignored.")]
        public String SettingsPath { get; set; }

        /// <summary>
        ///     Template name for resulting output files
        /// </summary>
        /// <example>--outputNameTemplate=your_output_log_name</example>
        [Option('n', "outputNameTemplate", Required = false, DefaultValue = "", HelpText = "Template name for resulting output files.")]
        public String OutputNameTemplate { get; set; }

        /// <summary>
        ///     Enable mapping of PVS-Studio error codes to other rule sets
        /// </summary>
        /// <example>--errorCodeMapping=CWE</example>
        [OptionList('m', "errorCodeMapping", Separator = ',', Required = false,
            HelpText = "Enable mapping of PVS-Studio error codes to other rule sets. Possible values: CWE,MISRA")]
        public IList<String> ErrorCodeMapping { get; set; }

        /// <summary>
        ///     Set this option to detect the presense of warnings in output log file
        /// </summary>
        /// <example>--outputNameTemplate=your_output_log_name</example>
        [Option('w', "indicate-warnings", Required = false,
            HelpText = "Set this option to detect the presense of analyzer warnings after filtering analysis log by setting the converter exit code to '2'.")]
        public bool IndicateWarnings { get; set; }

        protected override String GetPreOptionsLine()
        {
            return string.Format("{0}PlogConverter.exe [options] [log path(s)]", Environment.NewLine);
        }

        protected override String GetUtilityReturnValuesDescription() 
        {
            return "The PlogConverter utility defines several non-zero exit codes, which do not necessarily indicate"
                 + " some issue with the operation of the tool itself. Possible PlogConverter exit codes are:";
        }
    }
}