using System;
using System.Threading;


namespace find
{
    class EnumDir
    {
        public static void Run(string Dirname, EnumOptions opts, ref Stats stats, ManualResetEvent CrtlCEvent, Action<string> ProgressHandler)
        {
            Spi.StatusLineWriter StatusWriter = new Spi.StatusLineWriter();

            foreach (var entry in Spi.IO.Directory.Entries(
                startDir: Dirname, 
                DirErrorHandler: opts.errorHandler,
                FollowJunctions: opts.followJunctions,
                EnterDir: null,
                maxDepth: opts.maxDepth))
            {
                if (CrtlCEvent.WaitOne(0))
                {
                    break;
                }

                if (entry.IsDirectory)
                {
                    stats.AllDirs += 1;
                    ProgressHandler?.Invoke(entry.Name);
                }
                else
                {
                    stats.AllBytes += (long)entry.Filesize;
                    stats.AllFiles += 1;

                    if ( opts.matchFilename?.Invoke(entry.Name) == true )
                    { 
                        stats.MatchedBytes += (long)entry.Filesize;
                        stats.MatchedFiles += 1;
                        opts.printHandler?.Invoke(Dirname, entry.dirSinceRootDir, entry._FindData);
                    }
                }
            }
        }

    }
}
