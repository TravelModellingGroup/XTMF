/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Range = Datastructure.Range;

namespace XTMF.Testing
{
    [TestClass]
    public class TestRangeSet
    {
        [TestMethod]
        public void TestRangeSetIntList()
        {
            RangeSet rangeSet = new RangeSet(new[] { 1, 2, 3, 4, 5, 7, 8, 9, 10 });
            Assert.AreEqual(2, rangeSet.Count);
            Assert.AreEqual(new Range(1, 5), rangeSet[0]);
            Assert.AreEqual(new Range(7, 10), rangeSet[1]);
        }
    }
}