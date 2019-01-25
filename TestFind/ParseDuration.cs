using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestFind
{
    [TestClass]
    public class ParseDuration
    {
        [TestMethod]
        public void ParseIso8601Duration()
        {
            Assert.IsTrue( TimeSpan.TryParse("3", out TimeSpan result) );
            Assert.AreEqual(new TimeSpan(3, 0, 0, 0), result);
        }
    }
}
