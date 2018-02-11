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
        public static Stats Run(IEnumerable<string> dirs, EnumOptions opts, Action<string> ProgressHandler, ManualResetEvent CrtlCEvent)
        {
            Spi.CountdownLatch countdown = new Spi.CountdownLatch(dirs.Count());
            Process currProc = System.Diagnostics.Process.GetCurrentProcess();
            Stats stats = new Stats();

            List<EnumDirsParallel> enums = new List<EnumDirsParallel>();
            foreach (string dir in dirs)
            {
                EnumDirsParallel parallelEnumerator = EnumDirsParallel.Start(dir, opts, CrtlCEvent, countdown, ref stats);
                enums.Add(parallelEnumerator);
            }

            while ( ! countdown.Wait(1000) )
            {
                if ( CrtlCEvent.WaitOne(0) )
                {
                    break;
                }
                PrintProgress(ProgressHandler, stats, currProc);
            }

            return stats;
        }
        private static void PrintProgress(Action<string> ProgressHandler, Stats stats, Process currProc)
        {
            if ( ProgressHandler == null)
            {
                return;
            }

            ProgressHandler(
                  $"Enumerations enqueued/running: {stats.Enqueued}/{stats.EnumerationsRunning}"
                + $" | Files seen/matched: {stats.AllFiles} ({Misc.GetPrettyFilesize(stats.AllBytes)}) / {stats.MatchedFiles} ({Spi.IO.Misc.GetPrettyFilesize(stats.MatchedBytes)})"
                + $" | GC.Total: {Misc.GetPrettyFilesize(GC.GetTotalMemory(forceFullCollection: false))}"
                + $" | PrivateMemory: {Misc.GetPrettyFilesize(currProc.PrivateMemorySize64)}"
                + $" | Threads: {currProc.Threads.Count}"
            );
        }
    }
}
