using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32.SafeHandles;

namespace TestFind
{
    [TestClass]
    public class Simple
    {
        private static readonly string BaseDir = Path.Combine(System.Environment.GetEnvironmentVariable("TEMP"), "TestFind");

        [ClassInitialize]
        public static void CreateTempDir(TestContext testContext)
        {
            Directory.CreateDirectory(BaseDir);
        }

        [TestMethod]
        public void FindOneFileInADir()
        {
            string newdir = Path.Combine(BaseDir, "newdir");
            Directory.CreateDirectory(newdir);
            File.CreateText(Path.Combine(newdir, "find.me"));
            var entries = Spi.IO.Directory.Entries(newdir, (rc,msg) => { Assert.Fail("error enumerating rc [{0}] message [{1}]", rc,msg);  });
            var iter = entries.GetEnumerator();
            var item = iter.MoveNext();
            Assert.AreEqual("find.me", iter.Current.Name);
            Assert.IsFalse(iter.MoveNext());
        }
        [TestMethod]
        public void TestFileTimeSetToMaxDotNetDateTime()
        {
            string testDir = Path.Combine(BaseDir, "DateTest");
            Directory.CreateDirectory(testDir);
            string FullFilename = Path.Combine(testDir, "datetest_DotNet_MaxValue.txt");
            FileStream f = File.Create(FullFilename);
            f.Close();

            File.SetLastWriteTime(FullFilename, DateTime.MaxValue);

            Spi.IO.DirEntry found = Spi.IO.Directory.Entries(testDir, null).First( i => i.Name.Equals("datetest_DotNet_MaxValue.txt"));
             
            Assert.AreEqual(DateTime.MaxValue, DateTime.FromFileTime(found.LastWriteTimeUtcLong) );

            string timestamp = Spi.IO.Misc.FiletimeToString(found.LastWriteTime);
            Assert.AreEqual("9999.12.31 23:59:59", timestamp);

        }
        [TestMethod]
        public void TestFileTime_SetMaxPrintable_NTFS_Time()
        {
            const string Testfilename = "datetest_MaxPrintable_NTFS_Time.txt";

            string testDir = Path.Combine(BaseDir, "DateTest");
            Directory.CreateDirectory(testDir);
            string FullFilename = Path.Combine(testDir, Testfilename);
            FileStream f = File.Create(FullFilename);
            f.Close();

            long MaxNTFSTime;

            using (SafeFileHandle hFile = Spi.Native.Win32.CreateFileW(FullFilename, Spi.Native.Win32.EFileAccess.GENERIC_WRITE, FileShare.Read,
                IntPtr.Zero, FileMode.Open, FileAttributes.Normal, IntPtr.Zero))
            {
                Assert.IsFalse(hFile.IsInvalid, "CreateFile failed rc={0}", System.Runtime.InteropServices.Marshal.GetLastWin32Error());

                FILETIME create = new FILETIME();
                FILETIME access = new FILETIME();
                FILETIME lastwrite;

                var st = new Spi.Native.Win32.SYSTEMTIME()
                { Year = 30827, Month = 12, Day = 31, Hour = 23, Minute = 59, Second = 59, Milliseconds = 999 };

                Spi.Native.Win32.SystemTimeToFileTime(
                    ref st,
                    out lastwrite);

                Assert.IsTrue(Spi.Native.Win32.SetFileTime(hFile.DangerousGetHandle(), ref create, ref access, ref lastwrite),
                    "setFileTime failed with rc={0} Msg={1}", System.Runtime.InteropServices.Marshal.GetLastWin32Error(),
                    new System.ComponentModel.Win32Exception().Message);

                Assert.AreEqual(0, System.Runtime.InteropServices.Marshal.GetLastWin32Error());

                MaxNTFSTime = Spi.IO.Misc.TwoIntToLong(lastwrite.dwHighDateTime, lastwrite.dwLowDateTime);
            }

            Spi.IO.DirEntry found = Spi.IO.Directory.Entries(testDir, null).First(i => i.Name.Equals(Testfilename));

            Assert.AreEqual(MaxNTFSTime, found.LastWriteTimeUtcLong);
        }
        [TestMethod]
        public void TestFileTime_SetMax_NTFS_Time()
        {
            const string Testfilename = "datetest_Max7FFFFFFFFFFFFFFF_NTFS_Time.txt";

            string testDir = Path.Combine(BaseDir, "DateTest");
            Directory.CreateDirectory(testDir);
            string FullFilename = Path.Combine(testDir, Testfilename);

            long MaxNTFSTime;

            using (SafeFileHandle hFile = Spi.Native.Win32.CreateFileW(FullFilename, Spi.Native.Win32.EFileAccess.GENERIC_WRITE, FileShare.Read,
                IntPtr.Zero, FileMode.Create, FileAttributes.Normal, IntPtr.Zero))
            {
                Assert.IsFalse(hFile.IsInvalid, "CreateFile failed rc={0}", System.Runtime.InteropServices.Marshal.GetLastWin32Error());

                FILETIME create = new FILETIME();
                FILETIME access = new FILETIME();
                FILETIME lastwrite = new FILETIME() { dwHighDateTime = 0x7FFFFFFF, dwLowDateTime = -1 };

                Assert.IsTrue(
                    Spi.Native.Win32.SetFileTime(hFile.DangerousGetHandle(), ref create, ref access, ref lastwrite),
                    "setFileTime failed with rc={0} Msg={1}", System.Runtime.InteropServices.Marshal.GetLastWin32Error(),
                    new System.ComponentModel.Win32Exception().Message);

                Assert.AreEqual(0, System.Runtime.InteropServices.Marshal.GetLastWin32Error());

                MaxNTFSTime = Spi.IO.Misc.TwoIntToLong(lastwrite.dwHighDateTime, lastwrite.dwLowDateTime);
            }

            Spi.IO.DirEntry found = Spi.IO.Directory.Entries(testDir, null).First(i => i.Name.Equals(Testfilename));

            Assert.AreEqual(MaxNTFSTime, found.LastWriteTimeUtcLong);



        }

    }
}
