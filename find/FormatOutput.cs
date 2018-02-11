using System;
using System.Text;

using Spi.Native;

namespace find
{
    public class FormatOutput
    {
        static readonly string[] FormatKeyWords = new string[] { "fullname" };

        public static void HandleMatchedFile(string rootDir, string dir, Win32.WIN32_FIND_DATA find_data, string FormatString, Spi.ConsoleAndFileWriter writer, Action<int, string> ErrorHandler, bool tsvFormat)
        {
            if ( writer == null )
            {
                return;
            }

            if ( tsvFormat )
            {
                writer.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\\{6}"
                    , Spi.IO.DirEntry.GetFileSize(find_data)
                    , find_data.dwFileAttributes
                    , Spi.IO.DirEntry.FiletimeToLong(find_data.ftCreationTime)
                    , Spi.IO.DirEntry.FiletimeToLong(find_data.ftLastWriteTime)
                    , Spi.IO.DirEntry.FiletimeToLong(find_data.ftLastAccessTime)
                    , rootDir
                    , String.IsNullOrEmpty(dir) ? find_data.cFileName : System.IO.Path.Combine(dir, find_data.cFileName));
            }
            else if (! String.IsNullOrEmpty(FormatString))
            {
                writer.WriteLine(FormatLine(FormatString, rootDir, dir, find_data));
            }
            else
            {
                String LastWriteTime = FormatFiletime(find_data.ftLastWriteTime, ErrorHandler);

                writer.WriteLine("{0}\t{1,12}\t{2}\\{3}",
                    LastWriteTime,
                    Spi.IO.DirEntry.GetFileSize(find_data),
                    rootDir,
                    String.IsNullOrEmpty(dir) ? find_data.cFileName : System.IO.Path.Combine(dir, find_data.cFileName));
            }
        }
        static string FormatLine(string Format, string rootDir, string dir, Win32.WIN32_FIND_DATA find_data)
        {
            StringBuilder sb = new StringBuilder(Format);
            foreach (string magic in FormatKeyWords)
            {
                string ReplaceString = null;
                switch (magic)
                {
                    case "fullname": ReplaceString = rootDir + dir + find_data.cFileName; break;
                }
                if (!String.IsNullOrEmpty(ReplaceString))
                {
                    sb.Replace("%" + magic + "%", ReplaceString);
                }
            }

            return sb.ToString();
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
                long longFiletime = Spi.IO.Misc.TwoIntToLong(filetime.dwHighDateTime, filetime.dwLowDateTime);

                ErrorHandler?.Invoke(System.Runtime.InteropServices.Marshal.GetLastWin32Error(),
                    String.Format("error at FileTimeToSystemTime(). input parameter filetime {0:X}", longFiletime));
                return longFiletime.ToString();
            }

            Win32.SYSTEMTIME localSystemtime;
            if (!Win32.SystemTimeToTzSpecificLocalTime(IntPtr.Zero, ref universalSystemtime, out localSystemtime))
            {
                string UTCSystime = Spi.IO.Misc.FormatSystemtime(universalSystemtime);

                ErrorHandler?.Invoke(System.Runtime.InteropServices.Marshal.GetLastWin32Error(),
                    String.Format("error at SystemTimeToTzSpecificLocalTime() for SYSTEMTIME [{0}]", UTCSystime));
                return "(UTC) " + UTCSystime;
            }

            return Spi.IO.Misc.FormatSystemtime(localSystemtime);
        }

    }
}
