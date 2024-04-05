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
using Datastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TMG.Functions;

namespace XTMF.Testing;

[TestClass]
public class TestGravityModel
{
    [TestMethod]
    public void TestSimpleGravityModelSolution()
    {
        var data = CreateData();
        var gm = new GravityModel( data, null, 0.0f, 300 );
        var o = SparseArray<float>.CreateSparseArray( [1, 2], new float[] { 2, 2 } );
        var d = SparseArray<float>.CreateSparseArray( [1, 2], new[] { 1.5f, 2.5f } );
        var ret = gm.ProcessFlow( o, d, [1, 2] );
        var result = ret.GetFlatData();
        Assert.AreEqual( 0.5f, result[0][0], 0.0001f );
        Assert.AreEqual( 1.5f, result[0][1], 0.0001f);
        Assert.AreEqual( 1f, result[1][0], 0.0001f);
        Assert.AreEqual( 1f, result[1][1], 0.0001f);
    }

    private static SparseTwinIndex<float> CreateData()
    {
        var firstIndex = new[] { 1, 1, 2, 2 };
        var secondIndex = new[] { 1, 2, 1, 2 };
        var data = new[] { 0.25f, 0.75f, 2f, 2f };
        return SparseTwinIndex<float>.CreateTwinIndex( firstIndex, secondIndex, data );
    }
}