using System;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using Spi.Native;
using Spi.IO;
using System.Text;

namespace find
{
    public delegate void PrintFunction(string rootDir, string dir, ref Win32.WIN32_FIND_DATA find_data);
    
    class DirToEnum
    {
        public readonly int       depth;
        public readonly string    dirToSearchSinceRootDir;

        public DirToEnum(string DirToSearchSinceRootDir, int depth)
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
        readonly CancellationToken _CtrlCEvent;
        readonly Spi.ParallelExecutor<DirToEnum, object, StringBuilder> _executor;

        Stats _stats;

        public bool Wait(int milliSeconds)
        {
            return _executor.Wait(milliSeconds);
        }

        public EnumDirsParallel(string RootDir, EnumOptions opts, CancellationToken CtrlCEvent, ref Stats stats, int maxThreads)
        {
            _rootDirname = RootDir;
            _opts = opts;
            _CtrlCEvent = CtrlCEvent;
            _stats = stats;

            _executor = new Spi.ParallelExecutor<DirToEnum, object, StringBuilder>(
                  initTL: null
                , func: ThreadMethod
                , freeTL: null
                , context: null
                , maxThreads: maxThreads
                , ct: CtrlCEvent);

            _executor.Enqueue(new DirToEnum(String.Empty,0) );
        }
        private void ThreadMethod(DirToEnum item, Spi.ParallelExecutor<DirToEnum, object, StringBuilder> executor, object ctx, ref StringBuilder tlsBuilder)
        {
            Enumerate(item.dirToSearchSinceRootDir, item.depth);
        }
        private void Enumerate(string baseDir, int depth)
        {
            string FullDir;
            if (String.IsNullOrEmpty(baseDir))
            {
                FullDir = this._rootDirname;
            }
            else
            {
                FullDir = Path.Combine(this._rootDirname, baseDir);
            }

            Win32.WIN32_FIND_DATA find_data = new Win32.WIN32_FIND_DATA();
            using (SafeFindHandle SearchHandle = Win32.FindFirstFile(FullDir + "\\*", ref find_data))
            {
                if (SearchHandle.IsInvalid)
                {
                    _opts.errorHandler?.Invoke(Marshal.GetLastWin32Error(), FullDir);
                }
                else
                {
                    EnumerateDirNext(SearchHandle, baseDir, depth, ref find_data);
                }
            }
        }
        private void EnumerateDirNext(SafeFindHandle SearchHandle, string baseDir, int currDepth, ref Win32.WIN32_FIND_DATA find_data)
        {
            do
            {
                if (Misc.IsDotOrDotDotDirectory(find_data.cFileName))
                {
                    continue;
                }

                ProcessFindData(baseDir, currDepth, ref find_data);
                // -----
                if (_opts.lookForLongestFilename)
                {
                    HandleLongestFilename(baseDir, find_data.cFileName);
                }
                // -----
                if (    (_opts.matchFilename == null)
                     || (_opts.matchFilename != null && _opts.matchFilename(find_data.cFileName)) )
                {
                    EmitFindData(baseDir, ref find_data);
                }
            }
            while (Win32.FindNextFile(SearchHandle, ref find_data));
        }

        private void ProcessFindData(string baseDir, int currDepth, ref Win32.WIN32_FIND_DATA find_data)
        {
            if (Misc.IsDirectoryFlagSet(find_data.dwFileAttributes))
            {
                Interlocked.Increment(ref _stats.AllDirs);
                if (WalkIntoDir(ref find_data, _opts.followJunctions, currDepth, _opts.maxDepth))
                {
                    string NewDirToEnqueue = System.IO.Path.Combine(baseDir, find_data.cFileName);
                    _executor.Enqueue(new DirToEnum(NewDirToEnqueue, currDepth + 1));
                }
            }
            else
            {
                Interlocked.Add(ref _stats.AllBytes, (long)find_data.Filesize);
                Interlocked.Increment(ref _stats.AllFiles);
            }
        }
        private void EmitFindData(string baseDir, ref Win32.WIN32_FIND_DATA find_data)
        {
            Interlocked.Increment(ref _stats.MatchedFiles);
            Interlocked.Add(ref _stats.MatchedBytes, (long)find_data.Filesize);

            if ((_opts.emit == EMIT.BOTH)
                || (_opts.emit == EMIT.FILES && !Misc.IsDirectoryFlagSet(find_data.dwFileAttributes))
                || (_opts.emit == EMIT.DIRS  &&  Misc.IsDirectoryFlagSet(find_data.dwFileAttributes)))
            {
                _opts.printHandler?.Invoke(this._rootDirname, baseDir, ref find_data);
            }
        }
        private void HandleLongestFilename(string baseDir, string Filename)
        {
            int currLength = _rootDirname.Length + 1
                                        + (String.IsNullOrEmpty(baseDir) ? 0 : baseDir.Length + 1)
                                        + Filename.Length;
            if (currLength > _stats.LongestFilenameLength)
            {
                _stats.LongestFilenameLength = currLength;
                _stats.LongestFilename = Path.Combine(_rootDirname, baseDir, Filename);
            }
        }
        private static bool WalkIntoDir(ref Win32.WIN32_FIND_DATA findData, bool FollowJunctions, int currDepth, int maxDepth)
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
        public long Running { get { return _executor.Running;  } }
        public long Queued { get { return _executor.Queued; } }
    }
}
