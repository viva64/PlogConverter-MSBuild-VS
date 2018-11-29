//  2006-2008 (c) Viva64.com Team
//  2008-2018 (c) OOO "Program Verification Systems"
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;
using System;

namespace ProgramVerificationSystems.PlogConverter
{
    /// <summary>
    ///     Type for command line options
    /// </summary>
    public class CmdConverterOptions
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

        /// <summary>
        ///     Filter by analyzer type with a list of levels
        /// </summary>
        /// <example>--analyzer=GA:1,2;64:1,2,3</example>
        [OptionList('a', "analyzer", Separator = ';', Required = false,
            HelpText = "Specifies analyzer(s) and level(s) to be used for filtering, i.e. GA:1,2;64:1;OP:1,2,3;CS:1;MISRA:1,2")]
        public IList<string> AnalyzerLevelFilter { get; set; }

        /// <summary>
        ///     Render types
        /// </summary>
        /// <example>--renderTypes=Html,Totals,Txt,Csv,Plog</example>
        [OptionList('t', "renderTypes", Separator = ',', Required = false,
            HelpText = "Render types for output. Supported renderers: Html,FullHtml,Totals,Txt,Csv,Tasks,Plog")]
        public IList<string> PlogRenderTypes { get; set; }

        /// <summary>
        ///     Error codes to disable
        /// </summary>
        /// <example>--excludedCodes=V101,V102,...V200</example>
        [OptionList('d', "excludedCodes", Separator = ',', Required = false, HelpText = "Error codes to disable, i.e. V101,V102,V103")]
        public IList<string> DisabledErrorCodes { get; set; }

        [Option('s', "settings", Required = false, DefaultValue = "", HelpText = "Path to PVS-Studio settings file. Can be used to specify additional disabled error codes." 
            + " Note that other setting values (excluded directories, for example) are ignored.")]
        public String SettingsPath { get; set; }

        [Option('n', "outputNameTemplate", Required = false, DefaultValue = "", HelpText = "Template name for resulting output files.")]
        public String OutputNameTemplate { get; set; }

        [OptionList('m', "errorCodeMapping", Separator = ',', Required = false,
            HelpText = "Enable mapping of PVS-Studio error codes to other rule sets. Possible values: CWE,MISRA")]
        public IList<String> ErrorCodeMapping { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            var helper = HelpText.AutoBuild(this);
            helper.AddPreOptionsLine(string.Format("{0}PlogConverter.exe [options] [log path(s)]", Environment.NewLine));
            return helper;
        }
    }
}