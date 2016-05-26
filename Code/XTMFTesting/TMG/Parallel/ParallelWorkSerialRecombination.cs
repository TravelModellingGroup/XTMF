using System;
using TMG.Frameworks.Parallel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Runtime.CompilerServices;

namespace XTMF.Testing.TMG.Parallel
{
    [TestClass]
    public class TestParallelWorkSerialRecombination
    {
        [TestMethod]
        public void TestComputeInParallel()
        {
            Random r = new Random(12345);
            for (int i = 0; i < 100; i++)
            {
                var a = new float[0x1000];
                for (int j = 0; j < a.Length; j++)
                {
                    a[j] = (float)r.NextDouble();
                }
                // now that we have our data compute the sum of the numbers multiplied by two
                float total = 0.0f;
                var realSum = Mul2(a);
                ParallelWorkSerialRecombination<float, float>.ComputeInParallel(a, (float f) =>
                {
                    return f * 2.0f;
                }, (float f) =>
                {
                    total += f;
                });
                Assert.AreEqual(realSum, total, "These numbers must be identical!");
            }
        }

        /// <summary>
        /// the x86 JITTER will mess this up for us unless we turn off
        /// optimization for this since it will carry the accumulator
        /// at 80bit precision instead of 32bit.
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.NoOptimization)]
        private float Mul2(float[] a)
        {
            var acc = 0.0f;
            for (int i = 0; i < a.Length; i++)
            {
                acc += (a[i] * 2.0f);
            }
            return acc;
        }
    }
}
