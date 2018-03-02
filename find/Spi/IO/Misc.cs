using System;
using System.Text;
using System.Runtime.InteropServices.ComTypes;

namespace Spi.IO
{
    public class Misc
    {
        public static string NiceDuration(TimeSpan ts)
        {
            string res;
            if (ts.TotalHours >= 24)
            {
                res = String.Format("{0}d {1}h {2}m {3}s {4}ms", ts.Days, ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds);
            }
            else if (ts.TotalMinutes >= 60)
            {
                res = String.Format("{0}h {1}m {2}s {3}ms", ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds);
            }
            else if (ts.TotalSeconds >= 60)
            {
                res = String.Format("{0}m {1}s {2}ms", ts.Minutes, ts.Seconds, ts.Milliseconds);
            }
            else if (ts.TotalMilliseconds >= 1000)
            {
                res = String.Format("{0}s {1}ms", ts.Seconds, ts.Milliseconds);
            }
            else
            {
                res = String.Format("{0}ms", ts.Milliseconds);
            }
            return res;
        }
        public static string GetPrettyFilesize(ulong Filesize)
        {
            StringBuilder sb = new StringBuilder(50);
            Spi.Native.Win32.StrFormatByteSize((long)Filesize, sb, 50);
            return sb.ToString();
        }
        public static string GetPrettyFilesize(long Filesize)
        {
            return GetPrettyFilesize((ulong)Filesize);
        }

        public static string FiletimeToString(FILETIME filetime)
        {
            Native.Win32.SYSTEMTIME universalSystemtime;
            if ( ! Native.Win32.FileTimeToSystemTime(ref filetime, out universalSystemtime) )
            {
                throw new System.ComponentModel.Win32Exception();
            }
            /**
             *  universalTime:
             *  year:   30828
             *  month:  9
             *  day:    14
             *  hour:   2
             *  minute: 48
             *  second: 5
             *  milli:  477
            */

            Native.Win32.SYSTEMTIME localSystemtime;
            if ( ! Native.Win32.SystemTimeToTzSpecificLocalTime(IntPtr.Zero, ref universalSystemtime, out localSystemtime) )
            {
                throw new System.ComponentModel.Win32Exception();
            }

            return FormatSystemtime(localSystemtime);
        }
        public static string FormatSystemtime(Native.Win32.SYSTEMTIME sysTime)
        {
            return $"{sysTime.Year}.{sysTime.Month:00}.{sysTime.Day:00} {sysTime.Hour:00}:{sysTime.Minute:00}:{sysTime.Second:00}";
        }
        public static long TwoIntToLong(int high, int low)
        {
            ulong h = (ulong)high << 32;
            ulong l = (uint)low;

            ulong    u_result = h | l;
            long result = (long)u_result;

            return result; 
        }
        public static ulong TwoUIntsToULong(uint high, uint low)
        {
            ulong h = (ulong)high << 32;

            return h | (ulong)low;
        }
        public static long FiletimeToLong(FILETIME ft)
        {
            return TwoIntToLong(ft.dwHighDateTime, ft.dwLowDateTime);
        }
        public static bool IsDotOrDotDotDirectory(string Filename)
        {
            if (Filename[0] == '.')
            {
                if (Filename.Length == 1)
                {
                    return true;
                }
                if (Filename[1] == '.')
                {
                    if (Filename.Length == 2)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        public static bool IsDirectoryFlagSet(uint dwFileAttributes)
        {
            return (dwFileAttributes & 0x10) != 0;
        }
    }
}
