using System;
using System.Collections.Generic;
using System.Threading;

using Spi.IO;

namespace find
{
    class RunParallel
    {
        public static Stats Run(IEnumerable<string> dirs, int maxDepth, bool followJunctions, Predicate<string> matchFilename, Action<DirEntry> PrintMatchingFilesHandler, Action<int, string> ErrorHandler, Action<string> ProgressHandler, ManualResetEvent CrtlCEvent)
        {
            EnumDirsParallel parallelEnumerator 
                = EnumDirsParallel.Start(dirs, maxDepth, followJunctions, ReportToQueue: PrintMatchingFilesHandler != null, matchFilename, ErrorHandler, CrtlCEvent);

            if (PrintMatchingFilesHandler == null)
            {
                while ( ! parallelEnumerator.IsFinished(1000) )
                {
                    PrintProgress(parallelEnumerator, ProgressHandler);
                }
            }
            else
            {
                RunWithPrinting(PrintMatchingFilesHandler, ProgressHandler, parallelEnumerator);
            }
            return parallelEnumerator.EnumStats;
        }

        private static void RunWithPrinting(Action<DirEntry> PrintMatchingFilesHandler, Action<string> ProgressHandler, EnumDirsParallel parallelEnumerator)
        {
            long lastTicksProgressPrinted = 0;
            while (true)
            {
                parallelEnumerator.TryDequeue(out DirEntry? entry, out bool hasFinished, millisecondsTimeout: 1000);
                if (entry.HasValue)
                {
                    PrintMatchingFilesHandler?.Invoke(entry.Value);
                }
                else if ( hasFinished )
                {
                    break;
                }

                long currentTicks = DateTime.Now.Ticks;
                if ( (currentTicks - lastTicksProgressPrinted) >= TimeSpan.TicksPerSecond)
                {
                    PrintProgress(parallelEnumerator, ProgressHandler);
                    lastTicksProgressPrinted = currentTicks;
                }
            }
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
