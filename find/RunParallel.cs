using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;

using Spi.IO;

namespace find
{
    class RunParallel
    {
        public static Stats Run(IEnumerable<string> dirs, EnumOptions enumOpts, Action<string> ProgressHandler, ManualResetEvent CrtlCEvent, int maxThreads)
        {
            Spi.CountdownLatch countdown = new Spi.CountdownLatch(dirs.Count());
            Stats stats = new Stats();

            foreach (string dir in dirs)
            {
                EnumDirsParallel parallelEnumerator = EnumDirsParallel.Start(dir, enumOpts, CrtlCEvent, countdown, ref stats, maxThreads);
            }

            bool showMatching = enumOpts.matchFilename != null;
            while ( ! countdown.Wait(1000) )
            {
                if ( CrtlCEvent.WaitOne(0) )
                {
                    break;
                }
                PrintProgress(ProgressHandler, stats, showMatching);
            }
            PrintProgress(ProgressHandler, stats, showMatching);

            return stats;
        }
        private static void PrintProgress(Action<string> ProgressHandler, Stats stats, bool showMatching)
        {
            if ( ProgressHandler == null)
            {
                return;
            }

            Process currProc = System.Diagnostics.Process.GetCurrentProcess();

            string filesLine = showMatching ?
               $" | files seen/matched: {stats.AllFiles} ({Misc.GetPrettyFilesize(stats.AllBytes)}) / {stats.MatchedFiles} ({Spi.IO.Misc.GetPrettyFilesize(stats.MatchedBytes)})"
             : $" | files seen: {stats.AllFiles} ({Misc.GetPrettyFilesize(stats.AllBytes)})";

            ProgressHandler(
                  $"Enumerations enqueued/running: {stats.Enqueued}/{stats.EnumerationsRunning}"
                + filesLine
                + $" | dirs seen: {stats.AllDirs}"
                + $" | GC/VirtMem/Threads"
                + $" {Misc.GetPrettyFilesize(GC.GetTotalMemory(forceFullCollection: false))}"
                + $"/{Misc.GetPrettyFilesize(currProc.VirtualMemorySize64)}"
                + $"/{currProc.Threads.Count}"
            );
        }
    }
}
