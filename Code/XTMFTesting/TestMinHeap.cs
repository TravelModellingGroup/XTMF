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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace XTMF.Testing
{
    [TestClass]
    public class TestMinHeap
    {
        [TestMethod]
        public void TestConstructor()
        {
            int[] testMe = new int[] { 5, 3, 4, 7, 2, 1, 9 };
            int[] sorted = new int[] { 1, 2, 3, 4, 5, 7, 9 };
            var heap = new MinHeap<int>( testMe );
            for ( int i = 0; i < sorted.Length; i++ )
            {
                var newItem = heap.Pop();
                if ( newItem != sorted[i] )
                {
                    Assert.Fail( "Not in order!" );
                }
            }
        }

        [TestMethod]
        public void TestAdd()
        {
            int[] testMe = new int[] { 5, 3, 4, 7, 2, 1, 9 };
            int[] sorted = new int[] { 1, 2, 3, 4, 5, 7, 9 };
            var heap = new MinHeap<int>();
            for ( int i = 0; i < testMe.Length; i++ )
            {
                heap.Add( testMe[i] );
            }
            for ( int i = 0; i < sorted.Length; i++ )
            {
                var newItem = heap.Pop();
                if ( newItem != sorted[i] )
                {
                    Assert.Fail( "Not in order!" );
                }
            }
        }
    }
}
