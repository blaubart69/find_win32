using System;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace Spi.IO
{
    public class Long
    {
        public static string GetLongFilenameNotation(string Filename)
        {
            if (Filename.StartsWith(@"\\?\"))
            {
                return Filename;
            }

            if (Filename.Length >= 2 && Filename[1] == ':')
            {
                return @"\\?\" + Filename;
            }
            else if (Filename.StartsWith(@"\\") && !Filename.StartsWith(@"\\?\"))
            {
                return @"\\?\UNC\" + Filename.Remove(0, 2);
            }
            return Filename;
        }
        public static bool IsDirectory(string dir)
        {
            uint rc = Spi.Native.Win32.GetFileAttributes(dir);

            if (rc == uint.MaxValue)
            {
                //int LastError = Spi.Win32.GetLastWin32Error();
                return false;   // doesn't exist
            }
            /*
            FILE_ATTRIBUTE_DIRECTORY
            16 (0x10)
            The handle that identifies a directory.
            */
            return (rc & 0x10) != 0;
        }
        public static int CreateFile(string Longfilename, FileAccess fAccess, FileShare fShare, FileMode fMode, FileAttributes fAttr, out SafeFileHandle handle)
        {
            handle = Spi.Native.Win32.CreateFileW(Longfilename, fAccess, fShare, IntPtr.Zero, fMode, fAttr, IntPtr.Zero);

            if (handle.IsInvalid)
            {
                handle = null;
                return System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            }
            else
            {
                return 0;
            }
        }
        public static int GetFilestream(string Longfilename, FileAccess fAccess, FileShare fShare, FileMode fMode, FileAttributes fAttr, out FileStream fs)
        {
            SafeFileHandle handle;
            int rc = CreateFile(Longfilename, fAccess, fShare, fMode, fAttr, out handle);
            if (rc == 0)
            {
                fs = new FileStream(handle, fAccess);
            }
            else
            {
                fs = null;
            }
            return rc;
        }
        public static bool CreatePath(string PathToCreate)
        {
            if (IsDirectory(PathToCreate))
            {
                return true;
            }
            if (Spi.Native.Win32.CreateDirectoryW(PathToCreate, IntPtr.Zero))
            {
                return true;
            }

            bool rc = false;
            if (Spi.Native.Win32.GetLastWin32Error() == Spi.Native.Win32.ERROR_PATH_NOT_FOUND)
            {
                // not found. try to create the parent dir.
                int LastPos = PathToCreate.LastIndexOf(System.IO.Path.DirectorySeparatorChar);
                if (LastPos != -1)
                {
                    if (rc = CreatePath(PathToCreate.Substring(0, LastPos)))
                    {
                        // parent dir exist/was created
                        rc = Spi.Native.Win32.CreateDirectoryW(PathToCreate, IntPtr.Zero);
                    }
                }
            }
            return rc;
        }
        public static DateTime ConvertFromFiletime(int HighTime, int LowTime)
        {
            unchecked
            {
                //ulong val = ((ulong)FindData.ftLastWriteTime.dwHighDateTime << 32) | (ulong)FindData.ftLastWriteTime.dwLowDateTime;
                ulong val = ((ulong)HighTime) << 32;
                val = val | (uint)LowTime;
                return DateTime.FromFileTime((long)val);
            }
        }
    }
}
