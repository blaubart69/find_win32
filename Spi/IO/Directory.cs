using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Spi.IO
{
    public class Directory
    {
        private struct Internal_DirInfo
        {
            public int DirnameLength;
            public SafeFileHandle handle;
        }

        public struct DirEntry
        {
            private StringBuilder Dir;
            private int BaseDirLen;

            public readonly Spi.Win32.WIN32_FIND_DATA FindData;

            public bool isDirectory { get { return IsDirectoryFlagSet(FindData.dwFileAttributes); } }
            public string Filename { get { return FindData.cFileName; } }
            public UInt64 Filesize { get { return (((UInt64)FindData.nFileSizeHigh) << 32) | (UInt64)FindData.nFileSizeLow; } }
            public string Dirname  { get { return Dir.ToString(); } }
            public string DirAndFilenameFromStartDir { get { return GetFilenameSinceBaseDir(Dirname, BaseDirLen, FindData.cFileName); } }
            public string Fullname { 
                get 
                {
                    if (isDirectory)
                    {
                        return Dirname;
                    }
                    else
                    {
                        return Dirname + Path.DirectorySeparatorChar + FindData.cFileName;
                    }
                } 
            }
            public DateTime LastWriteTime { get { return Spi.IO.Misc.ConvertFromFiletime(FindData.ftLastWriteTime.dwHighDateTime, FindData.ftLastWriteTime.dwLowDateTime); } }

            public DirEntry(StringBuilder Dirname, Spi.Win32.WIN32_FIND_DATA FindData, int BaseDirLen)
            {
                this.Dir = Dirname;
                this.BaseDirLen = BaseDirLen;
                this.FindData = FindData;
            }
        }
        public static IEnumerable<DirEntry> Entries(string startDir, Action<int,string> DirErrorHandler)
        {
            return Entries(startDir, DirErrorHandler, -1, null);
        }
        public static IEnumerable<DirEntry> Entries(string startDir, Action<int,string> DirErrorHandler, int maxDepth, Predicate<string> EnterDir)
        {
            // expand directory to "unicode" convention
            StringBuilder           dir             = new StringBuilder( Misc.GetLongFilenameNotation(startDir) );
            int                     baseDirLength   = dir.Length;
            SafeFileHandle          SearchHandle    = null;
            Stack<Internal_DirInfo> dirStack        = new Stack<Internal_DirInfo>();
            int                     depth           = 0;
            Win32.WIN32_FIND_DATA   find_data;

            bool findFirstFile = true;

            do
            {
                if (findFirstFile)
                {
                    findFirstFile = false;

                    dir.Append("\\*");
                    SearchHandle = Win32.FindFirstFile(dir.ToString(), out find_data);
                    dir.Length -= 2;    // remove \* added before
                    if (SearchHandle.IsInvalid)
                    {
                        if ( DirErrorHandler != null)
                        {
                            DirErrorHandler(Marshal.GetLastWin32Error(), dir.ToString());
                        }
                        StepBack(ref dir, ref dirStack, out SearchHandle, ref depth);
                        continue;
                    }
                }
                else
                {
                    if (!Win32.FindNextFile(SearchHandle, out find_data))
                    {
                        Win32.FindClose(SearchHandle);
                        StepBack(ref dir, ref dirStack, out SearchHandle, ref depth);
                        continue;
                    }
                }
                if (!".".Equals(find_data.cFileName) && !"..".Equals(find_data.cFileName))
                {
                    if ( IsDirectoryFlagSet(find_data.dwFileAttributes)) // is a dir
                    {
                        //
                        // should we walk into this dir?
                        //
                        if (EnterDir == null || EnterDir(find_data.cFileName))
                        {
                            yield return new DirEntry(dir, find_data, baseDirLength);
                            //
                            // go down if depth is ok
                            //
                            if (maxDepth == -1 || depth < maxDepth)
                            {
                                depth++;
                                dirStack.Push(new Internal_DirInfo() { DirnameLength = find_data.cFileName.Length, handle = SearchHandle });
                                dir.Append("\\").Append(find_data.cFileName);
                                findFirstFile = true;
                            }
                        }
                    }
                    else
                    {
                        yield return new DirEntry(dir,find_data, baseDirLength);
                    }
                }
            } while (SearchHandle != null);
        }

        private static void StepBack(ref StringBuilder dir, ref Stack<Internal_DirInfo> dirStack, out SafeFileHandle SearchHandle, ref int depth)
        {
            if (dirStack.Count > 0)
            {
                Internal_DirInfo di = dirStack.Pop();
                dir.Length = dir.Length - di.DirnameLength - 1; // remove "\" and the directory name 
                SearchHandle = di.handle;
                depth--;
            }
            else
            {
                SearchHandle = null;
            }

        }
        private static string GetFilenameSinceBaseDir(string dir, int baseLen, string filename)
        {
            if (dir.Length == baseLen)
            {
                return filename;
            }
            else
            {
                // must be larger
                return dir.Substring(baseLen + 1) + "\\" + filename;
            }
        }
        private static bool IsDirectoryFlagSet(uint dwFileAttributes)
        {
            return ((dwFileAttributes & 0x10) != 0);
        }
    }
}
