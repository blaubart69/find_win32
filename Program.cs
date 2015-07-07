using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace find
{
    struct Stats
    {
        public UInt64 AllBytes;
        public UInt64 MatchedBytes;
        public UInt64 AllFiles;
        public UInt64 AllDirs;
        public UInt64 MatchedFiles;
    }
    class Opts
    {
        public IList<string> Dirs;
        public string Pattern;
        public string OutFilename;
        public bool show_help;
        public bool progress;
        public string FormatString;
    }
    class Program
    {
        static readonly string[] FormatKeyWords = new string[] { "fullname" };

        static int Main(string[] args)
        {
            const string ErrFilename = "find.err.txt";

            TextWriter ErrWriter = null;
            TextWriter OutWriter = null;

            Opts opts;
            if ( (opts=GetOpts(args)) == null)
            {
                return 8;
            }

            try
            {
                bool CrtlC_pressed = false;
                Stats stats = new Stats();

                Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs e) =>
                {
                    e.Cancel = true;    // means the program execution should go on
                    Console.Error.WriteLine("CTRL-C pressed. closing files. shutting down...");
                    CrtlC_pressed = true;
                };

                foreach (string dir in opts.Dirs)
                {
                    if ( CrtlC_pressed )
                    {
                        break;
                    }
                    EnumDir(dir, opts, ErrFilename, ref ErrWriter, ref OutWriter, ref stats, ref CrtlC_pressed);
                }
                WriteStats(stats);
            }
            finally
            {
                CloseWriters(ErrWriter, OutWriter);
            }
            return 0;
        }

        static void EnumDir(string Dirname, Opts opts, string ErrFilename, ref TextWriter ErrWriter, ref TextWriter OutWriter, ref Stats stats, ref bool CrtlC_pressed)
        {
            foreach (var entry in Spi.IO.Directory.Entries(Dirname, -1, null, null))
            {
                if (entry.LastError != 0)
                {
                    WriteToConsoleAndStream(ErrFilename, ref ErrWriter, Console.Error, "rc [{0}] dir [{1}]", entry.LastError, entry.Fullname);
                    continue;
                }

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

                bool PrintEntry = true;
                if (opts.Pattern != null)
                {
                    PrintEntry = Regex.IsMatch(entry.Filename, opts.Pattern);
                }

                if (PrintEntry)
                {
                    stats.MatchedBytes += entry.Filesize;
                    stats.MatchedFiles += 1;

                    if (String.IsNullOrEmpty(opts.FormatString))
                    {
                        WriteToConsoleAndStream(opts.OutFilename, ref OutWriter, Console.Out, "{0} {1,12} {2}",
                            entry.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            entry.Filesize,
                            entry.Fullname);
                    }
                    else
                    {
                        WriteToConsoleAndStream(opts.OutFilename, ref OutWriter, Console.Out, FormatOutput(opts.FormatString, entry));
                    }

                }
            }
        }
        static string FormatOutput(string Format, Spi.IO.Directory.DirEntry entry)
        {
            StringBuilder sb = new StringBuilder(Format);
            foreach ( string magic in FormatKeyWords )
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
        static void WriteToConsoleAndStream(string Filename, ref TextWriter FileWriter, TextWriter ConsoleWriter, string Format, params object[] args)
        {
            if ( !String.IsNullOrEmpty(Filename) )
            {
                if (FileWriter == null)
                {
                    FileWriter = new StreamWriter(
                        Filename,
                        false,      // append?
                        System.Text.Encoding.UTF8);
                }
                FileWriter.WriteLine(Format, args);
            }

            ConsoleWriter.WriteLine(Format, args);
        }
        static void WriteStats(Stats stats)
        {
            Console.Error.WriteLine("\nbytes seen [{0}] ({1}),  bytes matching files [{2}] ({3}), files seen [{4}], files matched [{5}], dirs seen [{6}]",
                    stats.AllBytes,
                    Spi.IO.Misc.GetPrettyFilesize(stats.AllBytes),
                    stats.MatchedBytes,
                    Spi.IO.Misc.GetPrettyFilesize(stats.MatchedBytes),
                    stats.AllFiles,
                    stats.MatchedFiles,
                    stats.AllDirs
                    );
        }
        static void ShowHelp(Mono.Options.OptionSet p)
        {
            Console.WriteLine("Usage: find [OPTIONS] [DIRECTORY]...");
            Console.WriteLine("lists all files in the given dir + subdirs");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }
        static Opts GetOpts(string[] args)
        {
            Opts opts = new Opts();
            var p = new Mono.Options.OptionSet() {
                { "r|rname=",   "regex applied to the filename",         v => opts.Pattern = v },
                { "o|out=",     "filename for result of files (UTF8)",   v => opts.OutFilename = v },
                { "p|progress", "prints out the directory currently scanned for a little progress indicator",   v => opts.progress = (v != null) },
                //{ "v", "increase debug message verbosity",                      v => { if (v != null) ++verbosity; } },
                { "h|help",     "show this message and exit",           v => opts.show_help = v != null },
                { "f|format=",  "format the output",                    v => opts.FormatString = v }
            };
            try
            {
                opts.Dirs = p.Parse(args);

                if (!String.IsNullOrEmpty(opts.Pattern))
                {
                    Console.Error.WriteLine("pattern parsed for rname [{0}]", opts.Pattern);
                }
                if (!String.IsNullOrEmpty(opts.FormatString))
                {
                    Console.Error.WriteLine("FormatString [{0}]", opts.FormatString);
                }

                if (opts.Dirs.Count() == 0)
                {
                    opts.Dirs.Add(Directory.GetCurrentDirectory());
                }
            }
            catch (Mono.Options.OptionException oex)
            {
                Console.WriteLine(oex.Message);
                return null;
            }
            if (opts.show_help)
            {
                ShowHelp(p);
                return null;
            }
            return opts;
        }
        static void CloseWriters(params TextWriter[] Writer)
        {
            foreach (TextWriter w in Writer)
            {
                if ( w != null )
                {
                    w.Close();
                }
            }
        }
    }
}
