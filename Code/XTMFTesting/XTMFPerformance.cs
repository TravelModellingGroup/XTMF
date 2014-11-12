using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace XTMF.Testing
{
    [TestClass]
    public class XTMFPerformance
    {
        [TestMethod]
        public void TestStartupPerformance()
        {
            var watch = new Stopwatch();
            watch.Start();
            XTMFRuntime runtime = new XTMFRuntime();
            watch.Stop();
            Assert.IsTrue( watch.ElapsedMilliseconds < 500, "Start time is greater than 1/2 a second!" );
        }
    }
}
