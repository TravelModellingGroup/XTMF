/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

    This file is part of XTMF.

    XTMF is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTMF is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTMF.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using TMG.Frameworks.Parallel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
                ParallelWorkSerialRecombination<float, float>.ComputeInParallel(a, f =>
                {
                    return f * 2.0f;
                }, f =>
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
