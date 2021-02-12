using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using Spi.IO;

namespace find
{
    class RunParallel
    {
        public static Stats Run(IEnumerable<string> dirs, EnumOptions enumOpts, Action<string> ProgressHandler, CancellationToken CtrlCEvent, int maxThreads)
        {
            bool showMatching = enumOpts.matchFilename != null;

            Stats stats = new Stats();
            foreach (string dir in dirs)
            {
                EnumDirsParallel parEnum = new EnumDirsParallel(dir, enumOpts, CtrlCEvent, ref stats, maxThreads);
                while ( ! parEnum.Wait(1000) )
                {
                    if (ProgressHandler != null)
                    {
                        PrintProgress(ProgressHandler, stats, parEnum.Queued, parEnum.Running, showMatching);
                    }
                }
            }

            return stats;
        }
        private static void PrintProgress(Action<string> ProgressHandler, Stats stats, long queued, long running, bool showMatching)
        {
            Process currProc = System.Diagnostics.Process.GetCurrentProcess();

            string filesLine = showMatching ?
               $" | files seen/matched: {stats.AllFiles:N0} ({Misc.GetPrettyFilesize(stats.AllBytes)}) / {stats.MatchedFiles:N0} ({Misc.GetPrettyFilesize(stats.MatchedBytes)})"
             : $" | files seen: {stats.AllFiles:N0} ({Misc.GetPrettyFilesize(stats.AllBytes)})";

            ProgressHandler(
                  $"dirs enqueued/running: {queued:N0}/{running}"
                + filesLine
                + $" | dirs seen: {stats.AllDirs:N0}"
                + $" | GC/VirtMem/Threads"
                + $" {Misc.GetPrettyFilesize(GC.GetTotalMemory(forceFullCollection: false))}"
                + $"/{Misc.GetPrettyFilesize(currProc.WorkingSet64)}"
                + $"/{currProc.Threads.Count}"
            );
        }
    }
}
