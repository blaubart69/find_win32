using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spi.IO
{
    public class StatusLineWriter
    {
        private DateTime LastWrite = new DateTime(0);
        private int PrevTextLen = -1;

        private readonly TextWriter tw = Console.Error;
        private readonly TimeSpan TimeSpanForWriting = new TimeSpan(hours: 0, minutes: 0, seconds: 1);

        public void WriteSimple(string Text)
        {
            if (!HasTimespanPassed(this.TimeSpanForWriting))
            {
                return;
            }

            int TextLen = Text.Length;

            if (TextLen < PrevTextLen)
            {
                string BlanksToDelete = new string(' ', PrevTextLen - TextLen);
                tw.Write("{0}{1}\r", Text, BlanksToDelete);
            }
            else
            {
                tw.Write("{0}\r", Text);
            }
            PrevTextLen = TextLen;
        }
        public void WriteWithDots(string Text)
        {
            if ( ! HasTimespanPassed(this.TimeSpanForWriting) )
            {
                return;
            }

            int TextLen = Text.Length;

            string Dots = "...";

            if (TextLen > Console.WindowWidth)
            {
                int LenLeftPart = (Console.WindowWidth - Dots.Length) / 2;
                int LenRightPart = Console.WindowWidth - Dots.Length - LenLeftPart;

                Console.Error.Write("{0}{1}{2}\r",
                    Text.Substring(0, LenLeftPart),
                    Dots,
                    Text.Substring(Text.Length - LenRightPart, LenRightPart)
                    );
            }
            else
            {
                if (TextLen < PrevTextLen)
                {
                    string BlanksToDelete = new string(' ', PrevTextLen - TextLen);
                    Console.Error.Write("{0}{1}\r", Text, BlanksToDelete);
                }
                else
                {
                    Console.Error.Write("{0}\r", Text);
                }
            }

            PrevTextLen = TextLen;
        }
        private bool HasTimespanPassed(TimeSpan tsMustHavePassed)
        {
            DateTime Now = DateTime.Now;

            bool hasPassed = new TimeSpan(Now.Ticks - LastWrite.Ticks) > tsMustHavePassed;
            
            if ( hasPassed )
            {
                LastWrite = Now;
            }

            return hasPassed;
        }
    }
}
