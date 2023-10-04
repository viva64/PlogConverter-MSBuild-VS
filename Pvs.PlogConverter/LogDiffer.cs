using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace ProgramVerificationSystems.PlogConverter
{
    public static class LogDiffer
    {
        private const string MissingMark = " - MISSING IN CURRENT";
        private const string AdditionsMark = " - ADDITIONAL IN CURRENT";

        static IEnumerable<ErrorInfoAdapter> lhLogErrorsSet = new HashSet<ErrorInfoAdapter>();
        static IEnumerable<ErrorInfoAdapter> rhLogErrorsSet = new HashSet<ErrorInfoAdapter>();

        static List<string> logPaths = new List<string>();
        static ParsedArguments parsedArg;

        private static void  FillErrorSets()
        {
            foreach (var logFile in parsedArg.RenderInfo.Logs)
            {
                logPaths.Add(logFile);
                AppendToErrorSet(lhLogErrorsSet as HashSet<ErrorInfoAdapter>,logFile);
            }

            AppendToErrorSet(rhLogErrorsSet as HashSet<ErrorInfoAdapter>, parsedArg.LogDifferences);
        }

        private static void AppendToErrorSet(HashSet<ErrorInfoAdapter> errorSet, string logFile)
        {
           errorSet.UnionWith(Utils.GetErrors(logFile, out _));
        }

        public static IEnumerable<ErrorInfoAdapter> GenerateDiffLog(this IEnumerable<ErrorInfoAdapter> errors, ParsedArguments parsedArguments) 
        {
            parsedArg = parsedArguments;
            if (parsedArg.LogDifferences != null) 
            {
                FillErrorSets();
                return GetLogDifference(lhLogErrorsSet, rhLogErrorsSet);
            }
            return errors;
        }

        private static IEnumerable<ErrorInfoAdapter> GetLogDifference(IEnumerable<ErrorInfoAdapter> lErrorSet, IEnumerable<ErrorInfoAdapter> rErrorSet)
        {
            var missings = lErrorSet.Except(rErrorSet);
            var additionals = rErrorSet.Except(lErrorSet);

            Parallel.Invoke(() => AppendMark(missings, MissingMark),
                            () => AppendMark(additionals, AdditionsMark));

            return missings.Union(additionals);
        }

        private static void AppendMark(IEnumerable<ErrorInfoAdapter> errorSet, string mark)
        {
            foreach (var error in errorSet)
            {
                error.ErrorInfo.Message = error.ErrorInfo.Message + mark;
            }
        }
    }
}

