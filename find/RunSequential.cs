using System;
using System.Threading;

using Spi;

namespace find
{
    class RunSequential
    {
        public static Stats Run(Opts opts, Predicate<string> matchFilename, Action<Spi.IO.DirEntry> MatchedFileHandler, Spi.IO.StatusLineWriter StatusWriter, Action<int, string> ErrorHandler, ManualResetEvent CrtlCEvent)
        {
            Stats stats = new Stats();

            foreach (string dir in opts.Dirs)
            {
                if (CrtlCEvent.WaitOne(0))
                {
                    break;
                }
                Console.Error.WriteLine("scanning [{0}]", dir);
                EnumDir.Run(
                    Dirname:            dir, 
                    opts:               opts, 
                    stats:              ref stats,
                    CrtlCEvent:         CrtlCEvent,
                    IsMatchingFile:      matchFilename,
                    MatchedFileHandler: MatchedFileHandler,
                    ErrorHandler:       ErrorHandler,
                    ProgressHandler:    (dirname) => { StatusWriter?.WriteWithDots(dirname); } );
            }

            return stats;
        }
    }
}
