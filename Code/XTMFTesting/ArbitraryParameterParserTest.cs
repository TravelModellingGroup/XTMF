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
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace XTMF.Testing
{
    [TestClass]
    public class ArbitraryParameterParserTest
    {
        [TestMethod]
        public void TestCharParsing()
        {
            string error = null;
            var obj = ArbitraryParameterParser.ArbitraryParameterParse( typeof( char ), "1", ref error );
            if ( !( obj is char ) )
            {
                Assert.Fail( "We should be able to parse a number!" );
            }
            obj = ArbitraryParameterParser.ArbitraryParameterParse( typeof( char ), "as", ref error );
            if ( obj != null || error == null )
            {
                Assert.Fail( "We need to make sure that invalid data doesn't get parsed" );
            }
        }

        [TestMethod]
        public void TestDateTimeParsing()
        {
            string error = null;
            var obj = ArbitraryParameterParser.ArbitraryParameterParse( typeof( DateTime ), "10:00 AM", ref error );
            if ( !( obj is DateTime ) )
            {
                Assert.Fail( "We should be able to parse a DateTime!" );
            }
            obj = ArbitraryParameterParser.ArbitraryParameterParse( typeof( DateTime ), "123211231:123:321", ref error );
            if ( obj != null || error == null )
            {
                Assert.Fail( "We need to make sure that invalid data doesn't get parsed" );
            }
        }

        [TestMethod]
        public void TestFloatParsing()
        {
            string error = null;
            var obj = ArbitraryParameterParser.ArbitraryParameterParse( typeof( float ), "12345.123", ref error );
            if ( !( obj is float ) )
            {
                Assert.Fail( "We should be able to parse a number!" );
            }
            obj = ArbitraryParameterParser.ArbitraryParameterParse( typeof( float ), "asdbasd.321", ref error );
            if ( obj != null || error == null )
            {
                Assert.Fail( "We need to make sure that invalid data doesn't get parsed" );
            }
        }

        [TestMethod]
        public void TestIntegerParsing()
        {
            string error = null;
            var obj = ArbitraryParameterParser.ArbitraryParameterParse( typeof( int ), "12345", ref error );
            if ( !( obj is int ) )
            {
                Assert.Fail( "We should be able to parse a number!" );
            }
            obj = ArbitraryParameterParser.ArbitraryParameterParse( typeof( int ), "asdbasd", ref error );
            if ( obj != null || error == null )
            {
                Assert.Fail( "We need to make sure that invalid data doesn't get parsed" );
            }
        }

        [TestMethod]
        public void TestStringParsing()
        {
            string error = null;
            var obj = ArbitraryParameterParser.ArbitraryParameterParse( typeof( string ), "12345.123", ref error );
            if ( !( obj is string ) )
            {
                Assert.Fail( "We should be able to parse a string!" );
            }
            obj = ArbitraryParameterParser.ArbitraryParameterParse( typeof( string ), "asdbasd.321", ref error );
            if ( obj == null || error != null )
            {
                Assert.Fail( "Strings should always be \"parsed\"" );
            }
        }

        [TestMethod]
        public void TestTestStructParsing()
        {
            string error = null;
            var obj = ArbitraryParameterParser.ArbitraryParameterParse( typeof( TestStruct ), "10:00 AM", ref error );
            if ( !( obj is TestStruct ) )
            {
                Assert.Fail( "We should be able to parse a TestStruct!" );
            }
            obj = ArbitraryParameterParser.ArbitraryParameterParse( typeof( TestStruct ), "", ref error );
            if ( obj != null || error == null )
            {
                Assert.Fail( "We need to make sure that invalid data doesn't get parsed" );
            }
        }

        [TestMethod]
        public void TestXTMFTimeParsing()
        {
            string error = null;
            var obj = ArbitraryParameterParser.ArbitraryParameterParse( typeof( Time ), "10:00 AM", ref error );
            if ( !( obj is Time ) )
            {
                Assert.Fail( "We should be able to parse a DateTime!" );
            }
            obj = ArbitraryParameterParser.ArbitraryParameterParse( typeof( Time ), "inverse:polynomial:321", ref error );
            if ( obj != null || error == null )
            {
                Assert.Fail( "We need to make sure that invalid data doesn't get parsed" );
            }
        }

        private struct TestStruct
        {
            public static bool TryParse(ref string error, string input, out TestStruct output)
            {
                output = default( TestStruct );
                if ( input.Length > 0 )
                {
                    return true;
                }
                error = "Length needs to be greater than zero!";
                return false;
            }
        }
    }
}