using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
namespace TMG.Functions
{
    public static class PerformanceTesting
    {
        public static long Time(Action action)
        {
            Stopwatch watch = Stopwatch.StartNew();
            action();
            watch.Stop();
            return watch.ElapsedTicks;
        }
    }
}
