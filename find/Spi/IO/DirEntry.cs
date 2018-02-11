using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices.ComTypes;

namespace Spi.IO
{
    public sealed class DirEntry
    {
        public readonly Spi.Native.Win32.WIN32_FIND_DATA _FindData;
        public readonly string dirSinceRootDir;

        public DirEntry(string dirSinceRootDir, Spi.Native.Win32.WIN32_FIND_DATA FindData)
        {
            this.dirSinceRootDir = dirSinceRootDir;
            this._FindData = FindData;
        }
        public bool IsDirectory { get { return Spi.IO.Misc.IsDirectoryFlagSet(_FindData.dwFileAttributes); } }
        public bool IsFile { get { return !IsDirectory; } }
        public string Name { get { return _FindData.cFileName; } }
        public UInt64 Filesize { get { return (((UInt64)_FindData.nFileSizeHigh) << 32) | (UInt64)_FindData.nFileSizeLow; } }
        public FILETIME LastWriteTime
        {
            get
            {
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
        public static UInt64 GetFileSize(Spi.Native.Win32.WIN32_FIND_DATA find_data)
        {
            return Spi.IO.Misc.TwoUIntsToULong(high: find_data.nFileSizeHigh, low: find_data.nFileSizeLow);
        }
        public static long FiletimeToLong(FILETIME time)
        {
            return Misc.TwoIntToLong( high: time.dwHighDateTime, low: time.dwLowDateTime);
        }
    }
}
