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
    public class SchoolBus : IMode
    {
        [RunParameter("AgeConstant1", 0.0f, "An additive constant for persons for different ages.")]
        public float AgeConstant1;

        [RunParameter("AgeConstant2", 0.0f, "An additive constant for persons for different ages.")]
        public float AgeConstant2;

        [RunParameter("AgeConstant3", 0.0f, "An additive constant for persons for different ages.")]
        public float AgeConstant3;

        [RunParameter("AgeConstant4", 0.0f, "An additive constant for persons for different ages.")]
        public float AgeConstant4;

        [RunParameter("Constant", 0.0f, "The base constant term of the utility calculation.")]
        public float Constant;

        [RunParameter("Destination Employment Density", 0.0f, "The weight to use for the employment density of the destination zone.")]
        public float DestinationEmploymentDensity;

        [RunParameter("Multiple-Vehicle Household", 0.0f, "An additive constant for households that contain a multiple vehicles.")]
        public float MultipleVehicleHousehold;

        [RunParameter("No Driver's License", 0.0f, "An additive constant for persons who have no driver's license.")]
        public float NoDriversLicense;

        [RunParameter("Origin Population Density", 0.0f, "The weight to use for the population density of the origin zone.")]
        public float OriginPopulationDensity;

        [RunParameter("OtherRegionDistance", 0f, "The weight to add based on the straight line distance between zones.")]
        public float OtherRegionDistance;

        [RunParameter("Parking Cost", 0.0f, "The weight given to the parking cost to calculate the utility.")]
        public float Parking;

        [RunParameter("Part-Time Work", 0.0f, "An additive constant for persons who are part time workers.")]
        public float PartTime;

        [RunParameter("Region Constant", 0f, "An additional constant to add if we are working with the 'Region Distance Number'")]
        public float SpecificRegionConstant;

        [RunParameter("SpecificRegionDistance", 0f, "The weight to add based on the straight line distance between zones.")]
        public float SpecificRegionDistance;

        [RunParameter("Region Distance Number", 1, "The region number to use for the specific distance.")]
        public int RegionDistanceNumber;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter("Single-Vehicle Household", 0.0f, "An additive constant for households that contain a single vehicle.")]
        public float SingleVehicleHousehold;

        [DoNotAutomate]
        protected ITripComponentData AdvancedNetworkData;

        [DoNotAutomate]
        protected INetworkData NetworkData;

        [Parameter("Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?")]
        public float CurrentlyFeasible { get; set; }

        [RunParameter("ModeName", "SchBs", "The name of the mode.")]
        public string ModeName { get; set; }

        public string Name
        {
            get;
            set;
        }

        [RunParameter("Network Name", "Auto", "The name of the network data to use.")]
        public string NetworkType { get; set; }

        [Parameter("NonPersonalVehicle", true, "Is this mode using a non-personal vehicle?")]
        public bool NonPersonalVehicle { get; set; }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public virtual float CalculateV(IZone origin, IZone destination, Time time)
        {
            // initialize to the combined constant value for the mode
            float v = Constant + PartTime + NoDriversLicense + SingleVehicleHousehold + MultipleVehicleHousehold
            + AgeConstant1 + AgeConstant2 + AgeConstant3 + AgeConstant4
            + Parking * destination.ParkingCost
            + GetDensityV( origin, destination );
            // if we need to calculate distance
            var distance = Root.ZoneSystem.Distances[origin.ZoneNumber, destination.ZoneNumber] / 1000f;
            if ( origin.RegionNumber == RegionDistanceNumber )
            {
                v += SpecificRegionConstant + SpecificRegionDistance * distance;
            }
            else
            {
                v += OtherRegionDistance * distance;
            }
            return v;
        }

        public virtual float Cost(IZone origin, IZone destination, Time time)
        {
            return NetworkData.TravelCost( origin, destination, time );
        }

        public virtual bool Feasible(IZone origin, IZone destination, Time time)
        {
            return ( CurrentlyFeasible > 0 ) && ( AdvancedNetworkData == null ?
                NetworkData.ValidOd( origin, destination, time )
                : AdvancedNetworkData.ValidOd( origin, destination, time ) );
        }

        public virtual bool RuntimeValidation(ref string error)
        {
            // Load in the network data
            LoadNetworkData();
            return ( NetworkData != null );
        }

        public virtual Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return NetworkData.TravelTime( origin, destination, time );
        }

        private float GetDensityV(IZone origin, IZone destination)
        {
            // convert the area to density per km
            return (float)( Math.Log( origin.Population / ( origin.InternalArea / 1000f ) + 1 ) * OriginPopulationDensity
                + Math.Log( destination.Employment / ( destination.InternalArea / 1000f ) + 1 ) * DestinationEmploymentDensity );
        }

        /// <summary>
        /// Find and Load in the network data
        /// </summary>
        private void LoadNetworkData()
        {
            foreach ( var dataSource in Root.NetworkData )
            {
                if ( dataSource.NetworkType == NetworkType )
                {
                    NetworkData = dataSource;
                    ITripComponentData advancedData = dataSource as ITripComponentData;
                    if ( advancedData != null )
                    {
                        AdvancedNetworkData = advancedData;
                    }
                    break;
                }
            }
        }
    }
}