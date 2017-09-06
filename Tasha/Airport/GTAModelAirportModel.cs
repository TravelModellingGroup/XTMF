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
using Datastructure;
using TMG;
using TMG.DataUtility;
using Tasha.Common;
using XTMF;
// ReSharper disable CompareOfFloatsByEqualityOperator
namespace Tasha.Airport
{
    [ModuleInformation(Description =
        @"GTAModelAirport is designed to replicate the GTAModelV2 airport model with the Tasha framework. 
This is achieved by being integrated into the EMME tally system.")]
    public class GTAModelAirportModel : IModeAggregationTally
    {
        [RunParameter("Beta", -0.1f, "The beta parameter applied to the distance utility calculation for secondary airports.")]
        public float Beta;

        [RunParameter("Region Level Constant", -27.7274f, "A constant applied across all regions.")]
        public float Constant;

        [RunParameter("Max Secondary Distance", 60f, "The maximum distance that a secondary airport can be accessed from, 0 if any.")]
        public float MaxSecondaryDistance;

        [SubModelInformation(Description = "The primary airport in the study area.", Required = true)]
        public Airport PrimaryAirport;

        [RunParameter("Region Constants", "53.79565,-226.373,0,0", typeof(FloatList), "Constant parameters for each specified region.")]
        public FloatList RegionConstants;

        [RunParameter("Region Employment Factor", 0.000876f, "The factor applied against the region's professional employment.")]
        public float RegionEmploymentFactor;

        [RunParameter("Region Numbers", "1,2,3,4", typeof(NumberList), "The numbers for the regions that we will be processing.")]
        public NumberList RegionNumbers;

        [RunParameter("Region Residence Factor", 0.004993f, "The factor applied against the region's professional residence.")]
        public float RegionResidenceFactor;

        [RunParameter("Return Factor", 0.9757f, "The factor to compute return trips, referred to as dfac in previous documentation.")]
        public float ReturnFactor;

        [RootModule]
        public ITashaRuntime Root;

        [RunParameter("Save Data", false, "Should we save the airport matrix?")]
        public bool SaveMatrix;

        [SubModelInformation(Description = "A list of airports to process.", Required = false)]
        public List<Airport> SecondaryAirports;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public void IncludeTally(float[][] data)
        {
            var numberOfRegions = RegionNumbers.Count;
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var numberOfZones = zones.Length;
            // Sum the employment and the Professional workers in each zone
            // that is not the airport per region aggregate at a regional level
            float[] employmentTotal = new float[numberOfRegions];
            float[] professionalTotal = new float[numberOfRegions];
            float[] tripsToRegion = new float[numberOfRegions];

            // Gather the information for each region
            float totalTrips = ComputeRegionInformation( numberOfRegions, zones, numberOfZones, employmentTotal, professionalTotal, tripsToRegion );
            // Now that we have the regional information we can use it to compute the primary airport
            ComputePrimaryAirport( zones, numberOfZones, employmentTotal, professionalTotal, tripsToRegion, data );
            // After computing the primary airport we can continue with the secondary airports
            ComputeSecondaryAirports( zones, numberOfZones, data, totalTrips );
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private void ComputePrimaryAirport(IZone[] zones, int numberOfZones, float[] employmentTotal, float[] professionalTotal, float[] tripsToRegion, float[][] data)
        {
            var primaryZone = Root.ZoneSystem.ZoneArray.GetFlatIndex( PrimaryAirport.ZoneNumber );
            for ( int i = 0; i < numberOfZones; i++ )
            {
                if ( i != primaryZone )
                {
                    if (InverseLookup(zones[i].RegionNumber, out int regionIndex))
                    {
                        var primary = tripsToRegion[regionIndex]
                            * ((zones[i].ProfessionalEmployment * RegionEmploymentFactor
                            + zones[i].WorkProfessional * RegionResidenceFactor)
                            /
                            (employmentTotal[regionIndex] * RegionEmploymentFactor
                            + professionalTotal[regionIndex] * RegionResidenceFactor));
                        if (!float.IsNaN(primary) & !float.IsInfinity(primary))
                        {
                            data[i][primaryZone] += primary;
                            data[primaryZone][i] += primary * ReturnFactor;
                        }
                    }
                }
            }
        }

        private float ComputeRegionInformation(int numberOfRegions, IZone[] zones, int numberOfZones, float[] employmentTotal, float[] professionalTotal, float[] tripsToRegion)
        {
            float denominator = 0f;
            for ( int i = 0; i < numberOfZones; i++ )
            {
                var zone = zones[i];
                // Don't process things not included in our regions nor if it is a zone that contains an airport
                if ( IsPrimaryAirportZone( zone.ZoneNumber ) || !InverseLookup( zone.RegionNumber, out int regionIndex ) )
                {
                    continue;
                }
                professionalTotal[regionIndex] += zone.WorkProfessional;
                employmentTotal[regionIndex] += zone.ProfessionalEmployment;
            }
            // Apply calculation for a region level breakdown and store it for each airport
            for ( int i = 0; i < numberOfRegions; i++ )
            {
                // Don't process things not included in our regions nor if it is a zone that contains an airport
                var value = RegionConstants[i]
                    + employmentTotal[i] * RegionEmploymentFactor
                    + professionalTotal[i] * RegionResidenceFactor;
                // don't allow negative values
                value = ( value < 0 ? 0 : value );
                tripsToRegion[i] = value;
                denominator += value;
            }
            // normalize the regions and then apply the prediction
            var timePeriodTrips = PrimaryAirport.BaseTimePeriod * ( PrimaryAirport.FuturePrediction / PrimaryAirport.Base );
            if ( float.IsNaN( timePeriodTrips ) || float.IsInfinity( timePeriodTrips ) )
            {
                throw new XTMFRuntimeException(this, "In '" + Name
                    + "' we encountered a non real value for the number of trips for the primary airport!\r\n"
                    + "Please make sure that the primary airport base is set properly!" );
            }
            for ( int i = 0; i < numberOfRegions; i++ )
            {
                tripsToRegion[i] = timePeriodTrips * ( tripsToRegion[i] / denominator );
            }
            return timePeriodTrips;
        }

        private void ComputeSecondaryAirports(IZone[] zones, int numberOfZones, float[][] data, float totalTrips)
        {
            var numberOfSecondaryAirports = SecondaryAirports.Count;
            if ( numberOfSecondaryAirports <= 0 )
            {
                return;
            }
            var distances = Root.ZoneSystem.Distances;
            var sparseZones = Root.ZoneSystem.ZoneArray;
            // compute the secondary airport values
            for ( int i = 0; i < numberOfSecondaryAirports; i++ )
            {
                // get the total amount for this airport
                var tripsToThisSecondary = totalTrips * ( SecondaryAirports[i].FuturePrediction / PrimaryAirport.FuturePrediction );
                // if there are no trips don't bother processing it all
                if ( tripsToThisSecondary == 0 ) continue;
                // compute the denominator
                float denominator = 0f;
                var airportIndex = sparseZones.GetFlatIndex( SecondaryAirports[i].ZoneNumber );
                // make sure the airport is in a valid zone
                if ( airportIndex < 0 || airportIndex >= numberOfZones ) continue;
                var airportZone = zones[airportIndex];
                for ( int j = 0; j < numberOfZones; j++ )
                {
                    // also save the friction calculation in the trip data for now
                    denominator += ( data[j][airportIndex] = ComputeSecondaryFriction( zones[j], airportZone, distances ) );
                }
                // now we can distribute the trips based on the friction [singly constrained gravity model]
                for ( int j = 0; j < numberOfZones; j++ )
                {
                    data[j][airportIndex] += tripsToThisSecondary * data[j][airportIndex] / denominator;
                    data[airportIndex][j] += data[j][airportIndex] * ReturnFactor;
                }
            }
        }

        private float ComputeSecondaryFriction(IZone from, IZone to, SparseTwinIndex<float> distances)
        {
            var distance = distances[from.ZoneNumber, to.ZoneNumber];
            if ( distance < MaxSecondaryDistance || MaxSecondaryDistance <= 0 )
            {
                return ( from.ProfessionalEmployment * RegionEmploymentFactor
                + from.WorkProfessional * RegionResidenceFactor )
                * (float)Math.Exp( Beta * distance );
            }
            return 0f;
        }

        private bool InverseLookup(int regionNumber, out int regionIndex)
        {
            var length = RegionNumbers.Count;
            for ( int i = 0; i < length; i++ )
            {
                if ( RegionNumbers[i] == regionNumber )
                {
                    regionIndex = i;
                    return true;
                }
            }
            regionIndex = -1;
            return false;
        }

        private bool IsPrimaryAirportZone(int zoneNumber)
        {
            return PrimaryAirport.ZoneNumber == zoneNumber;
        }

    }
}