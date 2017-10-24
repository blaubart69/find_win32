using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

using Spi;

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
        public IEnumerable<string> Dirs;
        public string Pattern;
        public string OutFilename;
        public bool show_help;
        public bool progress;
        public string FormatString;
        public bool FollowJunctions = false;
        public string FilenameWithDirs;
        public int Depth = -1;
    }
    class Program
    {
        static readonly string ErrFilename = Path.Combine(Environment.GetEnvironmentVariable("temp"), "find.err.txt");

        static int Main(string[] args)
        {
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

                using (var ErrWriter = new ConsoleAndFileWriter(Console.Error, ErrFilename))
                using (var OutWriter = new ConsoleAndFileWriter(Console.Out, opts.OutFilename))
                {
                    Spi.IO.StatusLineWriter StatusWriter = new Spi.IO.StatusLineWriter();
                    foreach (string dir in opts.Dirs)
                    {
                        Console.Error.WriteLine("scanning [{0}]", dir);
                        EnumDir.Run(Dirname: dir, opts: opts, stats: ref stats, CrtlC_pressed: ref CrtlC_pressed,
                            OutputHandler:    (filenamefound) => OutWriter.WriteLine(filenamefound),
                            ErrorHandler:     (rc, ErrDir)    => ErrWriter.WriteLine("rc {0}\t{1}", rc, ErrDir),
                            ProgressCallback: (dirname)       => { if (opts.progress) { StatusWriter.WriteWithDots(dirname); } });
                    }
                    if ( opts.progress )
                    {
                        StatusWriter.WriteWithDots("");
                    }
                    if (ErrWriter.hasDataWritten())
                    {
                        Console.Error.WriteLine("\nerrors were logged to file [{0}]\n", ErrFilename);
                    }
                }
                WriteStats(stats);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
                return 12;
            }
            return 0;
        }
        static void WriteStats(Stats stats)
        {
            Console.Error.WriteLine(
                  "dirs/files     {0}/{1} ({2})\n"
                + "files matched  {3} ({4})",
                    stats.AllDirs, stats.AllFiles, Spi.IO.Misc.GetPrettyFilesize(stats.AllBytes),
                    stats.MatchedFiles, Spi.IO.Misc.GetPrettyFilesize(stats.MatchedBytes));
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
                { "r|rname=",   "regex applied to the filename",            v => opts.Pattern = v },
                { "o|out=",     "filename for result of files (UTF8)",      v => opts.OutFilename = v },
                { "p|progress", "prints out the directory currently scanned for a little progress indicator",   v => opts.progress = (v != null) },
                { "t|depth=",   "max depth to go down",                     v => opts.Depth = Convert.ToInt32(v) },
                { "h|help",     "show this message and exit",               v => opts.show_help = v != null },
                { "f|format=",  "format the output. keywords: %fullname%",  v => opts.FormatString = v },
                { "j|follow",   "follow junctions",                         v => opts.FollowJunctions = (v != null) },
                { "d|dir=",     "directory names line by line in a file",   v => opts.FilenameWithDirs = v }
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

                if (!String.IsNullOrEmpty(opts.FilenameWithDirs))
                {
                    if ( !File.Exists(opts.FilenameWithDirs) )
                    {
                        Console.Error.WriteLine("E: The file you specified with the option -d does not exist. [{0}]", opts.FilenameWithDirs);
                        return null;
                    }
                    opts.Dirs = StringTools.TextFileByLine(opts.FilenameWithDirs);
                }
                else if (opts.Dirs.Count() == 0)
                {
                    opts.Dirs = new string[] { Directory.GetCurrentDirectory() };
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
    }
}
