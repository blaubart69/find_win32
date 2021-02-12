using System;
using System.Text;

using Spi;
using Spi.IO;
using Spi.Native;

namespace find
{
    public class Print
    {
        [ThreadStatic]
        static StringBuilder sb;

        public static void PrintEntry(
            string rootDir, string dir, ref Win32.WIN32_FIND_DATA find_data
            , ConsoleAndFileWriter writer, Action<int, string> ErrorHandler
            , string separator, PrintFormat format, bool PrependRootDir)
        {
            if ( writer == null )
            {
                return;
            }

            if ( sb==null)
            {
                sb = new StringBuilder();
            }
            sb.Clear();

            if (format == PrintFormat.FILENAME_ONLY)
            {
                AppendFilename(PrependRootDir, rootDir, dir, find_data.cFileName, separator, ref sb);
            }
            else if (format == PrintFormat.LONG)
            {
                sb.Append(FormatFiletime(find_data.ftLastWriteTime, ErrorHandler));     sb.Append(separator);
                sb.AppendFormat("{0,12}", find_data.Filesize);                          sb.Append(separator);
                AppendAttributes(find_data.dwFileAttributes, ref sb);                   sb.Append(separator);
                AppendFilename(PrependRootDir, rootDir, dir, find_data.cFileName, separator, ref sb);
            }
            else if (format == PrintFormat.FULL)
            {
                AppendAttributes(find_data.dwFileAttributes, ref sb);                sb.Append(separator);
                sb.Append(FormatFiletime(find_data.ftCreationTime,   ErrorHandler)); sb.Append(separator);
                sb.Append(FormatFiletime(find_data.ftLastWriteTime,  ErrorHandler)); sb.Append(separator);
                sb.Append(FormatFiletime(find_data.ftLastAccessTime, ErrorHandler)); sb.Append(separator);
                sb.AppendFormat("{0,12}", find_data.Filesize);                       sb.Append(separator);
                AppendFilename(PrependRootDir, rootDir, dir, find_data.cFileName, separator, ref sb);
            }
            else if ( format == PrintFormat.MACHINE)
            {
                /*
                 typedef struct _WIN32_FIND_DATAW {
                   DWORD dwFileAttributes;
                   FILETIME ftCreationTime;
                   FILETIME ftLastAccessTime;
                   FILETIME ftLastWriteTime;
                   DWORD nFileSizeHigh;
                   DWORD nFileSizeLow;
                   DWORD dwReserved0;
                   DWORD dwReserved1;
                   _Field_z_ WCHAR  cFileName[ MAX_PATH ];
                   _Field_z_ WCHAR  cAlternateFileName[ 14 ];
               } WIN32_FIND_DATAW, *PWIN32_FIND_DATAW, *LPWIN32_FIND_DATAW;
               */
                sb.AppendFormat("{1:X8}{0}{2:X8}{3:X8}{0}{4:X8}{5:X8}{0}{6:X8}{7:X8}{0}{8}{0}",
                    separator
                    , (UInt32)find_data.dwFileAttributes
                    , (UInt32)find_data.ftCreationTime.dwHighDateTime
                    , (UInt32)find_data.ftCreationTime.dwLowDateTime
                    , (UInt32)find_data.ftLastAccessTime.dwHighDateTime
                    , (UInt32)find_data.ftLastAccessTime.dwLowDateTime
                    , (UInt32)find_data.ftLastAccessTime.dwHighDateTime
                    , (UInt32)find_data.ftLastAccessTime.dwLowDateTime
                    , find_data.Filesize);
                AppendFilename(PrependRootDir, rootDir, dir, find_data.cFileName, separator, ref sb);
            }

            writer.WriteLine(sb.ToString());
        }
        static void AppendAttributes(uint dwFileAttributes, ref StringBuilder sb)
        {
            sb.Append(((dwFileAttributes & (uint)System.IO.FileAttributes.Archive)    != 0 ) ? 'A' : '-');
            sb.Append(((dwFileAttributes & (uint)System.IO.FileAttributes.System)     != 0 ) ? 'S' : '-');
            sb.Append(((dwFileAttributes & (uint)System.IO.FileAttributes.Hidden)     != 0 ) ? 'H' : '-');
            sb.Append(((dwFileAttributes & (uint)System.IO.FileAttributes.ReadOnly)   != 0 ) ? 'R' : '-');
            sb.Append(((dwFileAttributes & (uint)System.IO.FileAttributes.Directory)  != 0 ) ? 'D' : '-');
            sb.Append(((dwFileAttributes & (uint)System.IO.FileAttributes.Compressed) != 0 ) ? 'C' : '-');
            sb.Append(((dwFileAttributes & (uint)System.IO.FileAttributes.Temporary)  != 0 ) ? 'T' : '-');

        }
        static void AppendFilename(bool PrependRootDir, string rootDir, string dir, string filename, string separator, ref StringBuilder sb)
        {
            bool quote = !"\t".Equals(separator);
            
            if (quote)
            {
                sb.Append("\"");
            }

            if (PrependRootDir)
            {
                sb.Append(rootDir);
            }
            sb.Append(dir);
            sb.Append(filename);

            if (quote)
            {
                sb.Append("\"");
            }
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
        private static string FormatFiletime(System.Runtime.InteropServices.ComTypes.FILETIME filetime, Action<int, string> ErrorHandler)
        {
            Win32.SYSTEMTIME universalSystemtime;
            if (!Win32.FileTimeToSystemTime(ref filetime, out universalSystemtime))
            {
                long longFiletime = Misc.TwoIntToLong(filetime.dwHighDateTime, filetime.dwLowDateTime);

                ErrorHandler?.Invoke(System.Runtime.InteropServices.Marshal.GetLastWin32Error(),
                    String.Format("error at FileTimeToSystemTime(). input parameter filetime {0:X}", longFiletime));
                return longFiletime.ToString();
            }

            Win32.SYSTEMTIME localSystemtime;
            if (!Win32.SystemTimeToTzSpecificLocalTime(IntPtr.Zero, ref universalSystemtime, out localSystemtime))
            {
                string UTCSystime = Misc.FormatSystemtime(universalSystemtime);

                ErrorHandler?.Invoke(System.Runtime.InteropServices.Marshal.GetLastWin32Error(),
                    String.Format("error at SystemTimeToTzSpecificLocalTime() for SYSTEMTIME [{0}]", UTCSystime));
                return "(UTC) " + UTCSystime;
            }

            return Misc.FormatSystemtime(localSystemtime);
        }
    }
}
