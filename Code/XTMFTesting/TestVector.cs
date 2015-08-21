/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TMG.Functions;
using System.Numerics;
namespace XTMF.Testing
{
    [TestClass]
    public class TestVector
    {
        static Vector<float> _Unused;

        static TestVector()
        {
            _Unused = Vector<float>.One;
        }

        [TestInitialize]
        public void Initialize()
        {
            /*if(!IsHardwareAccelerated)
            {
                Assert.Fail("Hardware acceleration is required for these tests!");
            }*/
        }

        [TestMethod]
        public void TestSum()
        {
            float[] testArray = Enumerable.Range(1, 100).Select(p => 2.0f).ToArray();
            Assert.AreEqual(2.0f * testArray.Length, VectorHelper.VectorSum(testArray, 0, testArray.Length), 0.000001f);
        }

        [TestMethod]
        public void TestVectorSquareDiff()
        {
            float[] first = Enumerable.Range(1, 100).Select(p => 2.0f).ToArray();
            float[] second = Enumerable.Range(1, 100).Select(p => 4.0f).ToArray();
            Assert.AreEqual(4.0f * first.Length, VectorHelper.VectorSquareDiff(first, 0, second, 0, first.Length), 0.000001f);
        }

        [TestMethod]
        public void TestVectorMultiply2Vectors()
        {
            float[] first = Enumerable.Range(1, 100).Select(p => 2.0f).ToArray();
            float[] second = Enumerable.Range(1, 100).Select(p => 4.0f).ToArray();
            VectorHelper.VectorMultiply(first, 0, first, 0, second, 0, first.Length);
            for(int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(8.0f, first[i], 0.000001f);
            }
        }

        [TestMethod]
        public void TestVectorMultiply1V1S()
        {
            float[] first = Enumerable.Range(1, 100).Select(p => 2.0f).ToArray();
            VectorHelper.VectorMultiply(first, 0, first, 0, -1.0f, first.Length);
            for(int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(-2.0f, first[i], 0.000001f);
            }
        }

        [TestMethod]
        public void TestVectorMultiply4V()
        {
            float[] first = Enumerable.Range(1, 100).Select(p => 2.0f).ToArray();
            float[] second = Enumerable.Range(1, 100).Select(p => 3.0f).ToArray();
            float[] third = Enumerable.Range(1, 100).Select(p => -1.0f).ToArray();
            float[] fourth = Enumerable.Range(1, 100).Select(p => 5.0f).ToArray();
            VectorHelper.VectorMultiply(first, 0, first, 0, second, 0, third, 0, fourth, 0, first.Length);
            for(int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(-30.0f, first[i], 0.000001f);
            }
        }

        [TestMethod]
        public void TestVectorMultiplyAndSum2V()
        {
            float[] first = Enumerable.Range(1, 100).Select(p => 2.0f).ToArray();
            float[] second = Enumerable.Range(1, 100).Select(p => 3.0f).ToArray();
            var total = VectorHelper.VectorMultiplyAndSum(first, 0, first, 0, second, 0, first.Length);
            Assert.AreEqual(600.0f, total, 0.00001f);
            for(int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(6.0f, first[i], 0.00001f);
            }
        }

        [TestMethod]
        public void TestVectorMultiplyAndSum2VNoSave()
        {
            float[] first = Enumerable.Range(1, 100).Select(p => 2.0f).ToArray();
            float[] second = Enumerable.Range(1, 100).Select(p => 3.0f).ToArray();
            var total = VectorHelper.VectorMultiplyAndSum(first, 0, second, 0, first.Length);
            Assert.AreEqual(600.0f, total, 0.00001f);
        }

        [TestMethod]
        public void TestVectorMultiplyAndSum3VNoSave()
        {
            float[] first = Enumerable.Range(1, 100).Select(p => 2.0f).ToArray();
            float[] second = Enumerable.Range(1, 100).Select(p => 3.0f).ToArray();
            float[] third = Enumerable.Range(1, 100).Select(p => 5.0f).ToArray();
            var total = VectorHelper.VectorMultiply3AndSum(first, 0, second, 0, third, 0, first.Length);
            Assert.AreEqual(3000, total, 0.00001f);
        }

        [TestMethod]
        public void TestVectorMultiply2Scalar1AndColumnSum()
        {
            float[] first = Enumerable.Range(1, 100).Select(p => 2.0f).ToArray();
            float[] second = Enumerable.Range(1, 100).Select(p => 3.0f).ToArray();
            float[] columnSum = Enumerable.Range(1, 100).Select(p => (float)p).ToArray();
            VectorHelper.VectorMultiply2Scalar1AndColumnSum(first, 0, first, 0, second, 0, -1.0f, columnSum, 0, first.Length);
            for(int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(-6.0f, first[i], 0.00001f);
                Assert.AreEqual((i + 1) + -6.0f, columnSum[i], 0.00001f);
            }
        }

        [TestMethod]
        public void TestVectorMultiply3Scalar1AndColumnSum()
        {
            float[] first = Enumerable.Range(1, 100).Select(p => 2.0f).ToArray();
            float[] second = Enumerable.Range(1, 100).Select(p => 3.0f).ToArray();
            float[] third = Enumerable.Range(1, 100).Select(p => 7.0f).ToArray();
            float[] columnSum = Enumerable.Range(1, 100).Select(p => (float)p).ToArray();
            VectorHelper.VectorMultiply3Scalar1AndColumnSum(first, 0, first, 0, second, 0, third, 0, -1.0f, columnSum, 0, first.Length);
            for(int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(-42.0f, first[i], 0.00001f);
                Assert.AreEqual((i + 1) + -42.0f, columnSum[i], 0.00001f);
            }
        }

        [TestMethod]
        public void TestVectorAdd()
        {
            float[] first = Enumerable.Range(1, 100).Select(p => 2.0f).ToArray();
            float[] second = Enumerable.Range(1, 100).Select(p => 3.0f).ToArray();
            VectorHelper.VectorAdd(first, 0, first, 0, second, 0, first.Length);
            for(int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(5.0f, first[i], 0.00001f);
            }
        }

        [TestMethod]
        public void TestSelectIfFinite()
        {
            Vector<float> alternate = new Vector<float>(1.0f);
            float[] temp = new float[Vector<float>.Count];
            for(int i = 0; i < temp.Length; i++)
            {
                temp[i] = i + 5.0f;
            }
            temp[temp.Length - 1] = float.NaN;
            temp[temp.Length - 2] = float.PositiveInfinity;
            temp[temp.Length - 3] = float.NegativeInfinity;
            Vector<float> v = new Vector<float>(temp);
            var result = new float[Vector<float>.Count];
            VectorHelper.SelectIfFinite(v, alternate).CopyTo(result);
            for(int i = 0; i < temp.Length - 3; i++)
            {
                Assert.AreEqual(temp[i], result[i]);
            }
            Assert.AreEqual(1.0f, result[result.Length - 1], "NaN wasn't change to 1!");
            Assert.AreEqual(1.0f, result[result.Length - 2], "Positive Infinity wasn't change to 1!");
            Assert.AreEqual(1.0f, result[result.Length - 3], "Negative Infinity wasn't change to 1!");
        }

        [TestMethod]
        public void TestReplaceIfLessThanMinimum()
        {
            var temp = new float[0x100];
            const float incrementFactor = 0.015f;
            for (int i = 0; i < temp.Length; i++)
            {
                temp[i] = incrementFactor * i;
            }
            VectorHelper.ReplaceIfLessThanOrNotFinite(temp, 0, 0.0f, 0.2f, temp.Length);
            for (int i = 0; i < temp.Length; i++)
            {
                if(incrementFactor * i < 0.2f)
                {
                    Assert.AreEqual(0.0f, temp[i]);
                }
                else
                {
                    Assert.AreEqual(incrementFactor * i, temp[i], 0.001f);
                }
            }
        }
    }
}
