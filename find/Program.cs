using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text.RegularExpressions;

using Spi;

namespace find
{
    public class Stats
    {
        public long AllBytes;
        public long MatchedBytes;
        public long AllFiles;
        public long AllDirs;
        public long MatchedFiles;
        public long Enqueued;
        public long EnumerationsRunning;
        public string LongestFilename;
        public int LongestFilenameLength;
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
        public bool RunParallel = true;
        public bool Sum = false;
        public bool tsv = false;
        public string Encoding = null;
        public bool printLongestFilename = false;
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
                ManualResetEvent CrtlCEvent = new ManualResetEvent(false);

                new Thread(new ThreadStart(() =>
                {
                    while (true)
                    {
                        if (Console.ReadKey().KeyChar == 'q')
                        {
                            Console.Error.WriteLine("going down...");
                            CrtlCEvent.Set();
                            break;
                        }
                    }
                }))
                { IsBackground = true }.Start();

                System.Text.Encoding OutEncoding;
                if ( "16LE".Equals(opts.Encoding, StringComparison.OrdinalIgnoreCase) )
                {
                    OutEncoding = System.Text.Encoding.Unicode;
                }
                else
                {
                    OutEncoding = System.Text.Encoding.UTF8;
                }

                using (var ErrWriter = new ConsoleAndFileWriter(Console.Error, ErrFilename))
                using (var OutWriter = new ConsoleAndFileWriter(ConsoleWriter:  String.IsNullOrEmpty(opts.OutFilename) ? Console.Out : null, 
                                                                Filename:       opts.OutFilename, 
                                                                encoding:       OutEncoding))
                {
                    try
                    {
                        Spi.Native.PrivilegienStadl.TryToSetBackupPrivilege();
                    }
                    catch (Exception ex)
                    {
                        //ErrWriter.WriteException(ex);
                        ErrWriter.WriteLine("could not set SE_BACKUP_PRIVILEGE");
                    }

                    Action<string> ProgressHandler = null;
                    if ( opts.progress )
                    {
                        Spi.StatusLineWriter statusWriter = new StatusLineWriter();
                        ProgressHandler = (progressText) =>
                        {
                            statusWriter.WriteWithDots(progressText);
                        };
                    }

                    void ErrorHandler(int rc, string ErrDir) => ErrWriter.WriteLine("{0}\t{1}", rc, ErrDir);
                    bool IsFilenameMatching(string filename) => (opts.Pattern == null) ? true : Regex.IsMatch(filename, opts.Pattern);

                    PrintFunction MatchedEntryWriter = null;
                    if (! opts.Sum)
                    {
                        MatchedEntryWriter = (rootDir, dir, find_data) => 
                                FormatOutput.PrintEntry(rootDir, dir, find_data, opts.FormatString, OutWriter, ErrorHandler, opts.tsv);
                    }

                    opts.Dirs = opts.Dirs.Select(d => Spi.IO.Long.GetLongFilenameNotation(d));

                    EnumOptions enumOpts = new EnumOptions()
                    {
                        errorHandler = ErrorHandler,
                        printHandler = MatchedEntryWriter,
                        matchFilename = IsFilenameMatching,
                        followJunctions = opts.FollowJunctions,
                        maxDepth = opts.Depth,
                        lookForLongestFilename = opts.printLongestFilename
                    };

                    Stats stats;
                    if (opts.RunParallel)
                    {
                        stats = RunParallel.Run(opts.Dirs, enumOpts, ProgressHandler, CrtlCEvent);
                    }
                    else
                    {
                        stats = RunSequential.Run(opts.Dirs, enumOpts, ProgressHandler, CrtlCEvent);
                    }

                    WriteStats(stats, opts.printLongestFilename);
                    if (ErrWriter.hasDataWritten())
                    {
                        Console.Error.WriteLine("\nerrors were logged to file [{0}]", ErrFilename);
                    }
                    
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Hoppala. Call 555-D.R.S.P.I.N.D.L.E.R");
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
                return 12;
            }
            return 0;
        }

        static void WriteStats(Stats stats, bool printLongestFilename)
        {
            Console.Error.WriteLine(
                   "\n"
                +  "dirs           {0,10}\n" 
                +  "files          {1,10} ({2})\n"
                +  "files matched  {3,10} ({4})",
                    stats.AllDirs, 
                    stats.AllFiles,     Spi.IO.Misc.GetPrettyFilesize(stats.AllBytes),
                    stats.MatchedFiles, Spi.IO.Misc.GetPrettyFilesize(stats.MatchedBytes));
            if ( printLongestFilename)
            {
                Console.Error.WriteLine($"Longest filename len:  {stats.LongestFilenameLength}");
                Console.Error.WriteLine($"Longest filename name: {stats.LongestFilename}");
            }
        }
        static void ShowHelp(Mono.Options.OptionSet p)
        {
            Console.WriteLine("Usage: find [OPTIONS] [DIRECTORY]...");
            Console.WriteLine("lists all files in the given dir + subdirs");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
            Console.WriteLine("\nSamples:"
                + "\n  file extension via regex match ... find.exe -r \"\\.txt$\"" );
        }
        static Opts GetOpts(string[] args)
        {
            Opts opts = new Opts();
            var p = new Mono.Options.OptionSet() {
                { "r|rname=",   "regex applied to the filename",            v => opts.Pattern = v },
                { "o|out=",     "filename for result of files (UTF8)",      v => opts.OutFilename = v },
                { "p|progress", "prints out the directory currently scanned for a little progress indicator",   v => opts.progress = (v != null) },
                { "d|depth=",   "max depth to go down",                     v => opts.Depth = Convert.ToInt32(v) },
                { "h|help",     "show this message and exit",               v => opts.show_help = v != null },
                { "u|userformat=",  "format the output. keywords: %fullname%",  v => opts.FormatString = v },
                { "j|follow",   "follow junctions",                         v => opts.FollowJunctions = (v != null) },
                { "f|file=",    "directory names line by line in a file",   v => opts.FilenameWithDirs = v },
                { "q|sequential", "run single-threaded",                    v => opts.RunParallel = !( v != null) },
                { "s|sum",      "just count",                               v => opts.Sum = ( v != null) },
                { "t|tsv",      "write tab separated out file",             v => opts.tsv = ( v != null) },
                { "e|enc=",     "encoding default=UTF8 [16LE=UTF16 LE BOM]",v => opts.Encoding = v },
                { "l|len",      "print out longrst seen filename",          v => opts.printLongestFilename = (v != null) }
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
