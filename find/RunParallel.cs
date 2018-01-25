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
            Parallel.EnumDirsParallel parallelEnumerator 
                = new Parallel.EnumDirsParallel(maxDepth, followJunctions, matchFilename, ErrorHandler);

            parallelEnumerator.Start(dirs);

            bool hasFinished;
            do
            {
                parallelEnumerator.TryDequeue(out DirEntry? entry, out hasFinished, millisecondsTimeout: 1000);
                if (entry.HasValue)
                {
                    MatchedFileHandler?.Invoke(entry.Value);
                }
                else if (!hasFinished)
                {
                    parallelEnumerator.GetProgress(out ulong running, out ulong FoundEntriesQueueCount, out Stats tmpStats);
                    ProgressHandler($"Enumerations running: {running} | found queue count: {FoundEntriesQueueCount} | Files seen: {tmpStats.AllFiles} | Files matched: {tmpStats.MatchedFiles}");
                }
            }
            while (!hasFinished);

            return parallelEnumerator.EnumStats;
        }
    }
}
