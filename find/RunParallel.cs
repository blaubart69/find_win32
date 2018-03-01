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

            while ( ! countdown.Wait(1000) )
            {
                if ( CrtlCEvent.WaitOne(0) )
                {
                    break;
                }
                PrintProgress(ProgressHandler, stats);
            }
            PrintProgress(ProgressHandler, stats);

            return stats;
        }
        private static void PrintProgress(Action<string> ProgressHandler, Stats stats)
        {
            if ( ProgressHandler == null)
            {
                return;
            }

            Process currProc = System.Diagnostics.Process.GetCurrentProcess();

            ProgressHandler(
                  $"Enumerations enqueued/running: {stats.Enqueued}/{stats.EnumerationsRunning}"
                + $" | files seen/matched: {stats.AllFiles} ({Misc.GetPrettyFilesize(stats.AllBytes)}) / {stats.MatchedFiles} ({Spi.IO.Misc.GetPrettyFilesize(stats.MatchedBytes)})"
                + $" | dirs seen: {stats.AllDirs}"
                + $" | GC.Total: {Misc.GetPrettyFilesize(GC.GetTotalMemory(forceFullCollection: false))}"
                + $" | PrivateMemory: {Misc.GetPrettyFilesize(currProc.PrivateMemorySize64)}"
                + $" | Threads: {currProc.Threads.Count}"
            );
        }
    }
}
