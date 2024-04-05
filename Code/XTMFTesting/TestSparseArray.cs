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
using System.Diagnostics;
using Datastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace XTMF.Testing;

[TestClass]
public class TestSparseArray
{
    public int[][] GeneratePositions(int dimensions, int length)
    {
        int[][] position = new int[dimensions][];
        Random r = new();
        // Setup the data initially
        for ( int dim = 0; dim < dimensions; dim++ )
        {
            position[dim] = new int[length];
            // initial setup of the positions
            for ( int i = 0; i < length; i++ )
            {
                position[dim][i] = i;
            }
            // do a card shuffle algo
            for ( int i = 0; i < length; i++ )
            {
                var selected = r.Next( length - i );
                var temp = position[dim][i];
                position[dim][i] = position[dim][selected];
                position[dim][selected] = temp;
            }
        }
        return position;
    }

    [TestMethod]
    public void SparseArrayTest()
    {
        var length = 1000000;
        Random r = new();
        int[] baseData = new int[length];
        int[][] position = GeneratePositions( 1, length );
        for ( int i = 0; i < length; i++ )
        {
            baseData[i] = r.Next();
        }
        var sparseData = SparseArray<int>.CreateSparseArray( position[0], baseData );
        for ( int i = 0; i < length; i++ )
        {
            if ( baseData[i] != sparseData[position[0][i]] )
            {
                Assert.Fail($"{baseData[position[0][i]]} != {sparseData[position[0][i]]}");
            }
        }
    }

    [TestMethod]
    public void SparseTriIndexFlatTest()
    {
        var length = 1000000;
        Random r = new();
        int[] baseData = new int[length];
        int[][] position = GeneratePositions( 3, length );
        for ( int i = 0; i < length; i++ )
        {
            baseData[i] = r.Next();
        }
        var sparseData = SparseTriIndex<int>.CreateSparseTriIndex( position[0], position[1], position[2], baseData );
        for ( int dataPoint = 0; dataPoint < length; dataPoint++ )
        {
            int i = position[0][dataPoint], j = position[1][dataPoint], k = position[2][dataPoint];

            if ( !sparseData.GetFlatIndex( ref i, ref j, ref k ) )
            {
                Assert.Fail( "Valid position, {0}:{1}:{2} was deemed invalid", position[0][dataPoint], position[1][dataPoint], position[2][dataPoint] );
            }
        }
    }

    [TestMethod]
    public void SparseTriIndexTest()
    {
        var length = 1000000;
        Random r = new();
        int[] baseData = new int[length];
        int[][] position = GeneratePositions( 3, length );
        for ( int i = 0; i < length; i++ )
        {
            baseData[i] = r.Next();
        }
        var sparseData = SparseTriIndex<int>.CreateSparseTriIndex( position[0], position[1], position[2], baseData );
        for ( int i = 0; i < length; i++ )
        {
            if ( baseData[i] != sparseData[position[0][i], position[1][i], position[2][i]] )
            {
                Assert.Fail($"{baseData[i]} != {sparseData[position[0][i], position[1][i], position[2][i]]}");
            }
        }
    }

    [TestMethod]
    public void SparseTriIndexIndexingTest()
    {
        var length = 1000000;
        Random r = new();
        int[] baseData = new int[length];
        int[][] position = GeneratePositions( 3, length );
        for ( int i = 0; i < length; i++ )
        {
            baseData[i] = r.Next();
            position[0][i] *= 2;
            position[1][i] *= 1;
            position[2][i] *= 2;
        }
        var sparseData = SparseTriIndex<int>.CreateSparseTriIndex( position[0], position[1], position[2], baseData );
        for ( int i = 0; i < length; i++ )
        {
            if ( baseData[i] != sparseData[position[0][i], position[1][i], position[2][i]] )
            {
                Assert.Fail($"{baseData[i]} != {sparseData[position[0][i], position[1][i], position[2][i]]}");
            }
        }
    }

    [TestMethod]
    public void SparseTwinIndexTest()
    {
        var length = 1000000;
        Random r = new();
        int[] baseData = new int[length];
        int[][] position = GeneratePositions( 2, length );
        for ( int i = 0; i < length; i++ )
        {
            baseData[i] = r.Next();
        }
        var sparseData = SparseTwinIndex<int>.CreateTwinIndex( position[0], position[1], baseData );
        for ( int i = 0; i < length; i++ )
        {
            if ( baseData[i] != sparseData[position[0][i], position[1][i]] )
            {
                Assert.Fail($"{baseData[i]} != {sparseData[position[0][i], position[1][i]]}");
            }
        }
    }

    [TestMethod]
    public void SparseTwinIndexFrom2DArrayTest()
    {
        var length = 1000;
        Random r = new();
        int[][] baseData = new int[length][];
        int[] position = new int[length];
        for (int i = 0; i < position.Length; i++)
        {
            position[i] = i * 2 + 1;
        }
        for (int i = 0; i < baseData.Length; i++)
        {
            baseData[i] = new int[length];
            for (int j = 0; j < baseData[i].Length; j++)
            {
                baseData[i][j] = r.Next();
            }
        }
        var sparseData = SparseTwinIndex<int>.CreateSquareTwinIndex(position, baseData);
        for (int i = 0; i < baseData.Length; i++)
        {
            for (int j = 0; j < baseData[i].Length; j++)
            {
                if (baseData[i][j] != sparseData[position[i], position[j]])
                {
                    Assert.Fail($"{baseData[i][j]} != {sparseData[position[i], position[j]]}");
                }
            }
        }
    }

    [TestMethod]
    public void CreateSquareTwinArrayTest()
    {
        var length = 1000;
        Random r = new();
        int[] baseData = new int[length * length];
        int[][] position = GeneratePositions( 1, length );
        for ( int i = 0; i < length; i++ )
        {
            baseData[i] = r.Next();
        }
        var sparseArray = SparseArray<int>.CreateSparseArray( position[0], new int[length] );
        var twinArray = sparseArray.CreateSquareTwinArray<int>();
        for ( int i = 0; i < length; i++ )
        {
            for ( int j = 0; j < length; j++ )
            {
                twinArray[position[0][i], position[0][j]] = baseData[i * length + j];
            }
        }
        for ( int i = 0; i < length; i++ )
        {
            for ( int j = 0; j < length; j++ )
            {
                if ( baseData[i * length + j] != twinArray[position[0][i], position[0][j]] )
                {
                    Assert.Fail($"{baseData[i]} != {twinArray[position[0][i], position[0][j]]}");
                }
            }
        }
    }

    [TestMethod]
    public void LookupPerformanceTwinArray()
    {
        var length = 1000;
        Random r = new();
        int[] baseData = new int[length * length];
        int[][] position = GeneratePositions( 1, length );
        for ( int i = 0; i < length; i++ )
        {
            baseData[i] = r.Next();
        }
        var sparseArray = SparseArray<int>.CreateSparseArray( position[0], new int[length] );
        var twinArray = sparseArray.CreateSquareTwinArray<int>();
        for ( int i = 0; i < length; i++ )
        {
            for ( int j = 0; j < length; j++ )
            {
                twinArray[position[0][i], position[0][j]] = baseData[i * length + j];
            }
        }
        Stopwatch watch = Stopwatch.StartNew();
        {
            for ( int i = 0; i < length; i++ )
            {
                for ( int j = 0; j < length; j++ )
                {
                    if ( baseData[i * length + j] != twinArray[position[0][i], position[0][j]] )
                    {
                        Assert.Fail($"{baseData[i]} != {twinArray[position[0][i], position[0][j]]}");
                    }
                }
            }
        }
        watch.Stop();
        Console.WriteLine(watch.ElapsedMilliseconds + "ms");
    }
}