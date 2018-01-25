using System;
using System.Threading;


namespace find
{
    class EnumDir
    {
        public static void Run(string Dirname, Opts opts, ref Stats stats, ManualResetEvent CrtlCEvent, Predicate<string> matchFilename, Action<Spi.IO.DirEntry> MatchedFileHandler, Action<int, string> ErrorHandler, Action<string> ProgressHandler)
        {
            Spi.IO.StatusLineWriter StatusWriter = new Spi.IO.StatusLineWriter();

            string StartDirectoryFullname = System.IO.Path.GetFullPath(Dirname);

            foreach (var entry in Spi.IO.Directory.Entries(
                startDir: StartDirectoryFullname, 
                DirErrorHandler: ErrorHandler,
                FollowJunctions: opts.FollowJunctions,
                EnterDir: null,
                maxDepth: opts.Depth))
            {
                if (CrtlCEvent.WaitOne(0))
                {
                    break;
                }

                if (entry.IsDirectory)
                {
                    stats.AllDirs += 1;
                    ProgressHandler?.Invoke(StartDirectoryFullname);
                }
                else
                {
                    stats.AllBytes += (long)entry.Filesize;
                    stats.AllFiles += 1;

                    if ( matchFilename?.Invoke(entry.Name) == true )
                    { 
                        stats.MatchedBytes += (long)entry.Filesize;
                        stats.MatchedFiles += 1;
                        MatchedFileHandler?.Invoke(entry);
                    }
                }
            }
        }

    }
}
