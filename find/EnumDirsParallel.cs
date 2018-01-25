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
        public readonly string dir;
        public readonly int depth;

        public ParallelCtx(string dir, int depth)
        {
            this.dir = dir;
            this.depth = depth;
        }
    }
    public class EnumDirsParallel
    {
        readonly int _maxDepth;
        readonly bool _followJunctions;
        readonly Action<int, string> _dirErrorHandler;
        readonly Predicate<string> _matchFilename;

        readonly ManualResetEvent _isFinishedEvent;
        readonly AutoResetEvent _entryEnqueuedEvent;

        readonly Queue<Spi.IO.DirEntry> _SHR_queueEntriesFound;
        long _SHR_queued;
        Stats _enumStats;

        private EnumDirsParallel(int maxDepth, bool followJunctions, Predicate<string> matchFilename, Action<int, string> dirErrorHandler)
        {
            _maxDepth = maxDepth;
            _followJunctions = followJunctions;
            _matchFilename = matchFilename;
            _SHR_queued = 0;
            _SHR_queueEntriesFound = new Queue<Spi.IO.DirEntry>();
            _dirErrorHandler = dirErrorHandler;
            _isFinishedEvent    = new ManualResetEvent(false);
            _entryEnqueuedEvent = new AutoResetEvent(false);
            _enumStats = new Stats();
        }
        public static EnumDirsParallel Start(IEnumerable<string> dirs, int maxDepth, bool followJunctions, Predicate<string> matchFilename, Action<int, string> dirErrorHandler)
        {
            var enumerator = new EnumDirsParallel(maxDepth, followJunctions, matchFilename, dirErrorHandler);
            enumerator._internal_Start(dirs);
            return enumerator;
        }
        private void _internal_Start(IEnumerable<string> dirs)
        {
            _SHR_queued = 1;
            foreach (string dir in dirs)
            {
                QueueOneDirForEnumeration(dir: dir, currDepth: -1);
            }
            DecrementQueueCountAndSetFinishedIfZero();
        }
        public Stats EnumStats
        {
            get { return _enumStats;  }
        }
        public void GetProgress(out ulong running, out ulong FoundEntriesQueueCount, out Stats stats)
        {
            running = (ulong)_SHR_queued;
            FoundEntriesQueueCount = (ulong)_SHR_queueEntriesFound.Count;
            stats = _enumStats;
        }
        public bool TryDequeue(out Spi.IO.DirEntry? entry, out bool hasFinished, int millisecondsTimeout)
        {
            entry = null;

            lock (_SHR_queueEntriesFound )
            {
                if ( _SHR_queueEntriesFound.Count > 0 )
                {
                    entry = _SHR_queueEntriesFound.Dequeue();
                }
            }

            if (entry == null)
            {
                if (_entryEnqueuedEvent.WaitOne(millisecondsTimeout))
                {
                    lock (_SHR_queueEntriesFound)
                    {
                        if (_SHR_queueEntriesFound.Count > 0)
                        {
                            entry = _SHR_queueEntriesFound.Dequeue();
                        }
                    }
                }
            }

            hasFinished = _isFinishedEvent.WaitOne(0);

            return entry.HasValue;
        }
        private void DecrementQueueCountAndSetFinishedIfZero()
        {
            if (Interlocked.Decrement(ref _SHR_queued) == 0)
            {
                // I'm the last. Enumerations have finished
                _isFinishedEvent.Set();
            }
        }
        private void ThreadEnumDir(object state)
        {
            try
            {
                ParallelCtx ctx = (ParallelCtx)state;
                using (SafeFindHandle SearchHandle = Win32.FindFirstFile(ctx.dir + "\\*", out Win32.WIN32_FIND_DATA find_data))
                {
                    if (SearchHandle.IsInvalid)
                    {
                        _dirErrorHandler?.Invoke(Marshal.GetLastWin32Error(), ctx.dir);
                    }
                    else
                    {
                        RunThreadEnum(SearchHandle, ctx.dir, ctx.depth, ref find_data);
                    }
                }
            }
            finally
            {
                DecrementQueueCountAndSetFinishedIfZero();
            }
        }
        private void RunThreadEnum(SafeFindHandle SearchHandle, string dirname, int currDepth, ref Win32.WIN32_FIND_DATA find_data)
        {
            do
            {
                if (Spi.IO.Misc.IsDotOrDotDotDirectory(find_data.cFileName))
                {
                    continue;
                }
                if (Misc.IsDirectoryFlagSet(find_data.dwFileAttributes))
                {
                    Interlocked.Increment(ref _enumStats.AllDirs);

                    if (WalkIntoDir(ref find_data, _followJunctions, currDepth, _maxDepth))
                    {
                        QueueOneDirForEnumeration(Path.Combine(dirname, find_data.cFileName), currDepth);
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

                        // report ONLY matching items
                        QueueFoundItem(new DirEntry(dirname, find_data));
                    }
                }
            }
            while (Win32.FindNextFile(SearchHandle, out find_data));
        }
        private void QueueOneDirForEnumeration(string dir, int currDepth)
        {
            Interlocked.Increment(ref _SHR_queued);
            if (!ThreadPool.QueueUserWorkItem(new WaitCallback(ThreadEnumDir), new ParallelCtx(dir, currDepth + 1)))
            {
                Interlocked.Decrement(ref _SHR_queued);
                throw new Exception("ThreadPool.QueueUserWorkItem returned false. STOP!");
            }
        }
        private void QueueFoundItem(Spi.IO.DirEntry entry)
        {
            lock (this._SHR_queueEntriesFound)
            {
                _SHR_queueEntriesFound.Enqueue(entry);
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
