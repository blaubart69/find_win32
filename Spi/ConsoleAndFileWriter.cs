using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spi
{
    public class ConsoleAndFileWriter : IDisposable
    {
        private readonly    TextWriter  ConsoleWriter;
        private             TextWriter  FileWriter;
        private readonly    string      Filename;

        public ConsoleAndFileWriter(TextWriter ConsoleWriter, string Filename)
        {
            this.ConsoleWriter = ConsoleWriter;
            this.Filename = Filename;
        }
        public void WriteLine(string Format, params object[] args)
        {
            _internal_WriteLine(ConsoleWriter, Format, args);

            if (String.IsNullOrEmpty(Filename))
            {
                return;
            }

            if (FileWriter == null)
            {
                FileWriter = new StreamWriter(
                    Filename,
                    false,      // append?
                    System.Text.Encoding.UTF8);
            }

            _internal_WriteLine(FileWriter, Format, args);
        }
        public bool hasDataWritten()
        {
            return FileWriter != null;
        }
        public void Dispose()
        {
            if ( FileWriter != null )
            {
                FileWriter.Close();
            }
        }
        /// <summary>
        /// This functions exists for the following problem:
        /// If you pass no "args" (args==null) it would be a WriteLine(Format,null).
        /// Then if the string (Format) you passed has "{" "}" in it, the call will mostly crash because of a bad c# format string.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="Format"></param>
        /// <param name="args"></param>
        private void _internal_WriteLine(TextWriter writer, string Format, params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                writer.WriteLine(Format);
            }
            else
            {
                writer.WriteLine(Format, args);
            }
        }
    }
}
