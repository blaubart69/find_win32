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
                = EnumDirsParallel.Start(dirs, maxDepth, followJunctions, matchFilename, ErrorHandler);

            while (true)
            {
                parallelEnumerator.TryDequeue(out DirEntry? entry, out bool hasFinished, millisecondsTimeout: 1000);
                if (entry.HasValue)
                {
                    MatchedFileHandler?.Invoke(entry.Value);
                }
                else if (!hasFinished)
                {
                    parallelEnumerator.GetProgress(out ulong running, out ulong FoundEntriesQueueCount, out Stats tmpStats);
                    ProgressHandler?.Invoke($"Enumerations running: {running} | found queue count: {FoundEntriesQueueCount} | Files seen: {tmpStats.AllFiles} | Files matched: {tmpStats.MatchedFiles}");
                }
                else
                {
                    break;
                }
            }

            return parallelEnumerator.EnumStats;
        }
    }
}
