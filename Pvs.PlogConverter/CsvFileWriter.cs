//  2006-2008 (c) Viva64.com Team
//  2008-2020 (c) OOO "Program Verification Systems"
//  2020 (c) PVS-Studio LLC
using System.IO;
using System.Text;

namespace ProgramVerificationSystems.PlogConverter
{
    public sealed class CsvFileWriter : StreamWriter
    {
        private const char DefaultCsvSeparatorChar = ';';

        public CsvFileWriter(Stream stream)
            : base(stream)
        {
        }

        public CsvFileWriter(string filename)
            : base(filename)
        {
        }

        public void WriteRow(CsvRow row)
        {
            var builder = new StringBuilder();
            var firstColumn = true;
            foreach (var value in row)
            {
                if (!firstColumn)
                    builder.Append(DefaultCsvSeparatorChar);

                if (value.IndexOfAny(new[] { '"', DefaultCsvSeparatorChar }) != -1)
                    builder.AppendFormat("\"{0}\"", value.Replace("\"", "\"\""));
                else
                    builder.Append(value);

                firstColumn = false;
            }

            row.LineText = builder.ToString();
            WriteLine(row.LineText);
        }
    }
}