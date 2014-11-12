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
using Tasha.Common;
using Datastructure;
using System.Text;
using Tasha;
using XTMF;
using TMG;

namespace Beijing.Modes
{
    /// <summary>
    /// Essentially the same ride share except
    /// </summary>
    public sealed class PassengerMode : ISharedMode
    {
        [RootModule]
        public ITashaRuntime Root;

        [RunParameter( "Name", "Passenger", "The name of the mode" )]
        public string ModeName { get; set; }

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

        [RunParameter( "TravelCostWeight", 8.0f, "The weight of travel time on the utility" )]
        public float TravelCostWeight;

        [RunParameter( "TravelCostFactor", -1.0f, "The scaling component based on distance" )]
        public float TravelCostFactor;

        [RunParameter( "Youth", 0f, "A constant if the person is a youth" )]
        public float Youth;

        [RunParameter( "YoungAdult", 0f, "A constant if the person is a youth" )]
        public float YoungAdult;

        [RunParameter( "Female", 0f, "The constant for being a female" )]
        public float Female;

        [RunParameter( "Minimum Age", 0, "The minimum age to use this mode" )]
        public int MinAge;

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

        [RunParameter( "LicenceRequired", true, "Is a license required for this mode?" )]
        public bool LicenceRequired;

        [RunParameter( "LogTime", false, "Should we scale against the log base e of distance?" )]
        public bool LogTime;

        [RunParameter( "ExpTime", false, "Should we scale against the distance^2?" )]
        public bool ExpTime;

        [RunParameter( "Vehicle Name", "AutoType", "The name of the vehicle to use for this mode" )]
        public string VehicleName;
        [RunParameter( "croundtrip_facil", 0f, "The constant factor applied if the trip is passenger all the way around" )]
        public float croundtrip_facil;
        [RunParameter( "cconnecting_facil", 0f, "The constant factor applied if there is a connecting chain" )]
        public float cconnecting_facil;

        [RunParameter( "Max Driver Time", "15 minutes", typeof( Time ), "In minutes." )]
        public Time MaxDriverTimeThreshold;

        [RunParameter( "Network Name", "Auto", "The name of the network that this mode uses." )]
        public string NetworkType
        {
            get;
            set;
        }

        [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
        public float CurrentlyFeasible { get; set; }

        public bool Feasible(IZone origin, IZone destination, Time timeOfDay)
        {
            return CurrentlyFeasible > 0;
        }

        [SubModelInformation( Description = "Composes the sharing of resources", Required = true )]
        public IResourceAvailability ResourceAvailability;

        [DoNotAutomate]
        public INetworkData AutoData;

        private byte modeChoiceArrIndex = 0;

        /// <summary>
        /// 
        /// </summary>
        public byte ModeChoiceArrIndex
        {
            get
            {
                return modeChoiceArrIndex;
            }
            set
            {
                this.modeChoiceArrIndex = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// This does not require a personal vehicle
        /// </summary>
        public bool NonPersonalVehicle
        {
            get { return true; }
        }

        [DoNotAutomate]
        /// <summary>
        /// Does this require a vehicle
        /// </summary>
        public IVehicleType RequiresVehicle
        {
            get { return this.Root.AutoType; }
        }

        /// <summary>
        /// Create a new Auto mode, this will be called when
        /// Tasha# loads us
        /// </summary>
        public PassengerMode()
        {
        }

        /// <summary>
        /// Checking if the trip is feasible
        /// </summary>
        /// <param name="trip">the trip to test feasibility on</param>
        /// <returns>Is this trip feasible?</returns>
        public bool Feasible(ITrip trip)
        {
            //passenger mode does not handle joint trips
            if ( trip.TripChain.JointTrip ) return false;

            //they already are assigned a vehicle dont try to find a driver for them.
            if ( trip.Mode != null && !trip.Mode.NonPersonalVehicle ) return false;

            //attach to trip pass the configuration value to the GetDriver method
            trip.Attach( "MaxDriverTimeThreshold", MaxDriverTimeThreshold );

            //trip.SharedModeDriver = ra.GetDriver(trip, this);

            var possible = this.ResourceAvailability.AssignPossibleDrivers( trip, this );
            if ( possible )
            {
                return true;
            }
            return false;

        }



        /// <summary>
        /// Basic auto gas price from origin zone to destination zone
        /// </summary>return trip.TripChain.Person.Licence 
        /// <param name="origin"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        public float Cost(IZone origin, IZone destination, Time time)
        {
            return 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tripChain"></param>
        /// <returns></returns>
        public bool Feasible(ITripChain tripChain)
        {
            //passenger mode does not handle joint trips
            return !tripChain.JointTrip;
        }

        private static Time FifteenMinutes = Time.FromMinutes( 15 );
        /// <summary>
        /// This gets the travel time between zones
        /// </summary>
        /// <param name="origin">Where to start</param>
        /// <param name="destination">Where to go</param>
        /// <param name="time">What time of day is it? (hhmm.ss)</param>
        /// <returns>The amount of time it will take</returns>
        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return FifteenMinutes;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="trip"></param>
        /// <returns></returns>
        public double CalculateV(ITrip trip)
        {
            bool isDriver = (bool)trip["isDriver"];
            var person = trip.TripChain.Person;
            var household = person.Household;
            var income = (int)household[this.IncomeName];
            var time = this.AutoData.TravelTime( trip.OriginalZone, trip.DestinationZone, trip.TripStartTime ).ToMinutes();
            // Calculate the time of traveling
            var v = 0f;
            v = this.LinearTravelTimeWeight * time;
            if ( this.LogTime )
            {
                v += this.NonLinearTravelTimeFactor * this.TravelTimeFactor * ( (float)Math.Log( time ) + this.ModeConstant );
            }
            else if ( this.ExpTime )
            {
                v += this.NonLinearTravelTimeFactor * (float)( ( time * time ) + this.ModeConstant );
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

            if ( isDriver )
            {
                bool hasConnectingChain = ( trip.TripChain["ConnectingChain"] != null );
                if ( hasConnectingChain )
                {
                    v += this.cconnecting_facil;
                }
                else
                {
                    v += this.croundtrip_facil;
                }
                if ( trip.TripChain.Person.Licence )
                {
                    v += this.DriversLicense;
                }
            }
            return v;
        }

        [DoNotAutomate]
        public ITashaMode AssociatedMode
        {
            get { return this.Root.AutoMode; }
        }

        [RunParameter( "VarianceScale", 1.0f, "The scale for varriance used for variance testing." )]
        public double VarianceScale
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
            get { return new Tuple<byte, byte, byte>( 100, 200, 100 ); }
        }

        /// <summary>
        /// This is called before the start method as a way to pre-check that all of the parameters that are selected
        /// are in fact valid for this module.
        /// </summary>
        /// <param name="error">A string that should be assigned a detailed error</param>
        /// <returns>If the validation was successful or if there was a problem</returns>
        public bool RuntimeValidation(ref string error)
        {
            return this.FindAutoData( ref error );
        }

        private bool FindAutoData(ref string error)
        {
            IList<INetworkData> networks;
            networks = this.Root.NetworkData;
            if ( String.IsNullOrWhiteSpace( this.NetworkType ) )
            {
                error = "There was no network type selected for the " + ( String.IsNullOrWhiteSpace( this.ModeName ) ? "Auto" : this.ModeName ) + " mode!";
                return false;
            }
            if ( networks == null )
            {
                error = "There was no Auto Network loaded for the Auto Mode!";
                return false;
            }
            bool found = false;
            foreach ( var network in networks )
            {
                if ( network.NetworkType == this.NetworkType )
                {
                    this.AutoData = network;
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

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            throw new NotImplementedException();
        }
    }
}
