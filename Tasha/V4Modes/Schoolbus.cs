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
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.V4Modes
{
    [ModuleInformation( Description =
        @"This module is designed to implement the Schoolbus mode for GTAModel V4.0+." )]
    public class Schoolbus : ITashaMode, IIterationSensitive
    {
        [RunParameter( "AgeFactor", 0f, "The factor applied to the log of age of the trip maker (+1)." )]
        public float AgeFactor;

        [RunParameter( "Constant", 0f, "The mode constant." )]
        public float Constant;

        [RunParameter( "DistanceFactor", 0f, "The factor applied to the distance traveled in the bus (km)" )]
        public float DistanceFactor;

        [RunParameter( "DriversLicenceFlag", 0f, "The constant factor applied if the person has a driver's license" )]
        public float DriversLicenceFlag;

        [RunParameter( "IntrazonalConstant", 0f, "The mode constant if the trip is intrazonal." )]
        public float IntrazonalConstant;

        [RootModule]
        public ITashaRuntime Root;

        private INetworkData Network;

        [Parameter( "Feasible", 1f, "Is the mode feasible?(1)" )]
        public float CurrentlyFeasible { get; set; }

        [Parameter( "Mode Name", "Schoolbus", "The name of the mode." )]
        public string ModeName { get; set; }

        public string Name { get; set; }

        [RunParameter( "Network Name", "Auto", "The name of the network to use for times." )]
        public string NetworkType { get; set; }

        public bool NonPersonalVehicle
        {
            get { return true; }
        }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        [DoNotAutomate]
        public IVehicleType RequiresVehicle
        {
            get { return null; }
        }

        [RunParameter( "Variance Scale", 1.0, "The factor applied to the error term." )]
        public double VarianceScale { get; set; }

        [SubModelInformation(Required = false, Description = "Constants for time of day")]
        public TimePeriodSpatialConstant[] TimePeriodConstants;

        public double CalculateV(ITrip trip)
        {
            float v;
            var zoneSystem = Root.ZoneSystem;
            var zones = zoneSystem.ZoneArray;
            var o = zones.GetFlatIndex( trip.OriginalZone.ZoneNumber );
            var d = zones.GetFlatIndex( trip.DestinationZone.ZoneNumber );
            // get the distance in km
            var distance = zoneSystem.Distances.GetFlatData()[o][d] / 1000.0f;
            // if intrazonal
            if ( o == d )
            {
                v = IntrazonalConstant;
            }
            else
            {
                v = Constant;
            }
            v += DistanceFactor * distance;
            var p = trip.TripChain.Person;
            if ( p.Licence )
            {
                v += DriversLicenceFlag;
            }
            v += AgeFactor * (float)Math.Log(p.Age + 1);
            v += RegionConstants[trip.OriginalZone.RegionNumber, trip.DestinationZone.RegionNumber];
            v += GetPlanningDistrictConstant(trip.ActivityStartTime, trip.OriginalZone.PlanningDistrict, trip.DestinationZone.PlanningDistrict);
            return v;
        }

        public float GetPlanningDistrictConstant(Time startTime, int pdO, int pdD)
        {
            for (int i = 0; i < TimePeriodConstants.Length; i++)
            {
                if (startTime >= TimePeriodConstants[i].StartTime && startTime < TimePeriodConstants[i].EndTime)
                {
                    return TimePeriodConstants[i].GetConstant(pdO, pdD);
                }
            }
            return 0f;
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            throw new NotImplementedException( "This mode is designed for Tasha only!" );
        }

        public float Cost(IZone origin, IZone destination, Time time)
        {
            return Network.TravelCost( origin, destination, time );
        }

        public bool Feasible(ITrip trip)
        {
            // you need to be going within the same region
            if(trip.OriginalZone.RegionNumber != trip.DestinationZone.RegionNumber)
            {
                return false;
            }
            // if we are going to school, yes
            if ( trip.Purpose == Activity.School ) return true;
            var tc = trip.TripChain.Trips;
            var index = tc.IndexOf( trip );

            // check for return from school
            if(index > 0)
            {
                if ( tc[index - 1].Purpose == Activity.School )
                {
                    return true;
                }
            }
            return false;
        }

        public bool Feasible(ITripChain tripChain)
        {
            return true;
        }

        public bool Feasible(IZone origin, IZone destination, Time time)
        {
            throw new NotImplementedException( "This mode is designed for Tasha only!" );
        }

        public void IterationEnding(int iterationNumber, int maxIterations)
        {
            
        }

        public SpatialConstant[] SpatialConstants;

        private SparseTwinIndex<float> RegionConstants;
         
        public void IterationStarting(int iterationNumber, int maxIterations)
        {
            //build the region constants
            var regions = TMG.Functions.ZoneSystemHelper.CreateRegionArray<float>(Root.ZoneSystem.ZoneArray);
            var regionIndexes = regions.ValidIndexArray();
            RegionConstants = regions.CreateSquareTwinArray<float>();
            var data = RegionConstants.GetFlatData();
            for(int i = 0; i < data.Length; i++)
            {
                for(int j = 0; j < data[i].Length; j++)
                {
                    data[i][j] = GetRegionConstant(regionIndexes[i], regionIndexes[j]);
                }
            }
            foreach(var timePeriod in TimePeriodConstants)
            {
                timePeriod.BuildMatrix();
            }
        }

        private float GetRegionConstant(int originRegion, int destinationRegion)
        {
            for(int i = 0; i < SpatialConstants.Length; i++)
            {
                if(SpatialConstants[i].Origins.Contains(originRegion) && SpatialConstants[i].Destinations.Contains(destinationRegion))
                {
                    return SpatialConstants[i].Constant;
                }
            }
            return 0f;
        }

        public bool RuntimeValidation(ref string error)
        {
            var networks = Root.NetworkData;

            if ( String.IsNullOrWhiteSpace( NetworkType ) )
            {
                error = "There was no network type selected for the " + ( String.IsNullOrWhiteSpace( ModeName ) ? "Auto" : ModeName ) + " mode!";
                return false;
            }
            if ( networks == null )
            {
                error = "There was no Auto Network loaded for the Auto Mode!";
                return false;
            }
            if ( !AssignNetwork( networks ) )
            {
                error = "We were unable to find the network data with the name \"" + NetworkType + "\" in this Model System!";
                return false;
            }
            return true;
        }

        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return Network.TravelTime( origin, destination, time );
        }

        private bool AssignNetwork(IList<INetworkData> networks)
        {
            foreach ( var network in networks )
            {
                if ( network.NetworkType == NetworkType )
                {
                    Network = network;
                    return true;
                }
            }
            return false;
        }
    }
}