using System;
using System.Collections.Generic;
using System.Threading;

using Spi;

namespace find
{
    class RunSequential
    {
        public static Stats Run(IEnumerable<string> dirs, EnumOptions enumOpts, Action<string> ProgressHandler, ManualResetEvent CrtlCEvent)
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
                    opts:               enumOpts,
                    stats:              ref stats,
                    CrtlCEvent:         CrtlCEvent,
                    ProgressHandler:    ProgressHandler);
            }

            return stats;
        }
    }
}
