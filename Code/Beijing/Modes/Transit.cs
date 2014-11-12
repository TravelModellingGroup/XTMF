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
using Tasha.Common;
using XTMF;
using TMG;

namespace Beijing.Modes
{
    public class Transit : ITashaMode
    {
        [RootModule]
        public ITashaRuntime Root;

        [RunParameter( "Mode Name", "Transit", "The name of the mode being modelled." )]
        public string ModeName
        {
            get;
            set;
        }

        [RunParameter( "Distance Name", "Distance", "The name of the trip's distance attribute." )]
        public string DistanceName;

        [RunParameter( "Income Name", "Income", "The name of the households income attribute." )]
        public string IncomeName;

        [RunParameter( "TravelTimeFactor", -1.0f, "The scaling component based on distance" )]
        public float TravelTimeFactor;

        [RunParameter( "LinearTravelTimeWeight", -1.0f, "The estimated scaling component based on distance" )]
        public float LinearTravelTimeWeight;

        [RunParameter( "NonLinearTravelTimeWeight", -1.0f, "The estimated scaling component based on distance" )]
        public float NonLinearTravelTimeFactor;

        [RunParameter( "TravelTimeConstant", 0f, "The constant component based on distance" )]
        public float ModeConstant;

        [RunParameter( "Youth", 0f, "A constant if the person is a youth" )]
        public float Youth;

        [RunParameter( "YoungAdult", 0f, "A constant if the person is a youth" )]
        public float YoungAdult;

        [RunParameter( "Female", 0f, "The constant for being a female" )]
        public float Female;

        [RunParameter( "DriversLicense", 0.0f, "A constant applied if a person has a driver's license." )]
        public float DriversLicense;

        [RunParameter( "ManyCarsOwned", 0.0f, "A constant applied if a household has > 1 car." )]
        public float ManyCarsOwned;

        [RunParameter( "Income1", 0.0f, "A constant applied if a person is from a class 1 income." )]
        public float Income1;

        [RunParameter( "Income2", 0.0f, "A constant applied if a person is from a class 2 or 3 income" )]
        public float Income2;

        [RunParameter( "Income3", 0.0f, "A constant applied if a person is from a class 4 or 5 income." )]
        public float Income3;

        [RunParameter( "Income4", 0.0f, "A constant applied if a person is from a class > 5 income." )]
        public float Income4;

        [RunParameter( "Minimum Age", 0, "The minimum age to use this mode" )]
        public int MinAge;

        [RunParameter( "LogTime", false, "Should we scale against the log base e of time in minutes?" )]
        public bool LogDistance;

        [RunParameter( "ExpTime", false, "Should we scale against the timeInMinutes^2?" )]
        public bool ExpDistance;

        [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
        public float CurrentlyFeasible { get; set; }

        public bool Feasible(IZone origin, IZone destination, Time timeOfDay)
        {
            return CurrentlyFeasible > 0;
        }

        [DoNotAutomate]
        public IVehicleType RequiresVehicle
        {
            get { return null; }
        }

        private ITripComponentData TransitData;

        public bool Feasible(ITrip trip)
        {
            var person = trip.TripChain.Person;
            return ( person.Age >= this.MinAge );
        }

        public bool Feasible(ITripChain tripChain)
        {
            return true;
        }

        public double CalculateV(ITrip trip)
        {
            var person = trip.TripChain.Person;
            var household = person.Household;
            var income = (int)household[this.IncomeName];
            IZone origin = trip.OriginalZone;
            IZone destination = trip.DestinationZone;
            Time startTime = trip.TripStartTime;
            var inVehicleTime = this.TransitData.InVehicleTravelTime( origin, destination, startTime ).ToMinutes();
            var waitTime = this.TransitData.WaitTime( origin, destination, startTime ).ToMinutes();
            var walkTime = this.TransitData.WalkTime( origin, destination, startTime ).ToMinutes();
            var totalTime = inVehicleTime + waitTime + walkTime;
            // Calculate the time of traveling
            var v = 0f;
            v = this.LinearTravelTimeWeight * ( totalTime + this.ModeConstant );
            if ( this.LogDistance )
            {
                v += this.NonLinearTravelTimeFactor * this.TravelTimeFactor * ( (float)Math.Log( totalTime ) + this.ModeConstant );
            }
            else if ( this.ExpDistance )
            {
                v += this.NonLinearTravelTimeFactor * (float)( ( totalTime * totalTime ) + this.ModeConstant );
            }
            
            if ( household.Vehicles.Length > 1 )
            {
                v += this.ManyCarsOwned;
            }
            if ( person.Licence )
            {
                v += this.DriversLicense;
            }
            if ( person.Youth )
            {
                v += this.Youth;
            }
            else if ( person.YoungAdult )
            {
                v += this.YoungAdult;
            }
            if ( person.Female )
            {
                v += this.Female;
            }
            switch ( income )
            {
                // 1
                case 1:
                    v += this.Income1;
                    break;
                // 2
                case 2:
                case 3:
                    v += this.Income2;
                    break;
                // 3
                case 4:
                case 5:
                    v += this.Income3;
                    break;
                // 4
                case 6:
                case 7:
                case 8:
                default:
                    v += this.Income4;
                    break;
            }
            return v;
        }

        [RunParameter( "VarianceScale", 1.0, "How random the error term of utility will be" )]
        public double VarianceScale
        {
            get;
            set;
        }

        /// <summary>
        /// This gets the travel time between zones
        /// </summary>
        /// <param name="origin">Where to start</param>
        /// <param name="destination">Where to go</param>
        /// <param name="time">What time of day is it? (hhmm.ss)</param>
        /// <returns>The amount of time it will take</returns>
        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return this.TransitData.TravelTime( origin, destination, time );
        }

        public float Cost(IZone origin, IZone destination, Time time)
        {
            return this.TransitData.TravelCost( origin, destination, time );
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            return 0;
        }

        public bool NonPersonalVehicle
        {
            get { return true; }
        }

        [RunParameter( "Network Name", "Transit", "The name of the network that this mode uses." )]
        public string NetworkType
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            private set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public override string ToString()
        {
            return this.ModeName;
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( String.IsNullOrWhiteSpace( this.ModeName ) )
            {
                error = "All modes require a mode name!";
                return false;
            }
            bool found = false;
            IList<INetworkData> networks;
            networks = this.Root.NetworkData;
            foreach ( var network in networks )
            {
                if ( network.NetworkType == this.NetworkType )
                {
                    this.TransitData = network as ITripComponentData;
                    if ( this.TransitData == null )
                    {
                        error = "The network data \"" + this.NetworkType + "\" does not support the ITransitNetworkData interface.  Please use a module that supports this!";
                        return false;
                    }
                    found = true;
                    break;
                }
            }
            if ( !found )
            {
                error = "We were unable to find the network data with the name \"" + this.NetworkType + "\" in this Model System!";
                return false;
            }
            return true;
        }
    }
}
