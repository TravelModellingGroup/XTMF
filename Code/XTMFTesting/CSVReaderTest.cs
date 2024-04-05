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
using System.IO;
using System.IO.Compression;
using Datastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace XTMF.Testing
{
    [TestClass]
    public class CSVReaderTest
    {
        private string[] TestCSVFileNames = new[] { "CSVTest1.csv", "CSVTest2.csv", "CSVTest3.csv", "CSVTest4.csv", "CSVTest5.csv", "CSVTest6.csv.gz" };

        [TestInitialize]
        public void CreateTestEnvironment()
        {
            if ( !IsEnvironmentLoaded() )
            {
                using ( StreamWriter writer = new StreamWriter( TestCSVFileNames[0] ) )
                {
                    writer.WriteLine( "A,B,C,D,E" );
                    writer.WriteLine( "1,2,3,4,5" );
                    writer.WriteLine( "3,1,4,5,2" );
                    writer.WriteLine( "1.23,4.56,7.89,10.1112,0.1314" );
                }
                using ( StreamWriter writer = new StreamWriter( TestCSVFileNames[1] ) )
                {
                    writer.WriteLine( "\"A\",\"B\",\"C\",\"D\",\"E\"" );
                    writer.WriteLine( "\"1\",\"2\",3,\"4\",5" );
                    writer.WriteLine( "3,1,\"4\",5,2" );
                    writer.WriteLine( "1.23,\"4.56\",7.89,10.1112,0.1314" );
                }
                using ( StreamWriter writer = new StreamWriter( TestCSVFileNames[2] ) )
                {
                    writer.WriteLine( "A,B,C,D,E" );
                    writer.WriteLine( "1,2,3,4,5" );
                    writer.WriteLine( "3,1,4,5,2" );
                    writer.WriteLine( "1.23,4.56,7.89,10.1112,0.1314" );
                }
                using (StreamWriter writer = new StreamWriter(TestCSVFileNames[3]))
                {
                    writer.WriteLine("A,B,C,D,E");
                    writer.WriteLine("1,2,3,4,5");
                    writer.WriteLine("3,1,4,5,2");
                    writer.Write("1.23,4.56,7.89,10.1112,0.1314");
                }
                using (StreamWriter writer = new StreamWriter(TestCSVFileNames[4]))
                {
                    writer.WriteLine("A,B,C,D,E");
                    writer.WriteLine("\"abc\"\"1\",2,3,4,5");
                }
                using (StreamWriter writer = new StreamWriter(new GZipStream(File.Open(TestCSVFileNames[5], FileMode.Create, FileAccess.Write), CompressionMode.Compress)))
                {
                    writer.WriteLine("A,B,C,D,E");
                    writer.WriteLine("1,2,3,4,5");
                    writer.WriteLine("3,1,4,5,2");
                    writer.WriteLine("1.23,4.56,7.89,10.1112,0.1314");
                }
            }
        }

        public bool IsEnvironmentLoaded()
        {
            for ( int i = 0; i < TestCSVFileNames.Length; i++ )
            {
                if ( !File.Exists( TestCSVFileNames[i] ) ) return false;
            }
            return true;
        }

        [TestMethod]
        public void TestCSVReadLine()
        {
            using CsvReader reader = new CsvReader(TestCSVFileNames[0]);
            reader.LoadLine();
            //"A,B,C,D,E"
            for (int i = 0; i < 5; i++)
            {
                reader.Get(out string s, i);
                Assert.AreEqual(new String((char)('A' + i), 1), s);
            }
        }

        [TestMethod]
        public void TestQuotes()
        {
            using CsvReader reader = new CsvReader(TestCSVFileNames[1]);
            // first line
            reader.LoadLine();
            //"A,B,C,D,E"
            for (int i = 0; i < 5; i++)
            {
                reader.Get(out string s, i);
                Assert.AreEqual(new String((char)('A' + i), 1), s);
            }
            //"\"1\",\"2\",3,\"4\",5"
            reader.LoadLine();
            for (int i = 0; i < 5; i++)
            {
                reader.Get(out int n, i);
                Assert.AreEqual(i + 1, n);
            }
        }

        [TestMethod]
        public void TestDoubleQuotes()
        {
            using CsvReader reader = new CsvReader(TestCSVFileNames[4]);
            // first line
            reader.LoadLine();
            //"A,B,C,D,E"
            for (int i = 0; i < 5; i++)
            {
                reader.Get(out string s, i);
                Assert.AreEqual(new String((char)('A' + i), 1), s);
            }
            //writer.WriteLine("\"abc\"\"1\",2,3,4,5");
            reader.LoadLine();
            reader.Get(out string firstItem, 0);
            Assert.AreEqual("abc\"1", firstItem);
        }

        [TestMethod]
        public void TestLoadLineBool()
        {
            using CsvReader reader = new CsvReader(TestCSVFileNames[1]);
            int numberOfLines = 0;
            while (reader.LoadLine(out int columns))
            {
                if ((columns == 0) & (numberOfLines != 4))
                {
                    Assert.Fail("There was a blank line besides at the end of the file!");
                }
                else if (columns > 0)
                {
                    Assert.AreEqual(5, columns);
                }
                numberOfLines++;
            }
            Assert.AreEqual(5, numberOfLines);
        }

        [TestMethod]
        public void TestNoEnterLastLine()
        {
            using CsvReader reader = new CsvReader(TestCSVFileNames[3]);
            int numberOfLines = 0;
            while (reader.LoadLine(out int columns))
            {
                if ((columns == 0) & (numberOfLines != 4))
                {
                    Assert.Fail("There was a blank line besides at the end of the file!");
                }
                else if (columns > 0)
                {
                    Assert.AreEqual(5, columns);
                }
                numberOfLines++;
            }
            Assert.AreEqual(4, numberOfLines);
        }

        [TestMethod]
        public void TestLineReadValue()
        {
            using CsvReader reader = new CsvReader(TestCSVFileNames[2]);
            float lastColumnValue = float.NaN;
            while (reader.LoadLine(out int columns))
            {
                if (columns > 0)
                {
                    reader.Get(out lastColumnValue, 4);
                }
            }
            Assert.AreEqual(0.1314f, lastColumnValue);
        }

        [TestMethod]
        public void TestNoEnterLastLineReadValue()
        {
            using CsvReader reader = new CsvReader(TestCSVFileNames[3]);
            int numberOfLines = 0;
            float lastColumnValue = float.NaN;
            while (reader.LoadLine(out int columns))
            {
                if ((columns == 0) & (numberOfLines != 4))
                {
                    Assert.Fail("There was a blank line besides at the end of the file!");
                }
                else if (columns > 0)
                {
                    Assert.AreEqual(5, columns);
                }
                numberOfLines++;
                reader.Get(out lastColumnValue, 4);
            }
            Assert.AreEqual(4, numberOfLines);
            Assert.AreEqual(0.1314f, lastColumnValue);
        }

        [TestMethod]
        public void TestReadingCompressedStream()
        {
            using CsvReader reader = new CsvReader(TestCSVFileNames[5]);
            reader.LoadLine();
            //"A,B,C,D,E"
            for (int i = 0; i < 5; i++)
            {
                reader.Get(out string s, i);
                Assert.AreEqual(new String((char)('A' + i), 1), s);
            }
        }
    }
}