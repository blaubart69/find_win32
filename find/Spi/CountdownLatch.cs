using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Spi
{
    public class CountdownLatch : IDisposable
    {
        private int m_remain;
        private readonly EventWaitHandle m_event;

        public CountdownLatch(int count)
        {
            m_remain = count;
            m_event = new ManualResetEvent(false);
        }

        public void Signal()
        {
            // The last thread to signal also sets the event.
            if (Interlocked.Decrement(ref m_remain) == 0)
            {
                m_event.Set();
            }
        }

        public bool Wait(int millisecondsTimeout)
        {
            return m_event.WaitOne(millisecondsTimeout);
        }

        public void Dispose()
        {
            m_event?.Close();
        }
    }
}
