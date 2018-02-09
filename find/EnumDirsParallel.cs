using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Spi.Native;
using Spi.IO;

namespace find
{
    
    class ParallelCtx
    {
        public readonly int         depth;
        public readonly DirEntry    entry;
        //public readonly string dir;

        public ParallelCtx(DirEntry entry, int depth)
        {
            //this.dir = dir;
            this.depth = depth;
            this.entry = entry;
        }
    }
    
    public class EnumDirsParallel
    {
        readonly int _maxDepth;
        readonly bool _followJunctions;
        readonly Action<int, string> _ErrorHandler;
        readonly Predicate<string> _matchFilename;

        readonly ManualResetEvent _isFinishedEvent;
        readonly AutoResetEvent _entryEnqueuedEvent;

        readonly ManualResetEvent _CtrlCEvent;

        readonly Queue<Spi.IO.DirEntry> _FoundEntries;
        long _EnumerationsQueued;
        long _EnumerationsRunning;
        Stats _enumStats;

        private EnumDirsParallel(int maxDepth, bool followJunctions, bool ReportToQueue, Predicate<string> matchFilename, Action<int, string> ErrorHandler, ManualResetEvent CtrlCEvent)
        {
            _maxDepth = maxDepth;
            _followJunctions = followJunctions;
            _matchFilename = matchFilename;
            _CtrlCEvent = CtrlCEvent;
            
            _ErrorHandler = ErrorHandler;
            _isFinishedEvent    = new ManualResetEvent(false);
            
            _enumStats = new Stats();
            if (ReportToQueue)
            {
                _FoundEntries = new Queue<Spi.IO.DirEntry>();
                _entryEnqueuedEvent = new AutoResetEvent(false);
            }
        }
        ~EnumDirsParallel()
        {
            _isFinishedEvent?.Close();
            _entryEnqueuedEvent?.Close();
        }
        public static EnumDirsParallel Start(IEnumerable<string> dirs, int maxDepth, bool followJunctions, bool ReportToQueue, Predicate<string> matchFilename, Action<int, string> dirErrorHandler, ManualResetEvent CtrlCEvent)
        {
            var enumerator = new EnumDirsParallel(maxDepth, followJunctions, ReportToQueue, matchFilename, dirErrorHandler, CtrlCEvent);
            enumerator._internal_Start(dirs);
            return enumerator;
        }
        private void _internal_Start(IEnumerable<string> dirs)
        {
            _EnumerationsQueued = 1;
            try
            {
                foreach (string dir in dirs)
                {
                    QueueOneDirForEnumeration(entry: new DirEntry(dir), currDepth: -1);
                }
            }
            finally
            {
                DecrementEnumerationQueueCountAndSetFinishedIfZero();
            }
        }
        public Stats EnumStats
        {
            get { return _enumStats;  }
        }
        public bool IsFinished(int millisecondsTimeout)
        {
            return _isFinishedEvent.WaitOne(millisecondsTimeout);
        }
        public void GetProgress(out ulong submitted, out ulong running, out ulong FoundEntriesQueueCount, out Stats stats)
        {
            submitted = (ulong)_EnumerationsQueued;
            running = (ulong)_EnumerationsRunning;
            FoundEntriesQueueCount = _FoundEntries == null ? 0 : (ulong)_FoundEntries.Count;
            stats = _enumStats;
        }
        public bool TryDequeue(out Spi.IO.DirEntry entry, out bool hasFinished, int millisecondsTimeout)
        {
            entry = null;

            if (_FoundEntries != null)
            {
                WaitHandle.WaitAny(new WaitHandle[] { _isFinishedEvent, _entryEnqueuedEvent }, millisecondsTimeout);
                {
                    lock (_FoundEntries)
                    {
                        if (_FoundEntries.Count > 0)
                        {
                            entry = _FoundEntries.Dequeue();
                        }
                    }
                }
            }

            hasFinished = _isFinishedEvent.WaitOne(0);

            return entry != null;
        }
        private void DecrementEnumerationQueueCountAndSetFinishedIfZero()
        {
            if (Interlocked.Decrement(ref _EnumerationsQueued) == 0)
            {
                // I'm the last. Enumerations have finished
                _isFinishedEvent.Set();
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
                ParallelCtx ctx = (ParallelCtx)state;
                using (SafeFindHandle SearchHandle = Win32.FindFirstFile(ctx.entry.Fullname + "\\*", out Win32.WIN32_FIND_DATA find_data))
                {
                    if (SearchHandle.IsInvalid)
                    {
                        _ErrorHandler?.Invoke(Marshal.GetLastWin32Error(), ctx.entry.Fullname);
                    }
                    else
                    {
                        RunThreadEnum(SearchHandle, ctx.entry, ctx.depth, ref find_data);
                    }
                }
            }
            catch (Exception ex)
            {
                try
                {
                    _ErrorHandler?.Invoke(99,$"Exception caught (ThreadEnumDir): {ex.Message}\n{ex.StackTrace}");
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
                DecrementEnumerationQueueCountAndSetFinishedIfZero();
            }
        }
        private void RunThreadEnum(SafeFindHandle SearchHandle, DirEntry parentEntry, int currDepth, ref Win32.WIN32_FIND_DATA find_data)
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
                    Interlocked.Increment(ref _enumStats.AllDirs);

                    if (WalkIntoDir(ref find_data, _followJunctions, currDepth, _maxDepth))
                    {
                        QueueOneDirForEnumeration(new DirEntry(parentEntry, find_data), currDepth: currDepth + 1);
                    }
                }
                else
                {
                    long FileSize = (long)Misc.TwoUIntsToULong(find_data.nFileSizeHigh, find_data.nFileSizeLow);
                    Interlocked.Add(ref _enumStats.AllBytes, FileSize);
                    Interlocked.Increment(ref _enumStats.AllFiles);

                    if ( _matchFilename(find_data.cFileName) )
                    {
                        Interlocked.Increment(ref _enumStats.MatchedFiles);
                        Interlocked.Add(ref _enumStats.MatchedBytes, FileSize);

                        if (_FoundEntries != null)
                        {
                            // report ONLY matching items
                            QueueFoundItem(new DirEntry(parentEntry, find_data));
                        }
                    }
                }
            }
            while (Win32.FindNextFile(SearchHandle, out find_data));
        }
        private void QueueOneDirForEnumeration(DirEntry entry, int currDepth)
        {
            Interlocked.Increment(ref _EnumerationsQueued);
            if (!ThreadPool.QueueUserWorkItem(
                    new WaitCallback(ThreadEnumDir), 
                    new ParallelCtx(entry, currDepth)))
            {
                Interlocked.Decrement(ref _EnumerationsQueued);
                Console.Error.WriteLine("ThreadPool.QueueUserWorkItem returned false. STOP!");
                throw new Exception("ThreadPool.QueueUserWorkItem returned false. STOP!");
            }
        }
        private void QueueFoundItem(Spi.IO.DirEntry entry)
        {
            lock (this._FoundEntries)
            {
                _FoundEntries.Enqueue(entry);
            }
            _entryEnqueuedEvent.Set();
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
