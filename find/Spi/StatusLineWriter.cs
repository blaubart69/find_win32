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

        private void _internal_WriteSimple(string Text)
        {
            if (Text.Length < PrevTextLen)
            {
                string BlanksToDelete = new string(' ', PrevTextLen - Text.Length);
                tw.Write("{0}{1}\r", Text, BlanksToDelete);
            }
            else
            {
                tw.Write("{0}\r", Text);
            }
            PrevTextLen = Text.Length;
        }
        public void WriteSimple(string Text)
        {
            if (!HasTimespanPassed(this.TimeSpanForWriting))
            {
                return;
            }
            _internal_WriteSimple(Text);
        }
        public void WriteWithDots(string Text)
        {
            if ( ! HasTimespanPassed(this.TimeSpanForWriting) )
            {
                return;
            }

            const string Dots = "...";

            if (Text.Length > Console.WindowWidth)
            {
                int LenLeftPart = (Console.WindowWidth - Dots.Length) / 2;
                int LenRightPart = Console.WindowWidth - Dots.Length - LenLeftPart;

                tw.Write("{0}{1}{2}\r",
                    Text.Substring(0, LenLeftPart),
                    Dots,
                    Text.Substring(Text.Length - LenRightPart, LenRightPart)
                    );
            }
            else
            {
                _internal_WriteSimple(Text);
            }

            PrevTextLen = Text.Length;
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
