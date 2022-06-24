//  2006-2008 (c) Viva64.com Team
//  2008-2020 (c) OOO "Program Verification Systems"
//  2020-2022 (c) PVS-Studio LLC
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ProgramVerificationSystems.PlogConverter
{
    class DefaultLogger : ILogger
    {
        private int _errorCode = 0;
        public int ErrorCode { get { return _errorCode; } set { _errorCode = value; } }

        private TextWriter _writer = Console.Out, _errorWriter = Console.Error;
        public TextWriter Writer { get { return _writer; } private set { _writer = value; } }
        public TextWriter ErrorWriter { get { return _errorWriter; } private set { _errorWriter = value; } }

        public void Log(string message)
        {
            lock (_writer)
                _writer.WriteLine(message);
        }

        public void LogError(string message)
        {
            lock (_errorWriter)
                _errorWriter.WriteLine(message);
        }
    }
}
