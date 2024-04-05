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
using System.IO;
using XTMF;

namespace TMG.GTAModel.Analysis
{
    [ModuleInformation( Description = "This module will be used to create a file containing the distance travelled " +
        "multiplied by the number of trips between each origin and destination for a set of GTAModel purposes and " +
        "produces a table." )]
    public class CalculateDistanceTravelled : ISelfContainedModule
    {
        [RunParameter( "Distance Travelled FileName", "DistanceTravelled.csv", "The filename of the output data, blank to not generate." )]
        public string DistanceTravelledFileName;

        [RootModule]
        public ITravelDemandModel Root;

        [SubModelInformation( Description = "The modules to _Count the demand." )]
        public List<IModeAggregationTally> Tallies;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get;
            set;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            // Check to see if they want the output to begin with before calculating
            if ( String.IsNullOrWhiteSpace( DistanceTravelledFileName ) )
            {
                return;
            }
            var zones = Root.ZoneSystem.ZoneArray;
            var matrix = zones.CreateSquareTwinArray<float>().GetFlatData();
            foreach ( var tally in Tallies )
            {
                tally.IncludeTally( matrix );
            }
            SaveMatrix( zones.GetFlatData(), matrix, Root.ZoneSystem.Distances.GetFlatData() );
        }

        private static void ComputeData(IZone[] zones, float[][] demandMatrix, float[][] distanceMatrix, List<int> regionNumbers, float[] regionProductionDistances, float[] regionAttractionDistances, float[] regionAttractionSum, float[] regionProductionSum, float[] productionZoneDistances, float[] attractionZoneDistances, float[] zoneProductionSum, float[] zoneAttractionSum)
        {
            for ( int i = 0; i < zones.Length; i++ )
            {
                var demandRow = demandMatrix[i];
                var distanceRow = distanceMatrix[i];
                var regionIndex = regionNumbers.IndexOf( zones[i].RegionNumber );
                if ( regionIndex < 0 )
                {
                    for ( int j = 0; j < zones.Length; j++ )
                    {
                        var travel = demandRow[j] * distanceRow[j];
                        zoneProductionSum[i] += demandRow[j];
                        zoneAttractionSum[j] += demandRow[j];
                        productionZoneDistances[i] += travel;
                        attractionZoneDistances[j] += travel;
                    }
                }
                else
                {
                    for ( int j = 0; j < zones.Length; j++ )
                    {
                        var travel = demandRow[j] * distanceRow[j];
                        regionProductionSum[regionIndex] += demandRow[j];
                        zoneProductionSum[i] += demandRow[j];
                        zoneAttractionSum[j] += demandRow[j];

                        regionProductionDistances[regionIndex] += travel;
                        productionZoneDistances[i] += travel;
                        attractionZoneDistances[j] += travel;

                        var regionIndexJ = regionNumbers.IndexOf( zones[j].RegionNumber );
                        if ( regionIndexJ >= 0 )
                        {
                            regionAttractionSum[regionIndexJ] += demandRow[j];
                            regionAttractionDistances[regionIndexJ] += travel;
                        }
                    }
                }
            }
            for ( int i = 0; i < zones.Length; i++ )
            {
                productionZoneDistances[i] /= zoneProductionSum[i];
                attractionZoneDistances[i] /= zoneAttractionSum[i];
            }
            for ( int i = 0; i < regionProductionSum.Length; i++ )
            {
                regionProductionDistances[i] /= regionProductionSum[i];
                regionAttractionDistances[i] /= regionAttractionSum[i];
            }
        }

        private void GatherRegionData(List<int> regionNumbers, IZone[] zones)
        {
            for ( int i = 0; i < zones.Length; i++ )
            {
                var rn = zones[i].RegionNumber;
                if ( !regionNumbers.Contains( rn ) )
                {
                    regionNumbers.Add( rn );
                }
            }
        }

        private void SaveMatrix(IZone[] zones, float[][] demandMatrix, float[][] distanceMatrix)
        {
            // Make sure the output directory exists
            var dir = Path.GetDirectoryName( DistanceTravelledFileName );
            if ( !String.IsNullOrWhiteSpace( dir ) && !Directory.Exists( dir ) )
            {
                Directory.CreateDirectory( dir );
            }
            List<int> regionNumbers = [];
            GatherRegionData( regionNumbers, zones );
            float[] regionProductionDistances = new float[regionNumbers.Count];
            float[] regionAttractionDistances = new float[regionNumbers.Count];

            float[] regionAttractionSum = new float[regionNumbers.Count];
            float[] regionProductionSum = new float[regionNumbers.Count];

            float[] productionZoneDistances = new float[zones.Length];
            float[] attractionZoneDistances = new float[zones.Length];

            float[] zoneProductionSum = new float[zones.Length];
            float[] zoneAttractionSum = new float[zones.Length];

            // fill in all of the data
            ComputeData( zones, demandMatrix, distanceMatrix, regionNumbers, regionProductionDistances,
                regionAttractionDistances, regionAttractionSum, regionProductionSum, productionZoneDistances,
                attractionZoneDistances, zoneProductionSum, zoneAttractionSum );
            using ( StreamWriter writer = new StreamWriter( DistanceTravelledFileName ) )
            {
                writer.WriteLine( "Region Data" );
                writer.WriteLine( "Region Number,Production Distance Average,Attraction Distance Average" );
                for ( int i = 0; i < regionProductionDistances.Length; i++ )
                {
                    writer.Write( regionNumbers[i] );
                    writer.Write( ',' );
                    writer.Write( regionProductionDistances[i] );
                    writer.Write( ',' );
                    writer.Write( regionAttractionDistances[i] );
                    writer.WriteLine();
                }
                writer.WriteLine();
                writer.WriteLine();
                writer.WriteLine();
                writer.WriteLine( "Zone Data" );
                writer.WriteLine( "Zone Number,Production Distance Average,Attraction Distance Average" );
                // for each row
                for ( int i = 0; i < zones.Length; i++ )
                {
                    writer.Write( zones[i].ZoneNumber );
                    writer.Write( ',' );
                    writer.Write( productionZoneDistances[i] );
                    writer.Write( ',' );
                    writer.Write( attractionZoneDistances[i] );
                    writer.WriteLine();
                }
            }
        }
    }
}