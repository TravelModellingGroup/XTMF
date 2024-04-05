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
using System.Text;
using Datastructure;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace XTMF.Testing;

[TestClass]
public class ODCTesting
{
    [TestMethod]
    public void Test311ODC()
    {
        try
        {
            int[] zones = [0, 1, 2, 3, 4, 5, 6];
            SparseArray<int> referenceArray = new( new SparseIndexing()
            {
                Indexes =
            [
                new SparseSet() { Start = 0, Stop = 6 }
            ]
            } );
            float[][][] allData = new float[1][][];
            var data = CreateData( zones.Length );
            Create311File( zones, data, "Test.311" );
            allData[0] = data;
            var writer = new OdMatrixWriter<int>( referenceArray, 1, 1 );
            writer.LoadEmme2( "Test.311", 0 );
            writer.Save( "Test.odc", false );
            var odcFloatData = ConvertData( allData, zones.Length, 1, 1 );
            ValidateData( zones, odcFloatData, "Test.odc" );
        }
        finally
        {
            File.Delete( "Test.311" );
            File.Delete( "Test.odc" );
        }
    }

    [TestMethod]
    // ReSharper disable once InconsistentNaming
    public void TestCSVODC()
    {
        try
        {
            int[] zones = [0, 1, 2, 3, 4, 5, 6];
            SparseArray<int> referenceArray = new( new SparseIndexing()
            {
                Indexes =
            [
                new SparseSet() { Start = 0, Stop = 6 }
            ]
            } );
            float[][][] allData = new float[1][][];
            var data = CreateData( zones.Length );
            CreateCSVFile( zones, data, "Test.csv" );
            allData[0] = data;
            var writer = new OdMatrixWriter<int>( referenceArray, 1, 1 );
            writer.LoadCsvTimes( "Test.csv", false, 0, 0 );
            writer.Save( "Test.odc", false );
            var odcFloatData = ConvertData( allData, zones.Length, 1, 1 );
            ValidateData( zones, odcFloatData, "Test.odc" );
        }
        finally
        {
            File.Delete( "Test.csv" );
            File.Delete( "Test.odc" );
        }
    }

    [TestMethod]
    public void TestMultiTime311ODC()
    {
        float[][][] allData = new float[4][][];
        try
        {
            int[] zones = [0, 1, 2, 3, 4, 5, 6];
            SparseArray<int> referenceArray = new( new SparseIndexing()
            {
                Indexes =
            [
                new SparseSet() { Start = 0, Stop = 6 }
            ]
            } );

            for ( int i = 0; i < allData.Length; i++ )
            {
                var data = CreateData( zones.Length );
                Create311File( zones, data, "Test" + i + ".311" );
                allData[i] = data;
            }
            var writer = new OdMatrixWriter<int>( referenceArray, allData.Length, 1 );
            for ( int i = 0; i < allData.Length; i++ )
            {
                writer.LoadEmme2( "Test" + i + ".311", i, 0 );
            }
            writer.Save( "Test.odc", false );
            var odcFloatData = ConvertData( allData, zones.Length, allData.Length, 1 );
            ValidateData( zones, odcFloatData, "Test.odc" );
        }
        finally
        {
            for ( int i = 0; i < allData.Length; i++ )
            {
                File.Delete( "Test" + i + ".311" );
            }
            File.Delete( "Test.odc" );
        }
    }

    [TestMethod]
    public void TestMultiTimeTypes311ODC()
    {
        int times = 3, types = 2;
        float[][][] allData = new float[times * types][][];
        try
        {
            int[] zones = [0, 1, 2, 3, 4, 5, 6];
            SparseArray<int> referenceArray = new( new SparseIndexing()
            {
                Indexes =
            [
                new SparseSet() { Start = 0, Stop = 6 }
            ]
            } );

            for ( int i = 0; i < allData.Length; i++ )
            {
                var data = CreateData( zones.Length );
                Create311File( zones, data, "Test" + i + ".311" );
                allData[i] = data;
            }
            var writer = new OdMatrixWriter<int>( referenceArray, types, times);
            for ( int i = 0; i < types; i++ )
            {
                for ( int j = 0; j < times; j++ )
                {
                    writer.LoadEmme2( "Test" + ( i * times + j ) + ".311", j, i);
                }
            }
            writer.Save( "Test.odc", false );
            var odcFloatData = ConvertData( allData, zones.Length, times, types );
            ValidateData( zones, odcFloatData, "Test.odc" );
        }
        finally
        {
            for ( int i = 0; i < allData.Length; i++ )
            {
                File.Delete( "Test" + i + ".311" );
            }
            File.Delete( "Test.odc" );
        }
    }

    private float[][][] ConvertData(float[][][] allData, int numberOfZones, int times, int types)
    {
        float[][][] ret = new float[numberOfZones][][];
        Parallel.For( 0, ret.Length, delegate(int i)
        {
            ret[i] = new float[numberOfZones][];
            for ( int j = 0; j < ret[i].Length; j++ )
            {
                ret[i][j] = new float[times * types];
                for ( int k = 0; k < ret[i][j].Length; k++ )
                {
                    ret[i][j][k] = allData[k][i][j];
                }
            }
        } );
        return ret;
    }

    private void Create311File(int[] zones, float[][] data, string fileName)
    {
        using var writer = new StreamWriter(fileName);
        var numberOfZones = zones.Length;
        StringBuilder builder = new();
        builder.EnsureCapacity(100);
        char[] buff = new char[100];
        writer.WriteLine("c Emme Modeller - Matrix Transaction");
        writer.WriteLine("c Date: 2012-12-21 10:23:52");
        writer.WriteLine("c Project:        durham model");
        writer.WriteLine("t matrices");
        writer.WriteLine("a matrix=mf13   acost   0.0 Auto cost matrix ($) ");
        for (int i = 0; i < numberOfZones; i++)
        {
            for (int j = 0; j < numberOfZones; j++)
            {
                builder.AppendFormat("{0,7}", zones[i]);
                builder.AppendFormat("{0,7} ", zones[j]);
                builder.AppendFormat("{0,9}", data[i][j]);
                var size = builder.Length;
                if (size >= buff.Length)
                {
                    buff = new char[size * 2];
                }
                builder.CopyTo(0, buff, 0, size);
                writer.WriteLine(buff, 0, size);
                builder.Clear();
            }
        }
    }

    private void CreateCSVFile(int[] zones, float[][] data, string fileName)
    {
        using var writer = new StreamWriter(fileName);
        var numberOfZones = zones.Length;
        StringBuilder builder = new();
        builder.EnsureCapacity(100);
        char[] buff = new char[100];
        for (int i = 0; i < numberOfZones; i++)
        {
            for (int j = 0; j < numberOfZones; j++)
            {
                builder.Append(zones[i]);
                builder.Append(',');
                builder.Append(zones[j]);
                builder.Append(',');
                builder.Append(data[i][j]);
                var size = builder.Length;
                if (size >= buff.Length)
                {
                    buff = new char[size * 2];
                }
                builder.CopyTo(0, buff, 0, size);
                writer.WriteLine(buff, 0, size);
                builder.Clear();
            }
        }
    }

    private float[][] CreateData(int numberOfZones)
    {
        Random r = new();
        var ret = new float[numberOfZones][];
        for ( int i = 0; i < numberOfZones; i++ )
        {
            ret[i] = new float[numberOfZones];
            for ( int j = 0; j < numberOfZones; j++ )
            {
                ret[i][j] = ( (int)( r.NextDouble() * 1000 ) / 1000f );
            }
        }
        return ret;
    }

    private void ValidateData(int[] zones, float[][][] data, string odcFileName)
    {
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(data);
        OdCache odc = new( odcFileName );
        var storedData = odc.StoreAll().GetFlatData();
        odc.Release();
        if ( storedData.Length != data.Length )
        {
            Assert.Fail( "The stored data is not the same size as the given data! " + storedData.Length + " : " + data.Length );
        }
        for ( int i = 0; i < storedData.Length; i++ )
        {
            if (data[i] == null)
            {
                Assert.Fail("No data provided for data[i]");
            }
            if ( ( storedData[i] == null ) != ( data[i] == null ) )
            {
                Assert.Fail( "The data differs at zone " + zones[i] );
            }
            if ( storedData[i] == null ) continue;
            if ( storedData[i].Length != data[i].Length )
            {
                Assert.Fail( "The stored data is not the same size as the given data for zone " + zones[i] + "! " + storedData[i].Length + " : " + data[i].Length );
            }
            for ( int j = 0; j < storedData[i].Length; j++ )
            {
                if (data[i][j] == null)
                {
                    Assert.Fail("No data provided for data[i][j]");
                }
                if ( ( storedData[i][j] == null ) != ( data[i][j] == null ) )
                {
                    Assert.Fail( "The data differs at zone " + zones[i] + " in zone " + zones[j] );
                }
                if (storedData[i][j] != null)
                {
                    for (int k = 0; k < storedData[i][j].Length; k++)
                    {
                        if (storedData[i][j][k] != data[i][j][k])
                        {
                            if (Math.Round(storedData[i][j][k], 5) != Math.Round(data[i][j][k], 5))
                            {
                                Assert.Fail("The data differs at index " + i + ":" + j + ":" + k + " (" +
                                            storedData[i][j][k] + " / " + data[i][j][k] + ")");
                            }
                        }
                    }
                }
            }
        }
    }
}