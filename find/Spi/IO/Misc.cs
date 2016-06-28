using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Spi.IO
{
    public class Misc
    {
        public static string GetPrettyFilesize(ulong Filesize)
        {
            StringBuilder sb = new StringBuilder(50);
            Win32.StrFormatByteSize((long)Filesize, sb, 50);
            return sb.ToString();
        }
        public static bool IsDirectory(string dir)
        {
            uint rc = Win32.GetFileAttributes(dir);

            if (rc == uint.MaxValue)
            {
                //int LastError = Spi.Win32.GetLastWin32Error();
                return false;   // doesn't exist
            }
            /*
            FILE_ATTRIBUTE_DIRECTORY
            16 (0x10)
            The handle that identifies a directory.
            */
            return (rc & 0x10) != 0;
        }
        public static string GetLongFilenameNotation(string FilenameShort)
        {
            if (FilenameShort.Length >= 2 && FilenameShort[1] == ':')
            {
                return @"\\?\" + FilenameShort;
            }
            else if (FilenameShort.StartsWith(@"\\") && !FilenameShort.StartsWith(@"\\?\") )
            {
                return @"\\?\UNC\" + FilenameShort.Remove(0,2);
            }
            return FilenameShort;
        }
        public static DateTime ConvertFromFiletime(int HighTime, int LowTime)
        {
            ulong val = ((ulong)HighTime) << 32;
            val = val | (uint)LowTime;
            return DateTime.FromFileTime((long)val);
        }
    }
}
