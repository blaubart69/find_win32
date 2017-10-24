using System;
using System.Text;
using System.Runtime.InteropServices.ComTypes;

namespace Spi.IO
{
    public class Misc
    {
        public static string GetPrettyFilesize(ulong Filesize)
        {
            StringBuilder sb = new StringBuilder(50);
            Spi.Native.Win32.StrFormatByteSize((long)Filesize, sb, 50);
            return sb.ToString();
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
        public static DateTime ConvertFromFiletime(int HighTime, int LowTime)
        {
            long val = TwoIntToLong(HighTime, LowTime);
            return DateTime.FromFileTime(val);
        }
        public static long TwoIntToLong(int high, int low)
        {
            ulong h = (ulong)high << 32;
            ulong l = (uint)low;

            ulong    u_result = h | l;
            long result = (long)u_result;

            return result; 
        }
    }
}
