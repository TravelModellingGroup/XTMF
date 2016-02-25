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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

using TMG;
using TMG.Data;


namespace XTMF.Testing.TMG.Data
{
    [TestClass]
    public class TestZoneMap
    {
        [TestMethod]
        public void CreateValidMap()
        {
            // 3,2,1 three times
            var zones = new IZone[] { null, null, null, null, null, null, null, null, null };
            var mapping = new int[] { 3, 2, 1, 3, 2, 1, 3, 2, 1 };
            var map = ZoneMap.CreateZoneMap(zones, mapping);
            Assert.IsNotNull(map);
            for (int i = 0; i < mapping.Length; i++)
            {
                Assert.AreEqual(mapping[i], map.Map[i]);
            }
            Assert.AreEqual(3, map.MapValues.Count);
            foreach (var mapKey in map.MapValues)
            {
                var containedZoneIndexes = map.KeyToZoneIndex[mapKey];
                for (int i = 0; i < containedZoneIndexes.Count; i++)
                {
                    Assert.AreEqual(mapping[containedZoneIndexes[i]], mapKey);
                }
            }
        }

        [TestMethod]
        public void TestBadArguments()
        {
            var zones = new IZone[] { null, null, null, null, null, null, null, null, null };
            var mapping = new int[] { 3, 2, 1, 3, 2, 1, 3, 2, 1 };
            ExpectNullArgumentException(() => { ZoneMap.CreateZoneMap(null, null); });
            ExpectNullArgumentException(() => { ZoneMap.CreateZoneMap(zones, null); });
            ExpectNullArgumentException(() => { ZoneMap.CreateZoneMap(null, mapping); });
        }


        /// <summary>
        /// Used to test null argument exception is being thrown
        /// </summary>
        /// <param name="a"></param>
        private static void ExpectNullArgumentException(Action a)
        {
            bool caught = false;
            try
            {
                a();
            }
            catch (ArgumentNullException)
            {
                caught = true;
            }
            Assert.IsTrue(caught, "The expected exception was not caught!");
        }
    }
}
