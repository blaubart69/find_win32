using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spi
{
    public class StatusLineWriter
    {
        private DateTime LastWrite = new DateTime(0);
        private int PrevTextLen = -1;
        private bool _STATE_Console_available = true;

        private readonly TextWriter tw = Console.Error;
        private readonly TimeSpan TimeSpanForWriting = new TimeSpan(hours: 0, minutes: 0, seconds: 1);

        public void WriteWithDots(string Text)
        {
            if ( ! HasTimespanPassed(this.TimeSpanForWriting) )
            {
                return;
            }
            _internal_WriteSimple(GetDottedText(Text));
        }
        private string GetDottedText(string Text)
        {
            if ( ! _STATE_Console_available )
            {
                return Text;
            }

            string formattedText = Text;
            try
            {
                const string Dots = "...";
                if (Text.Length > Console.WindowWidth)
                {
                    int LenLeftPart = (Console.WindowWidth - Dots.Length) / 2;
                    int LenRightPart = Console.WindowWidth - Dots.Length - LenLeftPart;

                    formattedText = String.Format("{0}{1}{2}",
                        Text.Substring(0, LenLeftPart),
                        Dots,
                        Text.Substring(Text.Length - LenRightPart, LenRightPart)
                        );
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not format text to console. Seems Console is redirected. printing plain text. [{ex.Message}]");
                formattedText = Text;
                _STATE_Console_available = false;
            }

            return formattedText;
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

    }
}
