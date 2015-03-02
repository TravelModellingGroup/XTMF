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
using TMG.Functions.VectorHelper;
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
            Assert.AreEqual(2.0f*testArray.Length, VectorSum(testArray, 0, testArray.Length), 0.000001f);
        }

        [TestMethod]
        public void TestVectorSquareDiff()
        {
            float[] first = Enumerable.Range(1, 100).Select(p => 2.0f).ToArray();
            float[] second = Enumerable.Range(1, 100).Select(p => 4.0f).ToArray();
            Assert.AreEqual(4.0f * first.Length, VectorSquareDiff(first, 0, second, 0, first.Length), 0.000001f);
        }

        [TestMethod]
        public void TestVectorMultiply2Vectors()
        {
            float[] first = Enumerable.Range(1, 100).Select(p => 2.0f).ToArray();
            float[] second = Enumerable.Range(1, 100).Select(p => 4.0f).ToArray();
            VectorMultiply(first, 0, first, 0, second, 0, first.Length);
            for(int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(8.0f, first[i], 0.000001f);
            }
        }

        [TestMethod]
        public void TestVectorMultiply1V1S()
        {
            float[] first = Enumerable.Range(1, 100).Select(p => 2.0f).ToArray();
            VectorMultiply(first, 0, first, 0, -1.0f, first.Length);
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
            VectorMultiply(first, 0, first, 0, second, 0, third, 0, fourth, 0, first.Length);
            for(int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(-30.0f, first[i], 0.000001f);
            }
        }

        [TestMethod]
        public void TestVectirMultiplyAndSum2V()
        {
            float[] first = Enumerable.Range(1, 100).Select(p => 2.0f).ToArray();
            float[] second = Enumerable.Range(1, 100).Select(p => 3.0f).ToArray();
            var total = VectorMultiplyAndSum(first, 0, first, 0, second, 0, first.Length);
            Assert.AreEqual(600.0f, total, 0.00001f);
        }
    }
}
