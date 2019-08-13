using System;
using System.Text;
using System.IO;

using Spi;
using Spi.IO;
using Spi.Native;

namespace find
{
    public class FormatOutput
    {
        static readonly string[] FormatKeyWords = new string[] { "fullname", "filename" };

        //public static void PrintEntry(string rootDir, string dir, ref Win32.WIN32_FIND_DATA find_data, string FormatString, ConsoleAndFileWriter writer, Action<int, string> ErrorHandler, bool tsvFormat)
        public static void PrintEntry(string rootDir, string dir, ref Win32.WIN32_FIND_DATA find_data, ConsoleAndFileWriter writer, Action<int, string> ErrorHandler, bool tsvFormat, bool FullFormat, bool PrependRootDir)
        {
            if ( writer == null )
            {
                return;
            }

            if ( tsvFormat )
            {
                GetFirstDirAndRest(dir, find_data.cFileName, out string baseDir, out string rest);

                writer.WriteLine("{0}\t{1}\t{2}\t{3}\t{4}\t{5}"
                    , GetBasename(dir, find_data.cFileName)
                    , find_data.Filesize
                    , find_data.dwFileAttributes
                    , Misc.FiletimeToLong(find_data.ftCreationTime)
                    , Misc.FiletimeToLong(find_data.ftLastWriteTime)
                    , Misc.FiletimeToLong(find_data.ftLastAccessTime));
                    //, GetBasename(dir, find_data.cFileName));
            }
            else if (FullFormat)
            {
                String LastWriteTime = FormatFiletime(find_data.ftLastWriteTime, ErrorHandler);

                writer.WriteLine("{0}\t{1,12}\t{2}\t{3}"
                    , LastWriteTime
                    , find_data.Filesize
                    , GetAttributesField(find_data.dwFileAttributes)
                    , GetFullname(rootDir, dir, find_data.cFileName));
            }
            else if (PrependRootDir)
            {
                writer.WriteLine("{0}", GetFullname(rootDir, dir, find_data.cFileName));
            }
            else
            {
                writer.WriteLine("{0}", GetBasename(dir, find_data.cFileName));
            }
        }
        static string GetAttributesField(uint dwFileAttributes)
        {
            StringBuilder sb = new StringBuilder(5);
            sb.Length = 5;
            sb[0] = ((dwFileAttributes & (uint)System.IO.FileAttributes.Archive)   != 0 ) ? 'A' : '-';
            sb[1] = ((dwFileAttributes & (uint)System.IO.FileAttributes.System)    != 0 ) ? 'S' : '-';
            sb[2] = ((dwFileAttributes & (uint)System.IO.FileAttributes.Hidden)    != 0 ) ? 'H' : '-';
            sb[3] = ((dwFileAttributes & (uint)System.IO.FileAttributes.ReadOnly)  != 0 ) ? 'R' : '-';
            sb[4] = ((dwFileAttributes & (uint)System.IO.FileAttributes.Directory) != 0 ) ? 'D' : '-';

            return sb.ToString();
        }
        static void GetFirstDirAndRest(string dir, string filename, out string baseDir, out string rest)
        {
            int firstBackslash = -1;
            if ( String.IsNullOrEmpty(dir) )
            {
                baseDir = string.Empty;
                rest = filename;
            }
            else
            {
                firstBackslash = dir.IndexOf('\\');
                if ( firstBackslash == -1)
                {
                    baseDir = dir;
                    rest = filename;
                }
                else
                {
                    baseDir = dir.Substring(0, firstBackslash);
                    rest = dir.Substring(firstBackslash + 1);
                }
            }
        }
        static string GetBasename(string dir, string filename)
        {
            string tmpString;
            if (String.IsNullOrEmpty(dir))
            {
                tmpString = filename;
            }
            else
            {
                tmpString = Path.Combine(dir, filename);
            }
            return tmpString;
        }
        static string GetFullname(string rootDir, string dir, string filename)
        {
            string tmpString;
            if ( String.IsNullOrEmpty(dir) )
            {
                tmpString = Path.Combine(rootDir, filename);
            }
            else
            {
                tmpString = Path.Combine(rootDir, dir);
                tmpString = Path.Combine(tmpString, filename);
            }
            return tmpString;
        }
        static string FormatLine(string Format, string rootDir, string dir, Win32.WIN32_FIND_DATA find_data)
        {
            StringBuilder sb = new StringBuilder(Format);
            foreach (string magic in FormatKeyWords)
            {
                string ReplaceString = null;
                switch (magic)
                {
                    case "fullname": ReplaceString = GetFullname(rootDir, dir, find_data.cFileName); break;
                    case "filename": ReplaceString = find_data.cFileName; break;
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
