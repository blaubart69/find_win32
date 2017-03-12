using System;
using System.Text;
using System.Text.RegularExpressions;

namespace find
{
    class EnumDir
    {
        static readonly string[] FormatKeyWords = new string[] { "fullname" };

        public static void Run(string Dirname, Opts opts, ref Stats stats, ref bool CrtlC_pressed, Action<string> OutputHandler, Action<int, string> ErrorHandler, Action<string> ProgressCallback)
        {
            Spi.IO.StatusLineWriter StatusWriter = new Spi.IO.StatusLineWriter();

            string StartDirectoryFullname = System.IO.Path.GetFullPath(Dirname);

            foreach (var entry in Spi.IO.Directory.Entries(
                startDir: StartDirectoryFullname, 
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
                        ProgressCallback(StartDirectoryFullname);
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
                    HandleMatchedFile(entry, opts.FormatString, OutputHandler);
                }
            }
        }

        private static void HandleMatchedFile(Spi.IO.DirEntry entry, string FormatString, Action<string> OutputHandler)
        {
            string output;
            if (String.IsNullOrEmpty(FormatString))
            {
                String LastWriteTime;
                try
                {
                    LastWriteTime = entry.LastWriteTime.ToString("yyyy.MM.dd HH:mm:ss");
                }
                catch (ArgumentOutOfRangeException)
                {
                    LastWriteTime =                              "[out of .NET range]";
                }

                output = String.Format("{0}\t{1,12}\t{2}",
                    LastWriteTime,
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
    }
}
