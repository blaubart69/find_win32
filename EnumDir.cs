using System;
using System.Text;
using System.Text.RegularExpressions;

namespace find
{
    class EnumDir
    {
        static readonly string[] FormatKeyWords = new string[] { "fullname" };

        public static void Run(string Dirname, Opts opts, ref Stats stats, ref bool CrtlC_pressed, Action<string> OutputHandler, Action<int, string> ErrorHandler)
        {
            foreach (var entry in Spi.IO.Directory.Entries(Dirname, ErrorHandler, opts.FollowJunctions))
            {
                if (CrtlC_pressed)
                {
                    break;
                }

                if (entry.isDirectory)
                {
                    stats.AllDirs += 1;
                    if (opts.progress)
                    {
                        Console.Error.Write("[{0}]\r", entry.Dirname);
                    }
                    continue;
                }

                stats.AllBytes += entry.Filesize;
                stats.AllFiles += 1;

                bool PrintEntry = (opts.Pattern == null) ? true : Regex.IsMatch(entry.Filename, opts.Pattern);
                if (PrintEntry)
                {
                    stats.MatchedBytes += entry.Filesize;
                    stats.MatchedFiles += 1;
                    HandleMatchedFile(entry, opts.FormatString, OutputHandler);
                }
            }
        }

        private static void HandleMatchedFile(Spi.IO.Directory.DirEntry entry, string FormatString, Action<string> OutputHandler)
        {
            string output;
            if (String.IsNullOrEmpty(FormatString))
            {
                output = String.Format("{0} {1,12} {2}",
                    entry.LastWriteTime.ToString("yyyy.MM.dd HH:mm:ss"),
                    entry.Filesize,
                    entry.Fullname);
            }
            else
            {
                output = FormatOutput(FormatString, entry);
            }
            if (OutputHandler != null)
            {
                OutputHandler(output);
            }
        }
        static string FormatOutput(string Format, Spi.IO.Directory.DirEntry entry)
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

    }
}
