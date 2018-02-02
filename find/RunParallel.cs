using System;
using System.Collections.Generic;
using System.Threading;

using Spi.IO;

namespace find
{
    class RunParallel
    {
        public static Stats Run(IEnumerable<string> dirs, int maxDepth, bool followJunctions, Predicate<string> matchFilename, Action<DirEntry> MatchedFileHandler, Action<int, string> ErrorHandler, Action<string> ProgressHandler, ManualResetEvent CrtlCEvent)
        {
            EnumDirsParallel parallelEnumerator 
                = EnumDirsParallel.Start(dirs, maxDepth, followJunctions, ReportToQueue: MatchedFileHandler != null, matchFilename, ErrorHandler, CrtlCEvent);

            if (MatchedFileHandler == null)
            {
                while ( ! parallelEnumerator.IsFinished(1000) )
                {
                    PrintProgress(parallelEnumerator, ProgressHandler);
                }
            }
            else
            {
                while (true)
                {
                    parallelEnumerator.TryDequeue(out DirEntry? entry, out bool hasFinished, millisecondsTimeout: 1000);
                    if (entry.HasValue)
                    {
                        MatchedFileHandler?.Invoke(entry.Value);
                    }
                    else if (!hasFinished)
                    {
                        PrintProgress(parallelEnumerator, ProgressHandler);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            return parallelEnumerator.EnumStats;
        }
        private static void PrintProgress(EnumDirsParallel EnumPar, Action<string> ProgressHandler)
        {
            if ( ProgressHandler == null)
            {
                return;
            }
            EnumPar.GetProgress(out ulong submitted, out ulong running, out ulong FoundEntriesQueueCount, out Stats tmpStats);
            ProgressHandler(
                  $"Enumerations submitted/running: {submitted}/{running}"
                + $" | Files seen: {tmpStats.AllFiles} ({       Spi.IO.Misc.GetPrettyFilesize((ulong)tmpStats.AllBytes)})"
                + $" | Files matched: {tmpStats.MatchedFiles} ({Spi.IO.Misc.GetPrettyFilesize((ulong)tmpStats.MatchedBytes)})");
        }
    }
}
