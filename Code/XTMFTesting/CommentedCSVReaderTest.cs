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
using System.IO;
using Datastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace XTMF.Testing
{
    [TestClass]
    public class CommentedCSVReaderTest
    {
        private const int ExpectedNumberOfLines = 100;
        private const int FailNumberOfLines = 5;

        [TestInitialize]
        public void CreateTestEnvironment()
        {
            if ( !IsEnvironmentLoaded() )
            {
                using ( StreamWriter writer = new StreamWriter( "CommentedCSVReaderTest.csv" ) )
                {
                    writer.WriteLine( "Zone,Age,EmpStat,OccGroup,Value" );
                    for ( int i = 0; i < ExpectedNumberOfLines; i++ )
                    {
                        writer.WriteLine( "1,2,3,4,5" );
                    }
                }

                using ( StreamWriter writer = new StreamWriter( "CommentedCSVReaderTestFail.csv" ) )
                {
                    writer.WriteLine( "Zone,Age,EmpStat,OccGroup,Value" );
                    for ( int i = 0; i < FailNumberOfLines; i++ )
                    {
                        writer.WriteLine( "1,2,3,4,5" );
                    }
                    writer.WriteLine( "1,2,3,4" );
                }
            }
        }

        public bool IsEnvironmentLoaded()
        {
            return File.Exists( "CommentedCSVReaderTest.csv" ) & File.Exists( "CommentedCSVReaderTestFail.csv" );
        }

        [TestMethod]
        public void TestLoad()
        {
            using ( var reader = new CommentedCsvReader( "CommentedCSVReaderTest.csv" ) )
            {
                /*
                string[] truth = new string[] {"Zone", "Age", "EmpStat", "OccGroup", "Value"};
                for (int i = 0; i < reader.Headers.Length; i++)
                {
                    Assert.IsTrue(truth[i] == reader.Headers[i]);
                }*/
                int lines = 0;
                while ( reader.NextLine() )
                {
                    if ( reader.NumberOfCurrentCells > 0 )
                    {
                        Assert.AreEqual( 5, reader.NumberOfCurrentCells );
                        reader.Get(out float val, 5);
                        lines++;
                    }
                }
                Assert.AreEqual( ExpectedNumberOfLines, lines );
            }
            using ( var reader = new CommentedCsvReader( "CommentedCSVReaderTestFail.csv" ) )
            {
                for ( int i = 0; i < FailNumberOfLines; i++ )
                {
                    reader.NextLine();
                }

                bool failed = false;
                try
                {
                    reader.NextLine();
                }
                catch ( IOException )
                {
                    failed = true;
                }

                Assert.IsTrue( failed );
            }
        }
    }
}