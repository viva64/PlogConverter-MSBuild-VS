//  2006-2008 (c) Viva64.com Team
//  2008-2020 (c) OOO "Program Verification Systems"
//  2020-2022 (c) PVS-Studio LLC
using ProgramVerificationSystems.PVSStudio.CommonTypes;
using System;
using System.Collections.Generic;
using System.IO;

namespace ProgramVerificationSystems.PlogConverter
{
    public enum ErrorCodeMapping
    {
        CWE,
        MISRA,
        OWASP,
        AUTOSAR
    }

    /// <summary>
    ///     Renderer interface
    /// </summary>
    public interface IPlogRenderer
    {
        String LogExtension { get; }

        /// <summary>
        ///     Render information
        /// </summary>
        RenderInfo RenderInfo { get; }

        /// <summary>
        ///     Errors to render
        /// </summary>
        IEnumerable<ErrorInfoAdapter> Errors { get; }

        /// <summary>
        ///     Error code mappings to display
        /// </summary>
        IEnumerable<ErrorCodeMapping> ErrorCodeMappings { get; }

        /// <summary>
        ///     Renders plog-file
        /// </summary>
        void Render(Stream writer = null);

        /// <summary>
        ///     Callback handler on rendering completed
        /// </summary>
        event EventHandler<RenderCompleteEventArgs> RenderComplete;
    }

    public class RenderCompleteEventArgs : EventArgs
    {
        public string OutputFile { get; private set; }

        public RenderCompleteEventArgs(string outputFile)
        {
            OutputFile = outputFile;
        }
    }
}