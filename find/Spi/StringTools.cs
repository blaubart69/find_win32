using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spi
{
    public class StringTools
    {
        public static IEnumerable<string> TextFileByLine(string Filename)
        {
            using ( TextReader tr = new StreamReader(Filename) )
            {
                string line;
                while ((line = tr.ReadLine()) != null)
                {
                    yield return line;
                }
            }
        }
    }
}
