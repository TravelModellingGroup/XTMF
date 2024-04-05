using System;
using System.Diagnostics;
namespace TMG.Functions;

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
