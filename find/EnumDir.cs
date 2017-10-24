using System;
using System.Text;
using System.Text.RegularExpressions;
using Spi.Native;

namespace find
{
    class EnumDir
    {
        static readonly string[] FormatKeyWords = new string[] { "fullname" };

        public static void Run(string Dirname, Opts opts, ref Stats stats, ref bool CrtlC_pressed, Action<string> OutputHandler, Action<int, string> ErrorHandler, Action<string> ProgressCallback)
        {
            Spi.IO.StatusLineWriter StatusWriter = new Spi.IO.StatusLineWriter();

            foreach (var entry in Spi.IO.Directory.Entries(
                startDir: Dirname, 
                DirErrorHandler: ErrorHandler,
                FollowJunctions: opts.FollowJunctions,
                EnterDir: null,
                maxDepth: opts.Depth))
            {
                if (CrtlC_pressed)
                {
                    break;
                }

                if (entry.isDirectory)
                {
                    stats.AllDirs += 1;
                    if (ProgressCallback != null)
                    {
                        ProgressCallback(entry.Dirname);
                    }
                    continue;
                }

                stats.AllBytes += entry.Filesize;
                stats.AllFiles += 1;

                bool PrintEntry = (opts.Pattern == null) ? true : Regex.IsMatch(entry.Name, opts.Pattern);
                if (PrintEntry)
                {
                    stats.MatchedBytes += entry.Filesize;
                    stats.MatchedFiles += 1;
                    HandleMatchedFile(entry, opts.FormatString, OutputHandler, ErrorHandler);
                }
            }
        }

        private static void HandleMatchedFile(Spi.IO.DirEntry entry, string FormatString, Action<string> OutputHandler, Action<int, string> ErrorHandler)
        {
            string output;
            if (String.IsNullOrEmpty(FormatString))
            {
                String LastWriteTime = FormatFiletime(entry.LastWriteTime, ErrorHandler);

                output = String.Format("{0}\t{1,12}\t{2}",
                    LastWriteTime,
                    entry.Filesize,
                    entry.Fullname);
            }
            else
            {
                output = FormatOutput(FormatString, entry);
            }
            OutputHandler?.Invoke(output);
        }
        static string FormatOutput(string Format, Spi.IO.DirEntry entry)
        {
            StringBuilder sb = new StringBuilder(Format);
            foreach (string magic in FormatKeyWords)
            {
                string ReplaceString = null;
                switch (magic)
                {
                    case "fullname": ReplaceString = entry.Fullname; break;
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
            if ( ! Win32.FileTimeToSystemTime(ref filetime, out universalSystemtime) )
            {
                long longFiletime = Spi.IO.Misc.TwoIntToLong(filetime.dwHighDateTime, filetime.dwLowDateTime);

                ErrorHandler?.Invoke(System.Runtime.InteropServices.Marshal.GetLastWin32Error(),
                    String.Format("error at FileTimeToSystemTime(). input parameter filetime {0:X}", longFiletime));
                return longFiletime.ToString();
            }
            
            Win32.SYSTEMTIME localSystemtime;
            if ( ! Win32.SystemTimeToTzSpecificLocalTime(IntPtr.Zero, ref universalSystemtime, out localSystemtime) )
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
