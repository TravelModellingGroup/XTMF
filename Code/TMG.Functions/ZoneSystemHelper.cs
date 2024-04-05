/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
namespace TMG.Functions;

public static class ZoneSystemHelper
{
    public static SparseArray<T> CreatePdArray<T>(SparseArray<IZone> zoneArray)
    {
        var zones = zoneArray.GetFlatData();
        List<int> pdNumbersFound = new( 10 );
        for ( int i = 0; i < zones.Length; i++ )
        {
            var pdId = zones[i].PlanningDistrict;
            if ( !pdNumbersFound.Contains( pdId ) )
            {
                pdNumbersFound.Add( pdId );
            }
        }
        var pdArray = pdNumbersFound.ToArray();
        return SparseArray<T>.CreateSparseArray( pdArray, new T[pdArray.Length] );
    }

    public static SparseArray<T> CreateRegionArray<T>(SparseArray<IZone> zoneArray)
    {
        var zones = zoneArray.GetFlatData();
        List<int> regionNumbersFound = new(10);
        for(int i = 0; i < zones.Length; i++)
        {
            var regionId = zones[i].RegionNumber;
            if(!regionNumbersFound.Contains(regionId))
            {
                regionNumbersFound.Add(regionId);
            }
        }
        var regionArray = regionNumbersFound.ToArray();
        return SparseArray<T>.CreateSparseArray(regionArray, new T[regionArray.Length]);
    }

    /// <summary>
    /// Check to see if two zone systems share the same zone system
    /// </summary>
    /// <param name="firstMatrix"></param>
    /// <param name="secondMatrix"></param>
    /// <returns>True if they both represent the same indexes, false otherwise.</returns>
    public static bool IsSameZoneSystem(SparseTwinIndex<float> firstMatrix, SparseTwinIndex<float> secondMatrix)
    {
        var first = firstMatrix.Indexes.Indexes;
        var second = secondMatrix.Indexes.Indexes;
        // if they are using the same memory then they must be the same zone system
        if(first == second)
        {
            return true;
        }
        // if they are not we need to look at the contents
        if (first.Length != second.Length)
        {
            return false;
        }
        for (int i = 0; i < first.Length; i++)
        {
            // check the contents of the rows
            if((first[i].Start != second[i].Start) | (first[i].Stop != second[i].Stop))
            {
                return false;
            }
            // now search all of the columns
            var firstInner = first[i].SubIndex.Indexes;
            var secondInner = second[i].SubIndex.Indexes;
            if(firstInner.Length != secondInner.Length)
            {
                return false;
            }
            for (int j = 0; j < firstInner.Length; j++)
            {
                if ((firstInner[j].Start != secondInner[j].Start) | (firstInner[j].Stop != secondInner[j].Stop))
                {
                    return false;
                }
            }
        }
        // If everything has matched then they are the same.
        return true;
    }

    public static SparseTwinIndex<T> CreatePdTwinArray<T>(SparseArray<IZone> zoneArray)
    {
        return CreatePdArray<T>( zoneArray ).CreateSquareTwinArray<T>();
    }
}
