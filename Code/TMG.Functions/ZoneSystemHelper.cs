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
namespace TMG.Functions
{
    public static class ZoneSystemHelper
    {
        public static SparseArray<T> CreatePDArray<T>(SparseArray<IZone> zoneArray)
        {
            var zones = zoneArray.GetFlatData();
            List<int> pdNumbersFound = new List<int>( 10 );
            for ( int i = 0; i < zones.Length; i++ )
            {
                var pdID = zones[i].PlanningDistrict;
                if ( !pdNumbersFound.Contains( pdID ) )
                {
                    pdNumbersFound.Add( pdID );
                }
            }
            var pdArray = pdNumbersFound.ToArray();
            return SparseArray<T>.CreateSparseArray( pdArray, new T[pdArray.Length] );
        }

        public static SparseArray<T> CreateRegionArray<T>(SparseArray<IZone> zoneArray)
        {
            var zones = zoneArray.GetFlatData();
            List<int> regionNumbersFound = new List<int>(10);
            for(int i = 0; i < zones.Length; i++)
            {
                var regionID = zones[i].RegionNumber;
                if(!regionNumbersFound.Contains(regionID))
                {
                    regionNumbersFound.Add(regionID);
                }
            }
            var regionArray = regionNumbersFound.ToArray();
            return SparseArray<T>.CreateSparseArray(regionArray, new T[regionArray.Length]);
        }

        public static SparseTwinIndex<T> CreatePDTwinArray<T>(SparseArray<IZone> zoneArray)
        {
            return CreatePDArray<T>( zoneArray ).CreateSquareTwinArray<T>();
        }
    }
}
