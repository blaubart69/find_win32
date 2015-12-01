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
        public IList<string> Dirs;
        public string Pattern;
        public string OutFilename;
        public bool show_help;
        public bool progress;
        public string FormatString;
        public bool FollowJunctions = false;
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
                    foreach (string dir in opts.Dirs)
                    {
                        EnumDir.Run(dir, opts, ref stats, ref CrtlC_pressed, 
                            (filenamefound) => OutWriter.WriteLine(filenamefound), 
                            (rc, ErrDir)    => ErrWriter.WriteLine("rc {0}\t{1}", rc, ErrDir));
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
                { "r|rname=",   "regex applied to the filename",         v => opts.Pattern = v },
                { "o|out=",     "filename for result of files (UTF8)",   v => opts.OutFilename = v },
                { "p|progress", "prints out the directory currently scanned for a little progress indicator",   v => opts.progress = (v != null) },
                //{ "v", "increase debug message verbosity",                      v => { if (v != null) ++verbosity; } },
                { "h|help",     "show this message and exit",            v => opts.show_help = v != null },
                { "f|format=",  "format the output",                     v => opts.FormatString = v },
                { "j|follow",   "follow junctions",                      v => opts.FollowJunctions = (v != null) }
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
    }
}
