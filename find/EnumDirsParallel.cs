using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Spi.Native;
using Spi.IO;

namespace find
{
    public delegate void PrintFunction(string rootDir, string dir, ref Win32.WIN32_FIND_DATA find_data);
    
    class ParallelCtx
    {
        public readonly int       depth;
        public readonly string    dirToSearchSinceRootDir;

        public ParallelCtx(string DirToSearchSinceRootDir, int depth)
        {
            this.depth = depth;
            this.dirToSearchSinceRootDir = DirToSearchSinceRootDir;
        }
    }
    public enum EMIT
    {
        FILES,
        DIRS,
        BOTH
    }
    public struct EnumOptions
    {
        public int maxDepth;
        public bool followJunctions;
        public Predicate<string> matchFilename;
        public PrintFunction printHandler;
        public Action<int, string> errorHandler;
        public bool lookForLongestFilename;
        public EMIT emit;
    }

    public class EnumDirsParallel
    {
        readonly string _rootDirname;
        readonly EnumOptions _opts;
        readonly ManualResetEvent _CtrlCEvent;
        readonly Spi.CountdownLatch _countdownLatch;
        Stats _stats;

        readonly Queue<ParallelCtx> _workItems;
        int _ThreadpoolUserItemsEnqueued;
        int _maxThreads;

        long _EnumerationsQueued;
        long _EnumerationsRunning;

        private EnumDirsParallel(string RootDir, EnumOptions opts, ManualResetEvent CtrlCEvent, Spi.CountdownLatch CountdownLatch, ref Stats stats, int maxThreads)
        {
            _rootDirname = RootDir;
            _opts = opts;
            _CtrlCEvent = CtrlCEvent;
            _countdownLatch = CountdownLatch;
            _stats = stats;
            _workItems = new Queue<ParallelCtx>();
            _maxThreads = maxThreads;
        }
        public static EnumDirsParallel Start(string dir, EnumOptions opts, ManualResetEvent CtrlCEvent, Spi.CountdownLatch CountdownLatch, ref Stats stats, int maxThreads)
        {
            var enumerator = new EnumDirsParallel(dir, opts, CtrlCEvent, CountdownLatch, ref stats, maxThreads);
            enumerator._internal_Start();
            return enumerator;
        }
        public static EnumDirsParallel Start(string RootDir, IEnumerable<string> relativeDirs, EnumOptions opts, ManualResetEvent CtrlCEvent, Spi.CountdownLatch CountdownLatch, ref Stats stats, int maxThreads)
        {
            var enumerator = new EnumDirsParallel(RootDir, opts, CtrlCEvent, CountdownLatch, ref stats, maxThreads);
            enumerator._internal_Start(relativeDirs);
            return enumerator;
        }
        private void _internal_Start()
        {
            //
            // THIS INCREMENTS ARE FOR ...
            //
            _EnumerationsQueued = 1;
            Interlocked.Increment(ref _stats.Enqueued);
            try
            {
                QueueOneDirForEnumeration(dirSinceRootDir: null, currDepth: -1);
            }
            finally
            {
                //
                // ... THAT DECREMENTS.
                //
                DecrementEnumerationQueueCountAndSetFinishedIfZero();
            }
        }
        private void _internal_Start(IEnumerable<string> relativeDirs)
        {
            //
            // THIS INCREMENTS ARE FOR ...
            //
            _EnumerationsQueued = 1;
            Interlocked.Increment(ref _stats.Enqueued);

            try
            {
                foreach (string relativeDir in relativeDirs)
                {
                    QueueOneDirForEnumeration(dirSinceRootDir: relativeDir, currDepth: -1);
                }
            }
            finally
            {
                //
                // ... THAT DECREMENTS.
                //
                DecrementEnumerationQueueCountAndSetFinishedIfZero();
            }
        }
        private void DecrementEnumerationQueueCountAndSetFinishedIfZero()
        {
                Interlocked.Decrement(ref _stats.Enqueued);
            if (Interlocked.Decrement(ref _EnumerationsQueued) == 0)
            {
                // I'm the last. Enumerations have finished
                _countdownLatch.Signal();
            }
        }
        /***
         * make sure no exception escapes this method!
         * Otherwise we get an "Unhandled exception" and die
         ***/
        private void ThreadEnumDir(object state)
        {
            try
            {
                RunWorkitemLoop();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"{ex.Message}\n{ex.StackTrace}");
            }
        }
        private void RunWorkitemLoop()
        {
            try
            {
                Interlocked.Increment(ref _EnumerationsRunning);
                Interlocked.Increment(ref _stats.EnumerationsRunning);

                while (true)
                {
                    ParallelCtx ctx = null;
                    lock (_workItems)
                    {
                        if (_workItems.Count == 0)
                        {
                            break;
                        }
                        ctx = _workItems.Dequeue();
                    }
                    EnumerateDirFirst(ctx.dirToSearchSinceRootDir, ctx.depth);
                    DecrementEnumerationQueueCountAndSetFinishedIfZero();
                }
            }
            catch (Exception ex)
            {
                _opts.errorHandler?.Invoke(99, $"Exception caught (RunWorkitemLoop): {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                Interlocked.Decrement(ref _EnumerationsRunning);
                Interlocked.Decrement(ref _stats.EnumerationsRunning);
                Interlocked.Decrement(ref _ThreadpoolUserItemsEnqueued);
            }
        }
        private void EnumerateDirFirst(string dirToSearchSinceRootDir, int depth)
        {
            string dirToEnumerate;
            if (String.IsNullOrEmpty(dirToSearchSinceRootDir))
            {
                dirToEnumerate = this._rootDirname;
            }
            else
            {
                dirToEnumerate = Path.Combine(this._rootDirname, dirToSearchSinceRootDir);
            }

            using (SafeFindHandle SearchHandle = Win32.FindFirstFile(dirToEnumerate + "\\*", out Win32.WIN32_FIND_DATA find_data))
            {
                if (SearchHandle.IsInvalid)
                {
                    _opts.errorHandler?.Invoke(Marshal.GetLastWin32Error(), dirToEnumerate);
                }
                else
                {
                    EnumerateDirNext(SearchHandle, dirToSearchSinceRootDir, depth, ref find_data);
                }
            }
        }
        private void EnumerateDirNext(SafeFindHandle SearchHandle, string dirNameSinceRootDir, int currDepth, ref Win32.WIN32_FIND_DATA find_data)
        {
            do
            {
                if (_CtrlCEvent.WaitOne(0))
                {
                    break;
                }
                if (Spi.IO.Misc.IsDotOrDotDotDirectory(find_data.cFileName))
                {
                    continue;
                }

                ProcessFindData(dirNameSinceRootDir, currDepth, ref find_data);
                EmitFindData(dirNameSinceRootDir, ref find_data);
            }
            while (Win32.FindNextFile(SearchHandle, out find_data));
        }
        private void EmitFindData(string dirNameSinceRootDir, ref Win32.WIN32_FIND_DATA find_data)
        {
            bool ShouldEmit =
                                (_opts.matchFilename != null && _opts.matchFilename(find_data.cFileName))
                            || (_opts.matchFilename == null);

            // TODO: when matching IS NULL
            if (ShouldEmit)
            {
                long FileSize = (long)Misc.TwoUIntsToULong(find_data.nFileSizeHigh, find_data.nFileSizeLow);
                Interlocked.Increment(ref _stats.MatchedFiles);
                Interlocked.Add(ref _stats.MatchedBytes, FileSize);

                if ((_opts.emit == EMIT.BOTH)
                    || (_opts.emit == EMIT.FILES && !Misc.IsDirectoryFlagSet(find_data.dwFileAttributes))
                    || (_opts.emit == EMIT.DIRS && Misc.IsDirectoryFlagSet(find_data.dwFileAttributes)))
                {
                    _opts.printHandler?.Invoke(this._rootDirname, dirNameSinceRootDir, ref find_data);
                }
            }
        }

        private void ProcessFindData(string dirNameSinceRootDir, int currDepth, ref Win32.WIN32_FIND_DATA find_data)
        {
            if (Misc.IsDirectoryFlagSet(find_data.dwFileAttributes))
            {
                Interlocked.Increment(ref _stats.AllDirs);

                if (WalkIntoDir(ref find_data, _opts.followJunctions, currDepth, _opts.maxDepth))
                {
                    QueueOneDirForEnumeration(
                        dirSinceRootDir: dirNameSinceRootDir == null ? find_data.cFileName : System.IO.Path.Combine(dirNameSinceRootDir, find_data.cFileName),
                        currDepth: currDepth);
                }
            }
            else
            {
                long FileSize = (long)Misc.TwoUIntsToULong(find_data.nFileSizeHigh, find_data.nFileSizeLow);
                Interlocked.Add(ref _stats.AllBytes, FileSize);
                Interlocked.Increment(ref _stats.AllFiles);

                if (_opts.lookForLongestFilename)
                {
                    int currLength = _rootDirname.Length + 1
                                        + (String.IsNullOrEmpty(dirNameSinceRootDir) ? 0 : dirNameSinceRootDir.Length + 1)
                                        + find_data.cFileName.Length;
                    if (currLength > _stats.LongestFilenameLength)
                    {
                        _stats.LongestFilenameLength = currLength;
                        _stats.LongestFilename = Path.Combine(_rootDirname, dirNameSinceRootDir == null ? find_data.cFileName : System.IO.Path.Combine(dirNameSinceRootDir, find_data.cFileName));
                    }
                }
            }
        }

        private void QueueOneDirForEnumeration(string dirSinceRootDir, int currDepth)
        {
            Interlocked.Increment(ref _EnumerationsQueued);
            Interlocked.Increment(ref _stats.Enqueued);

            bool startNewThread = false;
            lock (_workItems)
            {
                _workItems.Enqueue(new ParallelCtx(dirSinceRootDir, currDepth + 1));

                if (_ThreadpoolUserItemsEnqueued < _maxThreads)
                {
                    startNewThread = true;
                    Interlocked.Increment(ref _ThreadpoolUserItemsEnqueued);
                }
            }

            if (startNewThread)
            {
                if (!ThreadPool.QueueUserWorkItem(callBack: new WaitCallback(ThreadEnumDir)))
                {
                    Interlocked.Decrement(ref _ThreadpoolUserItemsEnqueued);
                    throw new Exception("ThreadPool.QueueUserWorkItem returned false. STOP!");
                }
            }
        }
        public void GetCounter(out ulong queued, out ulong running)
        {
            queued = (ulong)_EnumerationsQueued;
            running = (ulong)_EnumerationsRunning;
        }
        private static bool WalkIntoDir(ref Spi.Native.Win32.WIN32_FIND_DATA findData, bool FollowJunctions, int currDepth, int maxDepth)
        {
            bool enterDir = true;

            if (maxDepth > -1)
            {
                if (currDepth + 1 > maxDepth)
                {
                    return false;
                }
            }

            const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x400;
            if ((findData.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0)
            {
                if (FollowJunctions == false)
                {
                    enterDir = false;
                }
            }

            return enterDir;

        }
    }
}
