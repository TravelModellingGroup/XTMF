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
            var zones = this.Root.ZoneSystem.ZoneArray;
            var matrix = zones.CreateSquareTwinArray<float>().GetFlatData();
            foreach ( var tally in this.Tallies )
            {
                tally.IncludeTally( matrix );
            }
            SaveMatrix( zones.GetFlatData(), matrix, this.Root.ZoneSystem.Distances.GetFlatData() );
        }

        private static void ComputeData(IZone[] zones, float[][] demandMatrix, float[][] distanceMatrix, List<int> regionNumbers, float[] RegionProductionDistances, float[] RegionAttractionDistances, float[] RegionAttractionSum, float[] RegionProductionSum, float[] ProductionZoneDistances, float[] AttractionZoneDistances, float[] ZoneProductionSum, float[] ZoneAttractionSum)
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
                        ZoneProductionSum[i] += demandRow[j];
                        ZoneAttractionSum[j] += demandRow[j];
                        ProductionZoneDistances[i] += travel;
                        AttractionZoneDistances[j] += travel;
                    }
                }
                else
                {
                    for ( int j = 0; j < zones.Length; j++ )
                    {
                        var travel = demandRow[j] * distanceRow[j];
                        RegionProductionSum[regionIndex] += demandRow[j];
                        ZoneProductionSum[i] += demandRow[j];
                        ZoneAttractionSum[j] += demandRow[j];

                        RegionProductionDistances[regionIndex] += travel;
                        ProductionZoneDistances[i] += travel;
                        AttractionZoneDistances[j] += travel;

                        var regionIndexJ = regionNumbers.IndexOf( zones[j].RegionNumber );
                        if ( regionIndexJ >= 0 )
                        {
                            RegionAttractionSum[regionIndexJ] += demandRow[j];
                            RegionAttractionDistances[regionIndexJ] += travel;
                        }
                    }
                }
            }
            for ( int i = 0; i < zones.Length; i++ )
            {
                ProductionZoneDistances[i] /= ZoneProductionSum[i];
                AttractionZoneDistances[i] /= ZoneAttractionSum[i];
            }
            for ( int i = 0; i < RegionProductionSum.Length; i++ )
            {
                RegionProductionDistances[i] /= RegionProductionSum[i];
                RegionAttractionDistances[i] /= RegionAttractionSum[i];
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
            var dir = Path.GetDirectoryName( this.DistanceTravelledFileName );
            if ( !String.IsNullOrWhiteSpace( dir ) && !Directory.Exists( dir ) )
            {
                Directory.CreateDirectory( dir );
            }
            List<int> regionNumbers = new List<int>();
            GatherRegionData( regionNumbers, zones );
            float[] RegionProductionDistances = new float[regionNumbers.Count];
            float[] RegionAttractionDistances = new float[regionNumbers.Count];

            float[] RegionAttractionSum = new float[regionNumbers.Count];
            float[] RegionProductionSum = new float[regionNumbers.Count];

            float[] ProductionZoneDistances = new float[zones.Length];
            float[] AttractionZoneDistances = new float[zones.Length];

            float[] ZoneProductionSum = new float[zones.Length];
            float[] ZoneAttractionSum = new float[zones.Length];

            int[] numberOfZonesInRegion = new int[regionNumbers.Count];
            // fill in all of the data
            ComputeData( zones, demandMatrix, distanceMatrix, regionNumbers, RegionProductionDistances,
                RegionAttractionDistances, RegionAttractionSum, RegionProductionSum, ProductionZoneDistances,
                AttractionZoneDistances, ZoneProductionSum, ZoneAttractionSum );
            using ( StreamWriter writer = new StreamWriter( this.DistanceTravelledFileName ) )
            {
                writer.WriteLine( "Region Data" );
                writer.WriteLine( "Region Number,Production Distance Average,Attraction Distance Average" );
                for ( int i = 0; i < RegionProductionDistances.Length; i++ )
                {
                    writer.Write( regionNumbers[i] );
                    writer.Write( ',' );
                    writer.Write( RegionProductionDistances[i] );
                    writer.Write( ',' );
                    writer.Write( RegionAttractionDistances[i] );
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
                    writer.Write( ProductionZoneDistances[i] );
                    writer.Write( ',' );
                    writer.Write( AttractionZoneDistances[i] );
                    writer.WriteLine();
                }
            }
        }
    }
}