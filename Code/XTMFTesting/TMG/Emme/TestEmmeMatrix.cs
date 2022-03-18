/*
    Copyright 2022 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using TMG.Emme;
using TMG.Functions;

namespace XTMF.Testing.TMG.Emme
{
    [TestClass]
    public class TestEmmeMatrix
    {
        [TestMethod]
        public void TestSaveMatrixCompressed()
        {
            int[] zones = CreateZones(2000);
            float[][] data = CreateIdentityMatrix(zones);
            EmmeMatrix matrix = new EmmeMatrix(zones, data);
            matrix.Save("matrix.mtx", false);
            matrix.Save("matrix.mtx.gz", false);
            FileInfo uncompressed = new FileInfo("matrix.mtx");
            FileInfo compressed = new FileInfo("matrix.mtx.gz");
            Assert.IsTrue(uncompressed.Length >  compressed.Length);
        }

        [TestMethod]
        public void TestLoadMatrixCompressed()
        {
            int[] zones = CreateZones(2000);
            float[][] data = CreateIdentityMatrix(zones);
            EmmeMatrix matrix = new EmmeMatrix(zones, data);
            matrix.Save("matrix.mtx.gz", false);
            EmmeMatrix loaded = default;
            BinaryHelpers.ExecuteReader(null, (reader) =>
            {
                loaded = new EmmeMatrix(reader);
            }, "matrix.mtx.gz");
            Assert.AreEqual(2, loaded.Dimensions);
            Assert.AreEqual(zones.Length * zones.Length, loaded.FloatData.Length);
            for (int i = 0; i < zones.Length; i++)
            {
                for (int j = 0; j < zones.Length; j++)
                {
                    if (loaded.FloatData[i * zones.Length + j] != (i == j ? 1.0f : 0.0f))
                    {
                        Assert.Fail("The matrix was not loaded back correctly!");
                    }
                }
            }
        }

        private float[][] CreateIdentityMatrix(int[] zones)
        {
            var ret = new float[zones.Length][];
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = new float[zones.Length];
                ret[i][i] = 1;
            }
            return ret;
        }

        private static int[] CreateZones(int numberOfZones)
        {
            var ret = new int[numberOfZones];
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = i + 1;
            }
            return ret;
        }
    }
}
