using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Spi.Native;
using Spi.IO;

namespace find
{
    public delegate void PrintFunction(string rootDir, string dir, Win32.WIN32_FIND_DATA find_data);
    
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
    
    public struct EnumOptions
    {
        public int maxDepth;
        public bool followJunctions;
        public Predicate<string> matchFilename;
        public PrintFunction printHandler;
        public Action<int, string> errorHandler;
    }

    public class EnumDirsParallel
    {
        readonly string _rootDirname;
        readonly EnumOptions _opts;
        readonly ManualResetEvent _CtrlCEvent;
        readonly Spi.CountdownLatch _countdownLatch;
        Stats _stats;

        long _EnumerationsQueued;
        long _EnumerationsRunning;

        private EnumDirsParallel(string RootDir, EnumOptions opts, ManualResetEvent CtrlCEvent, Spi.CountdownLatch CountdownLatch, ref Stats stats)
        {
            _rootDirname = RootDir;
            _opts = opts;
            _CtrlCEvent = CtrlCEvent;
            _countdownLatch = CountdownLatch;
            _stats = stats;
        }
        public static EnumDirsParallel Start(string dir, EnumOptions opts, ManualResetEvent CtrlCEvent, Spi.CountdownLatch CountdownLatch, ref Stats stats)
        {
            var enumerator = new EnumDirsParallel(dir, opts, CtrlCEvent, CountdownLatch, ref stats);
            enumerator._internal_Start(dir);
            return enumerator;
        }
        private void _internal_Start(string dir)
        {
            _EnumerationsQueued = 1;
            try
            {
                QueueOneDirForEnumeration(dirSinceRootDir: null, currDepth: -1);
            }
            finally
            {
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
                Interlocked.Increment(ref _EnumerationsRunning);
                Interlocked.Increment(ref _stats.EnumerationsRunning);

                ParallelCtx ctx = (ParallelCtx)state;

                string dirToEnumerate;
                if ( String.IsNullOrEmpty(ctx.dirToSearchSinceRootDir) )
                {
                    dirToEnumerate = this._rootDirname;
                }
                else
                {
                    dirToEnumerate = Path.Combine(this._rootDirname, ctx.dirToSearchSinceRootDir);
                }

                using (SafeFindHandle SearchHandle = Win32.FindFirstFile(dirToEnumerate + "\\*", out Win32.WIN32_FIND_DATA find_data))
                {
                    if (SearchHandle.IsInvalid)
                    {
                        _opts.errorHandler?.Invoke(Marshal.GetLastWin32Error(), dirToEnumerate);
                    }
                    else
                    {
                        RunThreadEnum(SearchHandle, ctx.dirToSearchSinceRootDir, ctx.depth, ref find_data);
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    _opts.errorHandler?.Invoke(99,$"Exception caught (ThreadEnumDir): {ex.Message}\n{ex.StackTrace}");
                }
                catch (Exception ex2)
                {
                    Console.Error.WriteLine("Exception writing exception to ErrorHandler. Bad.");
                    Console.Error.WriteLine($"First exception: {ex.Message}\n{ex.StackTrace}");
                    Console.Error.WriteLine($"Second exception: {ex2.Message}\n{ex2.StackTrace}");
                }
            }
            finally
            {
                Interlocked.Decrement(ref _EnumerationsRunning);
                Interlocked.Decrement(ref _stats.EnumerationsRunning);
                DecrementEnumerationQueueCountAndSetFinishedIfZero();
            }
        }
        private void RunThreadEnum(SafeFindHandle SearchHandle, string dirNameSinceRootDir, int currDepth, ref Win32.WIN32_FIND_DATA find_data)
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
                    Interlocked.Add      (ref _stats.AllBytes, FileSize);
                    Interlocked.Increment(ref _stats.AllFiles);

                    // TODO: when matching IS NULL
                    if ( _opts.matchFilename(find_data.cFileName) )
                    {
                        Interlocked.Increment(ref _stats.MatchedFiles);
                        Interlocked.Add(ref _stats.MatchedBytes, FileSize);

                        _opts.printHandler?.Invoke(this._rootDirname, dirNameSinceRootDir, find_data);
                    }
                }
            }
            while (Win32.FindNextFile(SearchHandle, out find_data));
        }
        private void QueueOneDirForEnumeration(string dirSinceRootDir, int currDepth)
        {
            Interlocked.Increment(ref _EnumerationsQueued);
            Interlocked.Increment(ref _stats.Enqueued);

            if (!ThreadPool.QueueUserWorkItem(
                    new WaitCallback(ThreadEnumDir), 
                    new ParallelCtx(dirSinceRootDir, currDepth + 1)))
            {
                Interlocked.Decrement(ref _EnumerationsQueued);
                Console.Error.WriteLine("ThreadPool.QueueUserWorkItem returned false. STOP!");
                throw new Exception("ThreadPool.QueueUserWorkItem returned false. STOP!");
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
