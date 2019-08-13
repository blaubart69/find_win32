using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text.RegularExpressions;

using Spi;
using Spi.Native;
using Spi.IO;

namespace find
{
    public enum FiletimeSearch
    {
        NEWER,
        OLDER
    }
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
        public string RegexPattern;
        public bool RegexCaseInsensitive = false;
        public string OutFilename;
        public bool show_help;
        public bool progress;
        //public string FormatString;
        public bool FullFormat;
        public bool PrependRootDir;
        public bool FollowJunctions = false;
        public string FilenameWithDirs;
        public int Depth = -1;
        public bool Sum = false;
        public bool tsv = false;
        public string Encoding = null;
        public bool printLongestFilename = false;
        public EMIT emitEntries = EMIT.BOTH;
        public int maxThreads = 32;
        public long filterFiletimeUTC = 0;
        public FiletimeSearch KindOfTimeSearch;
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

            //Console.WriteLine($"emit: {opts.emitEntries.ToString("g")}");

            try
            {
                //ManualResetEvent CrtlCEvent = new ManualResetEvent(false);
                CancellationTokenSource CtrlC = new CancellationTokenSource();

                new Thread(new ThreadStart(() =>
                {
                    try
                    {
                        while (true)
                        {
                            if (Console.ReadKey().KeyChar == 'q')
                            {
                                Console.Error.WriteLine("going down...");
                                CtrlC.Cancel();
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"something is wrong in the thread waiting for 'q' to be pressed.\n[{ex.Message}]");
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
                try
                {
                    PrivilegienStadl.TryToSetBackupPrivilege();
                }
                catch
                {
                    Console.Error.WriteLine("could not set SE_BACKUP_PRIVILEGE");
                }

                using (var ErrWriter = new ConsoleAndFileWriter(Console.Error, ErrFilename))
                using (var OutWriter = new ConsoleAndFileWriter(ConsoleWriter:  String.IsNullOrEmpty(opts.OutFilename) ? Console.Out : null, 
                                                                Filename:       opts.OutFilename, 
                                                                encoding:       OutEncoding))
                {
                    Action<string> ProgressHandler = null;
                    if ( opts.progress )
                    {
                        StatusLineWriter statusWriter = new StatusLineWriter();
                        ProgressHandler = (progressText) =>
                        {
                            statusWriter.WriteWithDots(progressText);
                        };
                    }
                    //
                    //
                    //
                    void ErrorHandler(int rc, string ErrDir) => ErrWriter.WriteLine("{0}\t{1}", rc, ErrDir);
                    //
                    // filename match
                    //
                    Predicate<string> MatchHandler = null;
                    if ( ! String.IsNullOrEmpty(opts.RegexPattern) )
                    {
                        RegexOptions rexOpt = opts.RegexCaseInsensitive ? RegexOptions.IgnoreCase : RegexOptions.None;
                        MatchHandler = (string filename) => Regex.IsMatch(filename, opts.RegexPattern, rexOpt);
                    }
                    //
                    // filetime modified match
                    //
                    Predicate<long> matchFiletimeHandler = null;
                    if (opts.filterFiletimeUTC > 0)
                    {
                        matchFiletimeHandler = (long LastWriteFiletime) =>
                        {
                            bool matched = false;

                            if ( opts.KindOfTimeSearch == FiletimeSearch.OLDER )
                            {
                                if ( LastWriteFiletime < opts.filterFiletimeUTC )
                                {
                                    matched = true;
                                }
                            }
                            else if (opts.KindOfTimeSearch == FiletimeSearch.NEWER)
                            {
                                if ( LastWriteFiletime > opts.filterFiletimeUTC )
                                {
                                    matched = true;
                                }
                            }
                            return matched;
                        };
                    }

                    PrintFunction MatchedEntryWriter = null;
                    if (! opts.Sum)
                    {
                        MatchedEntryWriter = (string rootDir, string dir, ref Win32.WIN32_FIND_DATA find_data) => 
                        FormatOutput.PrintEntry(rootDir, dir, ref find_data, OutWriter, ErrorHandler, opts.tsv, opts.FullFormat, opts.PrependRootDir);
                    }

                    EnumOptions enumOpts = new EnumOptions()
                    {
                        errorHandler = ErrorHandler,
                        printHandler = MatchedEntryWriter,
                        matchFilename = MatchHandler,
                        matchFiletime = matchFiletimeHandler,
                        followJunctions = opts.FollowJunctions,
                        maxDepth = opts.Depth,
                        lookForLongestFilename = opts.printLongestFilename,
                        emit = opts.emitEntries
                    };

                    Stats stats;
                    opts.Dirs = opts.Dirs.Select(d => Long.GetLongFilenameNotation(d));
                    stats = RunParallel.Run(opts.Dirs, enumOpts, ProgressHandler, CtrlC.Token, opts.maxThreads);

                    WriteStats(stats, opts.printLongestFilename, printMatches: enumOpts.matchFilename != null );
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

        static void WriteStats(Stats stats, bool printLongestFilename, bool printMatches)
        {
            if (printMatches)
            {
                Console.Error.WriteLine(
                       "\n"
                    + "dirs           {0,10:N0}\n"
                    + "files          {1,10:N0} ({2})\n"
                    + "files matched  {3,10:N0} ({4})",
                        stats.AllDirs,
                        stats.AllFiles, Misc.GetPrettyFilesize(stats.AllBytes),
                        stats.MatchedFiles, Misc.GetPrettyFilesize(stats.MatchedBytes));
            }
            else
            {
                Console.Error.WriteLine(
                       "\n"
                    + "dirs           {0,10:N0}\n"
                    + "files          {1,10:N0} ({2})\n",
                        stats.AllDirs,
                        stats.AllFiles, Misc.GetPrettyFilesize(stats.AllBytes));
            }

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
            Console.WriteLine();
            Console.WriteLine("Timespan: [-]{ d | [d.]hh:mm[:ss[.ff]] }");
            Console.WriteLine("\nSamples:"
                + "\n  file extension via regex match ... find.exe -r \"\\.txt$\"" );
        }
        static Opts GetOpts(string[] args)
        {
            Opts opts = new Opts();

            string emit = null;
            string timeExpression = null;

            var p = new Mono.Options.OptionSet() {
                { "r|rname=",   "regex applied to the filename",                            v =>   opts.RegexPattern = v },
                { "i|riname=",  "regex applied to the filename - case insensitive",         v => { opts.RegexPattern = v; opts.RegexCaseInsensitive = true; } },
                { "o|out=",     "filename for result of files (UTF8)",      v => opts.OutFilename = v },
                { "p|progress", "prints out the directory currently scanned for a little progress indicator",   v => opts.progress = (v != null) },
                { "d|depth=",   "max depth to go down",                     v => opts.Depth = Convert.ToInt32(v) },
                //{ "u|userformat=",  "format the output. keywords: %fullname%",  v => opts.FormatString = v },
                { "j|follow",   "follow junctions",                         v => opts.FollowJunctions = (v != null) },
                { "f|file=",    "directory names line by line in a file",   v => opts.FilenameWithDirs = v },
                { "s|sum",      "just count",                               v => opts.Sum = ( v != null) },
                { "t|tsv",      "write tab separated find_data",            v => opts.tsv = ( v != null) },
                { "c|enc=",     "encoding default=UTF8 [16LE=UTF16 LE BOM]",v => opts.Encoding = v },
                { "l|len",      "print out longest seen filename",          v => opts.printLongestFilename = (v != null) },
                { "e|emit=",    "emit what {f|d|b} (files, directories, both) default: both", v => emit = v.ToUpper() },
                { "ts=",        "{timespan;[new|old]}",                     v => timeExpression = v },
                { "full",       "full format",                              v => opts.FullFormat = true },
                { "root",       "prepend root directory",                   v => opts.PrependRootDir = true },
                { "x|threads=", "max threads to use for given directory",   (int v) => opts.maxThreads = v },
                { "h|help",     "show this message and exit",               v => opts.show_help = v != null }
            };
            try
            {
                opts.Dirs = p.Parse(args);

                if (!String.IsNullOrEmpty(opts.RegexPattern))
                {
                    Console.Error.WriteLine("pattern parsed for rname [{0}]", opts.RegexPattern);
                }
                /*
                if (!String.IsNullOrEmpty(opts.FormatString))
                {
                    Console.Error.WriteLine("FormatString [{0}]", opts.FormatString);
                }
                */

                if (!String.IsNullOrEmpty(opts.FilenameWithDirs))
                {
                    if ( !File.Exists(opts.FilenameWithDirs) )
                    {
                        Console.Error.WriteLine("E: The file you specified with the option -d does not exist. [{0}]", opts.FilenameWithDirs);
                        return null;
                    }
                    //opts.Dirs = StringTools.TextFileByLine(opts.FilenameWithDirs);
                    opts.Dirs = File.ReadLines(opts.FilenameWithDirs);
                }
                else if (opts.Dirs.Count() == 0)
                {
                    opts.Dirs = new string[] { System.IO.Directory.GetCurrentDirectory() };
                }
                if ( !String.IsNullOrEmpty(emit) )
                {
                    if (emit.StartsWith("F"))
                    {
                        opts.emitEntries = EMIT.FILES;
                    }
                    else if (emit.StartsWith("D"))
                    {
                        opts.emitEntries = EMIT.DIRS;
                    }
                    else if (emit.StartsWith("B"))
                    { 
                        opts.emitEntries = EMIT.BOTH;
                    }
                }
                //
                // newer, older
                //
                if ( !String.IsNullOrEmpty(timeExpression) )
                {
                    if ( ParseTimespan(timeExpression, out DateTime? PointInTime, out opts.KindOfTimeSearch))
                    {
                        opts.filterFiletimeUTC = PointInTime.Value.ToFileTimeUtc();
                        Console.Error.WriteLine($"showing files {opts.KindOfTimeSearch} than {PointInTime.Value}");
                    }
                    else
                    {
                        Console.Error.WriteLine("could not parse your timefilter");
                        return null;
                    }
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
        static bool ParseTimespan(string timeExpression, out DateTime? PointInTime, out FiletimeSearch TimeDirection)
        {
            PointInTime = null;
            TimeDirection = FiletimeSearch.OLDER;

            string[] timeAndKind = timeExpression.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            TimeSpan span;
            if ( timeAndKind.Length == 0)
            {
                return false;
            }
            else if (timeAndKind.Length == 1)
            {
                TimeDirection = FiletimeSearch.OLDER;
                span = TimeSpan.Parse(timeAndKind[0]);
            }
            else if (timeAndKind.Length == 2)
            {
                if (timeAndKind[1].StartsWith("new", StringComparison.OrdinalIgnoreCase) )
                {
                    TimeDirection = FiletimeSearch.NEWER;
                }
                else if (timeAndKind[1].StartsWith("old", StringComparison.OrdinalIgnoreCase))
                {
                    TimeDirection = FiletimeSearch.OLDER;
                }

                span = TimeSpan.Parse(timeAndKind[0]);
            }
            else
            {
                return false;
            }

            PointInTime = DateTime.Now.Subtract(span);

            return true;
        }
    }
}
