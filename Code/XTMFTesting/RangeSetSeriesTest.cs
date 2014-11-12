/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections.Generic;
using Datastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace XTMF.Testing
{
    /// <summary>
    ///This is a test class for RangeSetSeriesTest and is intended
    ///to contain all RangeSetSeriesTest Unit Tests
    ///</summary>
    [TestClass()]
    public class RangeSetSeriesTest
    {
        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }

            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes

        //
        //You can use the following additional attributes as you write your tests:
        //
        //Use ClassInitialize to run code before running the first test in the class
        //[ClassInitialize()]
        //public static void MyClassInitialize(TestContext testContext)
        //{
        //}
        //
        //Use ClassCleanup to run code after all tests in a class have run
        //[ClassCleanup()]
        //public static void MyClassCleanup()
        //{
        //}
        //
        //Use TestInitialize to run code before running each test
        //[TestInitialize()]
        //public void MyTestInitialize()
        //{
        //}
        //
        //Use TestCleanup to run code after each test has run
        //[TestCleanup()]
        //public void MyTestCleanup()
        //{
        //}
        //

        #endregion Additional test attributes

        /// <summary>
        ///A test for RangeSetSeries Constructor
        ///</summary>
        [TestMethod()]
        public void RangeSetSeriesConstructorTest()
        {
            List<RangeSet> tempRange = GenerateTempRange();
            RangeSetSeries target = new RangeSetSeries( tempRange );
            Assert.AreEqual( target.Count, 2 );
        }

        /// <summary>
        ///A test for ToString
        ///</summary>
        [TestMethod()]
        public void ToStringTest()
        {
            List<RangeSet> tempRange = GenerateTempRange();
            RangeSetSeries target = new RangeSetSeries( tempRange ); // TODO: Initialize to an appropriate value
            string expected = "{1-2,4-5},{11-12,14-15}"; // TODO: Initialize to an appropriate value
            string actual;
            actual = target.ToString();
            Assert.AreEqual( expected, actual );
        }

        /// <summary>
        ///A test for TryParse
        ///</summary>
        [TestMethod()]
        public void TryParseTestFail()
        {
            string error = null; // TODO: Initialize to an appropriate value
            string rangeString = "{1-2,4-5},{11-12,14-15"; // TODO: Initialize to an appropriate value
            RangeSetSeries output = null; // TODO: Initialize to an appropriate value
            RangeSetSeries outputExpected = null; // TODO: Initialize to an appropriate value
            bool expected = false; // TODO: Initialize to an appropriate value
            bool actual;
            actual = RangeSetSeries.TryParse( ref error, rangeString, out output );
            Assert.IsNotNull( error );
            Assert.AreEqual( outputExpected, output );
            Assert.AreEqual( expected, actual );
        }

        /// <summary>
        ///A test for TryParse
        ///</summary>
        [TestMethod()]
        public void TryParseTestNonError()
        {
            string rangeString = "{1-2,4-5},{11-12,14-15}"; // TODO: Initialize to an appropriate value
            RangeSetSeries output = null; // TODO: Initialize to an appropriate value
            RangeSetSeries outputExpected = new RangeSetSeries( GenerateTempRange() ); // TODO: Initialize to an appropriate value
            bool expected = true; // TODO: Initialize to an appropriate value
            bool actual;
            actual = RangeSetSeries.TryParse( rangeString, out output );
            Assert.AreEqual( outputExpected, output );
            Assert.AreEqual( expected, actual );
        }

        /// <summary>
        ///A test for TryParse
        ///</summary>
        [TestMethod()]
        public void TryParseTestSuccess()
        {
            string error = null; // TODO: Initialize to an appropriate value
            string errorExpected = null; // TODO: Initialize to an appropriate value
            string rangeString = "{1-2,4-5},{11-12,14-15}"; // TODO: Initialize to an appropriate value
            RangeSetSeries output = null; // TODO: Initialize to an appropriate value
            RangeSetSeries outputExpected = new RangeSetSeries( GenerateTempRange() ); // TODO: Initialize to an appropriate value
            bool expected = true; // TODO: Initialize to an appropriate value
            bool actual;
            actual = RangeSetSeries.TryParse( ref error, rangeString, out output );
            Assert.AreEqual( errorExpected, error );
            Assert.AreEqual( outputExpected, output );
            Assert.AreEqual( expected, actual );
        }

        private static List<RangeSet> GenerateTempRange()
        {
            List<RangeSet> tempRange = new List<RangeSet>()
            {
                new RangeSet(new List<Range>()
                {
                    new Range() { Start= 1, Stop=2 },
                    new Range() { Start= 4, Stop = 5 }
                }),
                new RangeSet(new List<Range>()
                {
                    new Range() { Start= 11, Stop= 12 },
                    new Range() { Start= 14, Stop = 15 }
                })
            };
            return tempRange;
        }
    }
}