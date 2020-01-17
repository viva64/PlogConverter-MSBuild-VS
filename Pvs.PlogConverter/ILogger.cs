//  2006-2008 (c) Viva64.com Team
//  2008-2020 (c) OOO "Program Verification Systems"
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ProgramVerificationSystems.PlogConverter
{
    public interface ILogger
    {
        TextWriter Writer { get; }
        TextWriter ErrorWriter { get; }
        int ErrorCode { get; set; }

        void Log(string message);
        void LogError(string message);
    }
}
