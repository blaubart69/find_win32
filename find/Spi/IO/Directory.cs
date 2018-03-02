using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

using Spi.Native;

namespace Spi.IO
{
    public class Directory
    {
        private struct Internal_DirInfo
        {
            public int DirnameLength;
            public SafeFindHandle handle;
        }

        public static IEnumerable<DirEntry> Entries(string startDir, Action<int,string> DirErrorHandler)
        {
            return Entries(startDir, DirErrorHandler, false);
        }
        public static IEnumerable<DirEntry> Entries(string startDir, Action<int, string> DirErrorHandler, bool FollowJunctions)
        {
            return Entries(startDir, DirErrorHandler, -1, null, FollowJunctions);
        }
        public static IEnumerable<DirEntry> Entries(string startDir, Action<int,string> DirErrorHandler, int maxDepth, Predicate<string> EnterDir, bool FollowJunctions)
        {
            // expand directory to "unicode" convention
            StringBuilder           dir             = new StringBuilder( startDir );
            SafeFindHandle          SearchHandle    = null;
            Stack<Internal_DirInfo> dirStack        = new Stack<Internal_DirInfo>();
            int                     depth           = 0;
            Spi.Native.Win32.WIN32_FIND_DATA   find_data;

            bool findFirstFile = true;

            /*
            if (dir[dir.Length-1] == '\\')
            {
                dir.Length -= 1;
            }
            */

            int rootDirLength = dir.Length;

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
                        DirErrorHandler?.Invoke(Marshal.GetLastWin32Error(), dir.ToString());
                        StepBack(ref dir, ref dirStack, out SearchHandle, ref depth);
                        continue;
                    }
                }
                else
                {
                    if (!Spi.Native.Win32.FindNextFile(SearchHandle, out find_data))
                    {
                        SearchHandle.Dispose();
                        StepBack(ref dir, ref dirStack, out SearchHandle, ref depth);
                        continue;
                    }
                }
                if ( Spi.IO.Misc.IsDirectoryFlagSet(find_data.dwFileAttributes)) // is a dir
                {
                    if (Misc.IsDotOrDotDotDirectory(find_data.cFileName))
                    {
                        continue;
                    }
                    if ( WalkIntoDir(ref find_data, EnterDir, FollowJunctions) )
                    {
                        yield return new DirEntry( RemoveRootDir(dir.ToString(), rootDirLength),  find_data);
                        //yield return new DirEntry(dir.ToString(), find_data);
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
                    yield return new DirEntry(RemoveRootDir(dir.ToString(), rootDirLength), find_data);
                    //yield return new DirEntry(dir.ToString(), find_data);
                }
            } while (SearchHandle != null);
        }
        private static string RemoveRootDir(string Fullname, int RootDirLength)
        {
            if ( Fullname.Length == RootDirLength )
            {
                return String.Empty;
            }
            else
            {
                return Fullname.Substring(RootDirLength);
            }
        }
        private static bool WalkIntoDir(ref Spi.Native.Win32.WIN32_FIND_DATA findData, Predicate<string> EnterDir, bool FollowJunctions)
        {
            const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x400;

            if ( (findData.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0)
            {
                // 2015-12-01 Spindler  is junction/symlink or whatever
                if ( FollowJunctions == false )
                {
                    return false;
                }
            }

            if ( EnterDir == null )
            {
                return true;
            }

            return EnterDir(findData.cFileName);

        }
        
        private static void StepBack(ref StringBuilder dir, ref Stack<Internal_DirInfo> dirStack, out SafeFindHandle SearchHandle, ref int depth)
        {
            if (dirStack.Count > 0)
            {
                Internal_DirInfo LastDir = dirStack.Pop();
                dir.Length = dir.Length - LastDir.DirnameLength - 1; // remove "\" and the directory name 
                SearchHandle = LastDir.handle;
                depth--;
            }
            else
            {
                SearchHandle = null;
            }

        }
        
    }
}
