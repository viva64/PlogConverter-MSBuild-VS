//  2006-2008 (c) Viva64.com Team
//  2008-2020 (c) OOO "Program Verification Systems"
//  2020-2022 (c) PVS-Studio LLC
using System.Collections.Generic;

namespace ProgramVerificationSystems.PlogConverter
{
    public sealed class CsvRow : List<string>
    {
        public string LineText { get; set; }
    }
}