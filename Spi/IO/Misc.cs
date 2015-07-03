using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Spi.IO
{
    public class Misc
    {
        public static string GetPrettyFilesize(ulong Filesize)
        {
            StringBuilder sb = new StringBuilder(50);
            Win32.StrFormatByteSize((long)Filesize, sb, 50);
            return sb.ToString();
        }
        public static bool IsDirectory(string dir)
        {
            uint rc = Win32.GetFileAttributes(dir);

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
        public static string GetLongFilenameNotation(string FilenameShort)
        {
            if (FilenameShort.Length >= 2 && FilenameShort[1] == ':')
            {
                return @"\\?\" + FilenameShort;
            }
            else if (FilenameShort.StartsWith(@"\\") && !FilenameShort.StartsWith(@"\\?\") )
            {
                return @"\\?\UNC\" + FilenameShort.Remove(0,2);
            }
            return FilenameShort;
        }
        public static int OpenLongfilename(string Longfilename, FileAccess fAccess, FileShare fShare, FileMode fMode, out FileStream fStream)
        {
            Microsoft.Win32.SafeHandles.SafeFileHandle handle =
                Spi.Win32.CreateFileW(Longfilename, fAccess, fShare, IntPtr.Zero, fMode, 0, IntPtr.Zero);

            if (handle.IsInvalid)
            {
                fStream = null;
                return System.Runtime.InteropServices.Marshal.GetLastWin32Error();
            }
            else
            {
                fStream = new FileStream(handle, fAccess);
                return 0;
            }
        }
        public static bool CreatePath(string PathToCreate)
        {
            if (IsDirectory(PathToCreate))
            {
                return true;
            }
            if (Win32.CreateDirectoryW(PathToCreate, IntPtr.Zero))
            {
                return true;
            }

            bool rc = false;
            if (Spi.Win32.GetLastWin32Error() == Spi.Win32.ERROR_PATH_NOT_FOUND)
            {
                // not found. try to create the parent dir.
                int LastPos = PathToCreate.LastIndexOf(System.IO.Path.DirectorySeparatorChar);
                if (LastPos != -1)
                {
                    if (rc = CreatePath(PathToCreate.Substring(0, LastPos)))
                    {
                        // parent dir exist/was created
                        rc = Win32.CreateDirectoryW(PathToCreate, IntPtr.Zero);
                    }
                }
            }
            return rc;
        }
        /// <summary>
        /// delete all the dirs and files in the given directory
        /// except the dirs and files specified in the two arrays
        /// </summary>
        /// <param name="dir">Directory to clean</param>
        /// <param name="ExcludeDirs">List of directories which should not be deleted</param>
        /// <param name="ExcludeFiles">List of files which should not be deleted</param>
        public static void EmptyDirectory(string dir, ICollection<string> ExcludeDirs, ICollection<string> ExcludeFiles, Action<string> DebugCallBack)
        {
            //
            // delete all directories
            //
            foreach (string Dir2Del in System.IO.Directory.GetDirectories(dir))
            {
                if (DebugCallBack != null) DebugCallBack(String.Format("dir enumerated [{0}]", Dir2Del));
                string DirOnlyName = Path.GetFileName(Dir2Del);
                
                if (!Spi.StringTools.Contains_OrdinalIgnoreCase(ExcludeDirs, DirOnlyName))
                {
                    if (DebugCallBack != null) DebugCallBack(String.Format("deleting dir [{0}]",DirOnlyName));
                    System.IO.Directory.Delete(Dir2Del, true); // true = delete recurse    
                }
            }
            //
            // delete all files
            //
            foreach (string FileToDel in System.IO.Directory.GetFiles(dir))
            {
                if (DebugCallBack != null) DebugCallBack(String.Format("file enumerated [{0}]", FileToDel));
                if (!Spi.StringTools.Contains_OrdinalIgnoreCase(ExcludeFiles, Path.GetFileName(FileToDel)))
                {
                    File.SetAttributes(FileToDel, FileAttributes.Normal);
                    if (DebugCallBack != null) DebugCallBack(String.Format("deleting file [{0}]", FileToDel));
                    File.Delete(FileToDel);
                }
            }
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
