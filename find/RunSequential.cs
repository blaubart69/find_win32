using System;
using System.Collections.Generic;
using System.Threading;

using Spi;

namespace find
{
    class RunSequential
    {
        public static Stats Run(IEnumerable<string> dirs, int maxDepth, bool followJunctions, Predicate<string> matchFilename, Action<Spi.IO.DirEntry> MatchedFileHandler, Action<string> ProgressHandler, Action<int, string> ErrorHandler, ManualResetEvent CrtlCEvent)
        {
            Stats stats = new Stats();

            foreach (string dir in dirs)
            {
                if (CrtlCEvent.WaitOne(0))
                {
                    break;
                }
                
                EnumDir.Run(
                    Dirname:            dir, 
                    maxDepth:           maxDepth,
                    followJunctions:    followJunctions,
                    stats:              ref stats,
                    CrtlCEvent:         CrtlCEvent,
                    IsMatchingFile:      matchFilename,
                    MatchedFileHandler: MatchedFileHandler,
                    ErrorHandler:       ErrorHandler,
                    ProgressHandler:    ProgressHandler);
            }

            return stats;
        }
    }
}
