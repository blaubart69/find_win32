using System;
using System.IO;
using System.Text;
using System.Security;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Win32.SafeHandles;
using System.Runtime.ConstrainedExecution;

namespace Spi.Native
{
    public static class Win32
    {
        public const int ERROR_PATH_NOT_FOUND = 0x00000003;
        public const int ERROR_INVALID_PARAMETER = 0x00000057;

        [Flags]
        public enum EFileAccess : uint
        {
            GENERIC_READ = 0x80000000,
            GENERIC_WRITE = 0x40000000,
            GENERIC_READ_WRITE = GENERIC_READ | GENERIC_WRITE
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEMTIME
        {
            [MarshalAs(UnmanagedType.U2)]
            public short Year;
            [MarshalAs(UnmanagedType.U2)]
            public short Month;
            [MarshalAs(UnmanagedType.U2)]
            public short DayOfWeek;
            [MarshalAs(UnmanagedType.U2)]
            public short Day;
            [MarshalAs(UnmanagedType.U2)]
            public short Hour;
            [MarshalAs(UnmanagedType.U2)]
            public short Minute;
            [MarshalAs(UnmanagedType.U2)]
            public short Second;
            [MarshalAs(UnmanagedType.U2)]
            public short Milliseconds;

            public SYSTEMTIME(DateTime dt)
            {
                dt = dt.ToUniversalTime();  // SetSystemTime expects the SYSTEMTIME in UTC
                Year = (short)dt.Year;
                Month = (short)dt.Month;
                DayOfWeek = (short)dt.DayOfWeek;
                Day = (short)dt.Day;
                Hour = (short)dt.Hour;
                Minute = (short)dt.Minute;
                Second = (short)dt.Second;
                Milliseconds = (short)dt.Millisecond;
            }
        }
        /*
        [StructLayout(LayoutKind.Sequential)]
        public struct FILETIME
        {
            public FILETIME(uint high, uint low)
            {
                dwLowDateTime = low;
                dwHighDateTime = high;
            }
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }
        */
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            private uint nFileSizeHigh;
            private uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;

            public ulong Filesize { get { return Spi.IO.Misc.TwoUIntsToULong(nFileSizeHigh, nFileSizeLow); } }
        }

        /*
        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFileHandle FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);
        */

        //public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFindHandle FindFirstFile(string lpFileName, ref WIN32_FIND_DATA lpFindFileData);
        //public static extern string FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool FindNextFile(SafeHandle hFindFile, ref WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool FindClose(SafeHandle hFindFile);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern uint GetFileAttributes(string lpFileName);

        [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
        public static extern long StrFormatByteSize(
                long fileSize
                , [MarshalAs(UnmanagedType.LPTStr)] StringBuilder buffer
                , int bufferSize);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFileHandle CreateFileW(
             [MarshalAs(UnmanagedType.LPWStr)]  string          filename,
                                                EFileAccess     access,
             [MarshalAs(UnmanagedType.U4)]      FileShare       share,
                                                IntPtr          securityAttributes,
             [MarshalAs(UnmanagedType.U4)]      FileMode        creationDisposition,
             [MarshalAs(UnmanagedType.U4)]      FileAttributes  flagsAndAttributes,
             IntPtr templateFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CreateDirectoryW(string lpPathName,IntPtr lpSecurityAttributes);

        public static bool SetFileTime(IntPtr hFile, System.Runtime.InteropServices.ComTypes.FILETIME lpCreationTime, System.Runtime.InteropServices.ComTypes.FILETIME lpLastAccessTime, System.Runtime.InteropServices.ComTypes.FILETIME lpLastWriteTime)
        {
            long c = Spi.IO.Misc.FiletimeToLong(lpCreationTime);
            long a = Spi.IO.Misc.FiletimeToLong(lpLastAccessTime);
            long w = Spi.IO.Misc.FiletimeToLong(lpLastWriteTime);

            return
                SetFileTime(
                    hFile: hFile,
                    lpCreationTime:     ref c,
                    lpLastAccessTime:   ref a,
                    lpLastWriteTime:    ref w);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetFileTime(IntPtr hFile, ref long lpCreationTime, ref long lpLastAccessTime, ref long lpLastWriteTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FileTimeToSystemTime(ref System.Runtime.InteropServices.ComTypes.FILETIME ft, out SYSTEMTIME st);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FileTimeToLocalFileTime(ref System.Runtime.InteropServices.ComTypes.FILETIME ftin, ref System.Runtime.InteropServices.ComTypes.FILETIME ftout);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemTimeToTzSpecificLocalTime(IntPtr lpTimeZoneInformation, [In] ref SYSTEMTIME lpUniversalTime, out SYSTEMTIME lpLocalTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemTimeToFileTime(ref SYSTEMTIME st, out System.Runtime.InteropServices.ComTypes.FILETIME ft);

        

        /*
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetFileTime(IntPtr hFile, ref long lpCreationTime, ref long lpLastAccessTime, ref long lpLastWriteTime);
        */
        /***
         * as we use SafefileHandles these days, we don't really need CloseHandle() any more
         * 
        [DllImport("kernel32.dll", SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr hObject);
        */
    }
}
