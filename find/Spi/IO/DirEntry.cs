using System;
using System.Runtime.InteropServices.ComTypes;

namespace Spi.IO
{
    public struct DirEntry
    {
        private readonly string _BaseDirName;
        private readonly int _RootDirLen;
        private readonly Spi.Native.Win32.WIN32_FIND_DATA _FindData;

        public DirEntry(string BaseDirName, Spi.Native.Win32.WIN32_FIND_DATA FindData, int RootDirLength)
        {
            this._BaseDirName = BaseDirName;
            this._RootDirLen = RootDirLength;
            this._FindData = FindData;
        }

        public bool IsDirectory { get { return Spi.IO.Misc.IsDirectoryFlagSet(_FindData.dwFileAttributes); } }
        public bool IsFile { get { return !IsDirectory; } }
        public string Name { get { return _FindData.cFileName; } }
        public UInt64 Filesize { get { return (((UInt64)_FindData.nFileSizeHigh) << 32) | (UInt64)_FindData.nFileSizeLow; } }
        
        public string NameFromRootDir
        {
            get
            {
                return GetFilenameSinceBaseDir(_BaseDirName, _RootDirLen, _FindData.cFileName);
            }
        }
        
        public string Fullname
        {
            get
            {
                return _BaseDirName + System.IO.Path.DirectorySeparatorChar + _FindData.cFileName;
            }
        }
        public FILETIME LastWriteTime
        {
            get
            {
                //return Spi.IO.Long.ConvertFromFiletime(FindData.ftLastWriteTime.dwHighDateTime, FindData.ftLastWriteTime.dwLowDateTime);
                return _FindData.ftLastWriteTime;
            }
        }
        public long LastWriteTimeUtcLong
        {
            get
            {
                return Misc.FiletimeToLong(_FindData.ftLastWriteTime);
            }
        }
        public long LastAccessTimeUtcLong
        {
            get
            {
                return Misc.FiletimeToLong(_FindData.ftLastAccessTime);
            }
        }
        public long CreationTimeTimeUtcLong
        {
            get
            {
                return Misc.FiletimeToLong(_FindData.ftCreationTime);
            }
        }
        public uint Attributes
        {
            get
            {
                return _FindData.dwFileAttributes;
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
    }
}
