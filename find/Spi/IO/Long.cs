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
        /*
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
            if (System.Runtime.InteropServices.Marshal.GetLastWin32Error() == Spi.Native.Win32.ERROR_PATH_NOT_FOUND)
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
        */
    }
}
