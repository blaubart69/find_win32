using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace find
{
    class Program
    {
        static int Main(string[] args)
        {
            string Dirname = null;
            string Pattern = null;
            string OutFilename = null;
            bool show_help = false;
            bool progress = false;

            const string ErrFilename = "find.err.txt";

            TextWriter ErrWriter = null;
            TextWriter OutWriter = null;

            var p = new Mono.Options.OptionSet() {
                { "r|rname=",   "regex applied to the filename",         v => Pattern = v },
                { "o|out=",     "filename for result of files (UTF8)",   v => OutFilename = v },
                { "p|progress=","filename for result of files (UTF8)",   v => progress = (v != null) },
                //{ "v", "increase debug message verbosity",                      v => { if (v != null) ++verbosity; } },
                { "h|help",   "show this message and exit",           v => show_help = v != null },
            };
            try
            {
                List<string> ExtraParams = p.Parse(args);
                if ( ExtraParams.Count() == 0 )
                {
                    Console.Error.WriteLine("E: You must specify at least one directory name.");
                    Console.Error.WriteLine();
                    ShowHelp(p);
                    return 8;
                }
                else
                {
                    Dirname = ExtraParams[0];
                }

            }
            catch (Mono.Options.OptionException oex)
            {
                Console.WriteLine(oex.Message);
                return 8;
            }
            if (show_help)
            {
                ShowHelp(p);
                return 8;
            }

            try
            {
                UInt64 AllBytes = 0;
                UInt64 MatchedBytes = 0;
                UInt64 AllFiles = 0;
                UInt64 AllDirs = 0;
                UInt64 MatchedFiles = 0;

                Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs e) => 
                { WriteStats(AllBytes, MatchedBytes, AllFiles, MatchedFiles, AllDirs); };

                foreach (var entry in Spi.IO.Directory.Entries(Dirname, -1, null, null))
                {
                    if (entry.LastError != 0)
                    {
                        WriteToConsoleAndStream(ErrFilename, ref ErrWriter, Console.Error, "rc [{0}] dir [{1}]", entry.LastError, entry.Fullname);
                        continue;
                    }

                    if ( entry.isDirectory )
                    {
                        AllDirs += 1;
                        if (progress)
                        {
                            Console.Error.Write("[{0}]\r", entry.Dirname);
                        }
                        continue;
                    }

                    AllBytes += entry.Filesize;
                    AllFiles += 1;

                    bool PrintEntry = true;
                    if (Pattern != null) 
                    {
                        PrintEntry = Regex.IsMatch(entry.Filename, Pattern);
                    }

                    if (PrintEntry)
                    {
                        MatchedBytes += entry.Filesize;
                        MatchedFiles += 1;

                        WriteToConsoleAndStream(OutFilename, ref OutWriter, Console.Out, "{0} {1,12} {2}",
                            entry.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            entry.Filesize,
                            entry.Fullname);

                    }
                }
                WriteStats(AllBytes, MatchedBytes, AllFiles, MatchedFiles, AllDirs);
            }
            finally
            {
                if (ErrWriter!=null) { ErrWriter.Close(); }
                if (OutWriter != null) { OutWriter.Close(); }
            }
            return 0;
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
        static void WriteStats(UInt64 AllBytes, UInt64 MatchedBytes, UInt64 AllFiles, UInt64 MatchedFiles, UInt64 AllDirs)
        {
            Console.Error.WriteLine("\nbytes seen [{0}] ({1}),  bytes matching files [{2}] ({3}), files seen [{4}], files matched [{5}], dirs seen [{6}]",
                    AllBytes,
                    Spi.IO.Misc.GetPrettyFilesize(AllBytes),
                    MatchedBytes,
                    Spi.IO.Misc.GetPrettyFilesize(MatchedBytes),
                    AllFiles,
                    MatchedFiles,
                    AllDirs
                    );
        }
        static void ShowHelp(Mono.Options.OptionSet p)
        {
            Console.WriteLine("Usage: find [OPTIONS]+ directory");
            Console.WriteLine("lists all files in the given dir + subdirs");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }
        /*
        static bool GetArgs(string[] args, out string Dirname, out string Pattern, out string OutFilename)
        {
            Dirname = "";
            Pattern = null;
            OutFilename = null;

            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i].ToLower();
                if (key.StartsWith("-dir")) Dirname = args[i + 1];
                else if (key.StartsWith("-pattern")) Pattern = args[i + 1];
                else if (key.StartsWith("-out")) OutFilename = args[i + 1];
            }
            return !String.IsNullOrEmpty(Dirname);
        }*/
    }
}
