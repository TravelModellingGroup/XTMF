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
using XTMF;

namespace TMG.GTAModel.Modes
{
    [ModuleInformation( Description =
        @"This module allows the ability to split the logic based upon distance between walk and bike under a single mode.  
Using the distance ranges either walk or bike parameters will be used for a given OD pair." )]
    public class WalkBike : IMode
    {
        [RunParameter( "AgeConstant1", 0.0f, "An additive constant for persons for different ages." )]
        public float AgeConstant1;

        [RunParameter( "AgeConstant2", 0.0f, "An additive constant for persons for different ages." )]
        public float AgeConstant2;

        [RunParameter( "AgeConstant3", 0.0f, "An additive constant for persons for different ages." )]
        public float AgeConstant3;

        [RunParameter( "AgeConstant4", 0.0f, "An additive constant for persons for different ages." )]
        public float AgeConstant4;

        [RunParameter( "Bike Time Weight", 0.0f, "The utility for each minute on a bike.  This is only used for network data sources that support walk time components." )]
        public float Bike;

        [RunParameter( "Bike Constant", 0.0f, "The base constant term of the utility calculation." )]
        public float BikeConstant;

        [RunParameter( "Bike Distance", 0f, "The weight to add based on the straight line distance (KM) between zones for Bike." )]
        public float BikeDistance;

        [RunParameter( "Bike Max Distance", 12.0f, "The maximum distance (KM) that you can travel with this mode, 0 means unlimited" )]
        public float BikeMaxDistance;

        [RunParameter( "Bike Speed", 66.667f * 4, "The speed in meters per minute." )]
        public float BikeMetersPerMinute;

        [RunParameter( "Bike Min Distance", 3.0f, "The minimum distance (KM) that you can travel with this mode." )]
        public float BikeMinDistance;

        [RunParameter( "Destination Employment Density", 0.0f, "The weight to use for the employment density of the destination zone." )]
        public float DestinationEmploymentDensity;

        [RunParameter( "Destination Population Density", 0.0f, "The weight to use for the population density of the destination zone." )]
        public float DestinationPopulationDensity;

        [RunParameter( "Multiple-Vehicle Household", 0.0f, "An additive constant for households that contain a multiple vehicles." )]
        public float MultipleVehicleHousehold;

        [RunParameter( "No Driver's License", 0.0f, "An additive constant for persons who have no driver's license." )]
        public float NoDriversLicense;

        [RunParameter( "Origin Employment Density", 0.0f, "The weight to use for the employment density of the origin zone." )]
        public float OriginEmploymentDensity;

        [RunParameter( "Origin Population Density", 0.0f, "The weight to use for the population density of the origin zone." )]
        public float OriginPopulationDensity;

        [RunParameter( "Part-Time Work", 0.0f, "An additive constant for persons who are part time workers." )]
        public float PartTime;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Single-Vehicle Household", 0.0f, "An additive constant for households that contain a single vehicle." )]
        public float SingleVehicleHousehold;

        [RunParameter( "Walk Time Weight", 0.0f, "The utility for each minute walking.  This is only used for network data sources that support walk time components." )]
        public float Walk;

        [RunParameter( "Walk Constant", 0.0f, "The base constant term of the utility calculation." )]
        public float WalkConstant;

        [RunParameter( "Walk Distance", 0f, "The weight to add based on the straight line distance (KM) between zones for Walk." )]
        public float WalkDistance;

        [RunParameter( "Walk Max Distance", 3.0f, "The maximum distance (KM) that you can travel with this mode, 0 means unlimited." )]
        public float WalkMaxDistance;

        [RunParameter( "Walk Speed", 66.667f, "The speed in meters per minute." )]
        public float WalkMetersPerMinute;

        [RunParameter( "Walk Min Distance", 0.0f, "The minimum distance (KM) that you can travel with this mode." )]
        public float WalkMinDistance;

        [Parameter( "Demographic Category Feasible", 1.0f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
        public float CurrentlyFeasible { get; set; }

        [RunParameter( "Mode Name", "WalkBike", "The name of the mode." )]
        public string ModeName { get; set; }

        public string Name
        {
            get;
            set;
        }

        public string NetworkType
        {
            get { return null; }
        }

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

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            // add up all of the constants
            float v = this.PartTime + this.SingleVehicleHousehold + this.MultipleVehicleHousehold;
            v += this.AgeConstant1 + this.AgeConstant2 + this.AgeConstant3 + this.AgeConstant4;
            v += this.GetDensityV( origin, destination );
            var distance = this.Root.ZoneSystem.Distances[origin.ZoneNumber, destination.ZoneNumber] / 1000f;
            if ( distance >= BikeMinDistance )
            {
                v += this.BikeConstant;
                v += this.BikeDistance * distance;
            }
            else
            {
                v += this.WalkConstant;
                v += this.WalkDistance * distance;
            }
            return v;
        }

        public float Cost(IZone origin, IZone destination, Time time)
        {
            return 0f;
        }

        public bool Feasible(IZone origin, IZone destination, Time time)
        {
            if ( CurrentlyFeasible <= 0 ) return false;
            if ( ( WalkMaxDistance == 0 ) & ( BikeMaxDistance == 0 ) ) return true;
            var distance = this.Root.ZoneSystem.Distances[origin.ZoneNumber, destination.ZoneNumber] / 1000f;
            // make sure it is in one of the valid ranges
            return ( ( distance >= WalkMinDistance ) & ( distance <= WalkMaxDistance ) )
                | ( ( distance >= BikeMinDistance ) & ( distance <= BikeMaxDistance ) );
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( WalkMinDistance > WalkMaxDistance )
            {
                error = "In " + this.Name + " the Minimum distance is greater than the maximum distance!\r\nPlease fix these parameters in order to continue.";
                return false;
            }
            if ( BikeMinDistance > BikeMaxDistance )
            {
                error = "In " + this.Name + " the Minimum distance is greater than the maximum distance!\r\nPlease fix these parameters in order to continue.";
                return false;
            }
            return true;
        }

        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            var distance = this.Root.ZoneSystem.Distances[origin.ZoneNumber, destination.ZoneNumber] / 1000f;
            if ( distance >= BikeMinDistance )
            {
                return Time.FromMinutes( distance / BikeMetersPerMinute );
            }
            else
            {
                return Time.FromMinutes( distance / WalkMetersPerMinute );
            }
        }

        private float GetDensityV(IZone origin, IZone destination)
        {
            // convert the area to KM^2
            var originFactor = 1f / ( origin.InternalArea / 1000f );
            var destinationFactor = 1f / ( destination.InternalArea / 1000f );
            return (float)(
                 Math.Log( origin.Population * originFactor + 1 ) * OriginPopulationDensity
                + Math.Log( destination.Employment * destinationFactor + 1 ) * DestinationEmploymentDensity
                );
        }
    }
}