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

namespace XTMF.Testing
{
    [TestClass]
    public class TestSparseArray
    {
        public int[][] GeneratePositions(int dimensions, int length)
        {
            int[][] Position = new int[dimensions][];
            Random r = new Random();
            // Setup the data initially
            for ( int dim = 0; dim < dimensions; dim++ )
            {
                Position[dim] = new int[length];
                // initial setup of the positions
                for ( int i = 0; i < length; i++ )
                {
                    Position[dim][i] = i;
                }
                // do a card shuffle algo
                for ( int i = 0; i < length; i++ )
                {
                    var selected = r.Next( length - i );
                    var temp = Position[dim][i];
                    Position[dim][i] = Position[dim][selected];
                    Position[dim][selected] = temp;
                }
            }
            return Position;
        }

        [TestMethod]
        public void SparseArrayTest()
        {
            var length = 1000000;
            Random r = new Random();
            int[] BaseData = new int[length];
            int[][] Position = GeneratePositions( 1, length );
            for ( int i = 0; i < length; i++ )
            {
                BaseData[i] = r.Next();
            }
            var sparseData = SparseArray<int>.CreateSparseArray( Position[0], BaseData );
            for ( int i = 0; i < length; i++ )
            {
                if ( BaseData[i] != sparseData[Position[0][i]] )
                {
                    Assert.Fail( String.Format( "{0} != {1}", BaseData[Position[0][i]], sparseData[Position[0][i]] ) );
                }
            }
        }

        [TestMethod]
        public void SparseTriIndexFlatTest()
        {
            var length = 1000000;
            Random r = new Random();
            int[] BaseData = new int[length];
            int[][] Position = GeneratePositions( 3, length );
            for ( int i = 0; i < length; i++ )
            {
                BaseData[i] = r.Next();
            }
            var sparseData = SparseTriIndex<int>.CreateSparseTriIndex( Position[0], Position[1], Position[2], BaseData );
            for ( int dataPoint = 0; dataPoint < length; dataPoint++ )
            {
                int i = Position[0][dataPoint], j = Position[1][dataPoint], k = Position[2][dataPoint];

                if ( !sparseData.GetFlatIndex( ref i, ref j, ref k ) )
                {
                    Assert.Fail( "Valid position, {0}:{1}:{2} was deemed invalid", Position[0][dataPoint], Position[1][dataPoint], Position[2][dataPoint] );
                }
            }
        }

        [TestMethod]
        public void SparseTriIndexTest()
        {
            var length = 1000000;
            Random r = new Random();
            int[] BaseData = new int[length];
            int[][] Position = GeneratePositions( 3, length );
            for ( int i = 0; i < length; i++ )
            {
                BaseData[i] = r.Next();
            }
            var sparseData = SparseTriIndex<int>.CreateSparseTriIndex( Position[0], Position[1], Position[2], BaseData );
            for ( int i = 0; i < length; i++ )
            {
                if ( BaseData[i] != sparseData[Position[0][i], Position[1][i], Position[2][i]] )
                {
                    Assert.Fail( String.Format( "{0} != {1}", BaseData[i], sparseData[Position[0][i], Position[1][i], Position[2][i]] ) );
                }
            }
        }

        [TestMethod]
        public void SparseTriIndexIndexingTest()
        {
            var length = 1000000;
            Random r = new Random();
            int[] BaseData = new int[length];
            int[][] Position = GeneratePositions( 3, length );
            for ( int i = 0; i < length; i++ )
            {
                BaseData[i] = r.Next();
                Position[0][i] *= 2;
                Position[1][i] *= 1;
                Position[2][i] *= 2;
            }
            var sparseData = SparseTriIndex<int>.CreateSparseTriIndex( Position[0], Position[1], Position[2], BaseData );
            for ( int i = 0; i < length; i++ )
            {
                if ( BaseData[i] != sparseData[Position[0][i], Position[1][i], Position[2][i]] )
                {
                    Assert.Fail( String.Format( "{0} != {1}", BaseData[i], sparseData[Position[0][i], Position[1][i], Position[2][i]] ) );
                }
            }
        }

        [TestMethod]
        public void SparseTwinIndexTest()
        {
            var length = 1000000;
            Random r = new Random();
            int[] BaseData = new int[length];
            int[][] Position = GeneratePositions( 2, length );
            for ( int i = 0; i < length; i++ )
            {
                BaseData[i] = r.Next();
            }
            var sparseData = SparseTwinIndex<int>.CreateTwinIndex( Position[0], Position[1], BaseData );
            for ( int i = 0; i < length; i++ )
            {
                if ( BaseData[i] != sparseData[Position[0][i], Position[1][i]] )
                {
                    Assert.Fail( String.Format( "{0} != {1}", BaseData[i], sparseData[Position[0][i], Position[1][i]] ) );
                }
            }
        }

        [TestMethod]
        public void CreateSquareTwinArrayTest()
        {
            var length = 1000;
            Random r = new Random();
            int[] BaseData = new int[length * length];
            int[][] Position = GeneratePositions( 1, length );
            for ( int i = 0; i < length; i++ )
            {
                BaseData[i] = r.Next();
            }
            var sparseArray = SparseArray<int>.CreateSparseArray( Position[0], new int[length] );
            var twinArray = sparseArray.CreateSquareTwinArray<int>();
            for ( int i = 0; i < length; i++ )
            {
                for ( int j = 0; j < length; j++ )
                {
                    twinArray[Position[0][i], Position[0][j]] = BaseData[i * length + j];
                }
            }
            for ( int i = 0; i < length; i++ )
            {
                for ( int j = 0; j < length; j++ )
                {
                    if ( BaseData[i * length + j] != twinArray[Position[0][i], Position[0][j]] )
                    {
                        Assert.Fail( String.Format( "{0} != {1}", BaseData[i], twinArray[Position[0][i], Position[0][j]] ) );
                    }
                }
            }
        }

        [TestMethod]
        public void LookupPerformanceTwinArray()
        {
            var length = 1000;
            Random r = new Random();
            int[] BaseData = new int[length * length];
            int[][] Position = GeneratePositions( 1, length );
            for ( int i = 0; i < length; i++ )
            {
                BaseData[i] = r.Next();
            }
            var sparseArray = SparseArray<int>.CreateSparseArray( Position[0], new int[length] );
            var twinArray = sparseArray.CreateSquareTwinArray<int>();
            for ( int i = 0; i < length; i++ )
            {
                for ( int j = 0; j < length; j++ )
                {
                    twinArray[Position[0][i], Position[0][j]] = BaseData[i * length + j];
                }
            }
            Stopwatch watch = Stopwatch.StartNew();
            {
                for ( int i = 0; i < length; i++ )
                {
                    for ( int j = 0; j < length; j++ )
                    {
                        if ( BaseData[i * length + j] != twinArray[Position[0][i], Position[0][j]] )
                        {
                            Assert.Fail( String.Format( "{0} != {1}", BaseData[i], twinArray[Position[0][i], Position[0][j]] ) );
                        }
                    }
                }
            }
            watch.Stop();
            Console.WriteLine(watch.ElapsedMilliseconds + "ms");
        }
    }
}