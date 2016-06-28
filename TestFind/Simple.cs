using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestFind
{
    [TestClass]
    public class Simple
    {
        private static readonly string BaseDir = Path.Combine(System.Environment.GetEnvironmentVariable("TEMP"), "TestFind");

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
    }
}
