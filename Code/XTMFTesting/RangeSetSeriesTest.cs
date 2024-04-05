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
using Range = Datastructure.Range;

namespace XTMF.Testing;

/// <summary>
///This is a test class for RangeSetSeriesTest and is intended
///to contain all RangeSetSeriesTest Unit Tests
///</summary>
[TestClass()]
public class RangeSetSeriesTest
{

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
        RangeSetSeries target = new( tempRange );
        Assert.AreEqual( target.Count, 2 );
    }

    /// <summary>
    ///A test for ToString
    ///</summary>
    [TestMethod()]
    public void ToStringTest()
    {
        List<RangeSet> tempRange = GenerateTempRange();
        RangeSetSeries target = new( tempRange ); // TODO: Initialize to an appropriate value
        string expected = "{1-2,4-5},{11-12,14-15}"; // TODO: Initialize to an appropriate value
        var actual = target.ToString();
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
                                                       // TODO: Initialize to an appropriate value
        bool actual = RangeSetSeries.TryParse(ref error, rangeString, out RangeSetSeries output);
        Assert.IsNotNull( error );
        Assert.AreEqual( null, output );
        Assert.AreEqual( false, actual );
    }

    /// <summary>
    ///A test for TryParse
    ///</summary>
    [TestMethod()]
    public void TryParseTestNonError()
    {
        string rangeString = "{1-2,4-5},{11-12,14-15}"; // TODO: Initialize to an appropriate value
                                                        // TODO: Initialize to an appropriate value
        RangeSetSeries outputExpected = new(GenerateTempRange()); // TODO: Initialize to an appropriate value
        var actual = RangeSetSeries.TryParse( rangeString, out RangeSetSeries output );
        Assert.AreEqual( outputExpected, output );
        Assert.AreEqual( true, actual );
    }

    /// <summary>
    ///A test for TryParse
    ///</summary>
    [TestMethod()]
    public void TryParseTestSuccess()
    {
        string error = null; // TODO: Initialize to an appropriate value
        string rangeString = "{1-2,4-5},{11-12,14-15}"; // TODO: Initialize to an appropriate value
                                                        // TODO: Initialize to an appropriate value
        RangeSetSeries outputExpected = new(GenerateTempRange()); // TODO: Initialize to an appropriate value
        var actual = RangeSetSeries.TryParse( ref error, rangeString, out RangeSetSeries output );
        Assert.AreEqual( null, error );
        Assert.AreEqual( outputExpected, output );
        Assert.AreEqual( true, actual );
    }

    private static List<RangeSet> GenerateTempRange()
    {
        List<RangeSet> tempRange =
        [
            new RangeSet(new List<Range>()
            {
                new(1, 2),
                new(4, 5)
            }),
            new RangeSet(new List<Range>()
            {
                new(11, 12),
                new(14, 15)
            })
        ];
        return tempRange;
    }
}