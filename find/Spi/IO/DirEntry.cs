using System;
using System.Text;
using System.Runtime.InteropServices.ComTypes;

namespace Spi.IO
{
    public struct DirEntry
    {
        private readonly string DirName;
        //private readonly int BaseDirLen;
        private readonly Spi.Native.Win32.WIN32_FIND_DATA FindData;

        public bool IsDirectory { get { return Spi.IO.Misc.IsDirectoryFlagSet(FindData.dwFileAttributes); } }
        public bool IsFile { get { return !IsDirectory; } }
        public string Name { get { return FindData.cFileName; } }
        public UInt64 Filesize { get { return (((UInt64)FindData.nFileSizeHigh) << 32) | (UInt64)FindData.nFileSizeLow; } }
        public string Dirname { get { return Dirname; } }
        /*
        public string DirAndFilenameFromStartDir { get { return GetFilenameSinceBaseDir(Dirname, BaseDirLen, FindData.cFileName); } }
        */
        public string Fullname
        {
            get
            {
                return Dirname + System.IO.Path.DirectorySeparatorChar + FindData.cFileName;
            }
        }
        public FILETIME LastWriteTime
        {
            get
            {
                //return Spi.IO.Long.ConvertFromFiletime(FindData.ftLastWriteTime.dwHighDateTime, FindData.ftLastWriteTime.dwLowDateTime);
                return FindData.ftLastWriteTime;
            }
        }
        public long LastWriteTimeUtcLong
        {
            get
            {
                return Misc.TwoIntToLong(
                    FindData.ftLastWriteTime.dwHighDateTime, 
                    FindData.ftLastWriteTime.dwLowDateTime);
            }
        }
        public DirEntry(string BaseDirName, Spi.Native.Win32.WIN32_FIND_DATA FindData)
        {
            this.DirName = BaseDirName;
            //this.BaseDirLen = BaseDirLen;
            this.FindData = FindData;
        }
        /*
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
        */
    }
}
