using System;
using System.Collections.Concurrent;
using System.Threading;

namespace find.Spi
{
    public class ParallelExecutor<T, C, TL>
    {
        public delegate void WorkFunc(T item, ParallelExecutor<T, C, TL> executor, C context, ref TL threadLocalObject);

        private long _itemCount;
        private long _running;
        private long _done;
        private int _threadsToUse;

        private readonly WorkFunc _workFunc;
        private readonly C _context;

        private readonly Func<TL> _initTLfunc;
        private readonly Action<TL> _freeTLfunc;

        private readonly BlockingCollection<T> _queue;
        private readonly CancellationToken _cancelToken;
        private readonly ManualResetEvent _finished;

        public long Running { get { return _running; } }
        public long Queued { get { return _itemCount; } }
        public long Done { get { return _done; } }

        public ParallelExecutor(Func<TL> initTL, WorkFunc func, Action<TL> freeTL, C context, int maxThreads, CancellationToken ct)
        {
            _queue = new BlockingCollection<T>(new ConcurrentQueue<T>());
            _cancelToken = ct;
            _initTLfunc = initTL;
            _workFunc = func;
            _freeTLfunc = freeTL;
            _threadsToUse = maxThreads;
            _context = context;
            _finished = new ManualResetEvent(false);

            StartWorkerThreads(maxThreads);
        }
        public bool Wait(int milliSeconds)
        {
            return _finished.WaitOne(milliSeconds);
        }

        public void Enqueue(T item)
        {
            Interlocked.Increment(ref _itemCount);
            _queue.Add(item);
        }

        private void ThreadFunc()
        {
            TL threadLocalObject = _initTLfunc == null ? default : _initTLfunc();
            try
            {
                T item;
                while ((item = _queue.Take(_cancelToken)) != null)
                {
                    Interlocked.Increment(ref _running);
                    ExecFunc(item, ref threadLocalObject);
                    Interlocked.Decrement(ref _running);
                    Interlocked.Increment(ref _done);

                    if (Interlocked.Decrement(ref _itemCount) == 0)
                    {
                        SignalEndToOtherThreads();
                        _finished.Set();
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"E-ThreadFunc: bad stuff happend. Exception {ex.Message}");
            }
            finally
            {
                _freeTLfunc?.Invoke(threadLocalObject);
            }
        }

        private void ExecFunc(T item, ref TL threadLocalObject)
        {
            try
            {
                _workFunc(item, this, _context, ref threadLocalObject);
            }
            catch
            {
            }
        }
        private void StartWorkerThreads(int ThreadsToStart)
        {
            for (int i = 0; i < ThreadsToStart; i++)
            {
                new Thread(new ThreadStart(ThreadFunc)).Start();
            }
        }
        private void SignalEndToOtherThreads()
        {
            for (int i = 0; i < _threadsToUse; i++)
            {
                _queue.Add(default);
            }
        }
    }
}
