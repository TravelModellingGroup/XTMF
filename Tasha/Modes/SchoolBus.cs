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
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Modes
{
    /// <summary>
    ///
    /// </summary>
    public sealed class SchoolBus : ITashaMode
    {
        [RunParameter( "Age", 0f, "The factor applied to the age of the person" )]
        public float Age;

        [RunParameter( "Auto Network Name", "Auto", "The name of the auto network." )]
        public string AutoNetworkName;

        [RunParameter( "CSchoolBus", 0f, "The constant factor for School Bus" )]
        public float CSchoolBus;

        [RunParameter( "Distance", 0f, "The factor applied to the distance travelled in the bus" )]
        public float Distance;

        [RunParameter( "DriversLicence", 0f, "The constant factor applied if the person has a driver's licence" )]
        public float DriversLicence;

        [RunParameter( "MaxAge", 20, "The maximum age that can travel on this mode" )]
        public float MaxAge;

        [RunParameter( "MaxDistance", 15000, "The maximum distance (M) that can be travelled for this mode is available" )]
        public float MaxDistance;

        //constants for school bus
        [RunParameter( "MinDistance", 1600, "The minimum distance (M) needed to be travelled before this mode is available" )]
        public float MinDistance;

        [RunParameter( "SchoolPurpose", 0f, "The constant factor applied if the trip purpose is for school" )]
        public float SchoolPurpose;

        [RootModule]
        public ITravelDemandModel TashaRuntime;

        [RunParameter( "TransitPass", 0f, "The constant factor applied if the person has a transit pass" )]
        public float TransitPass;

        [RunParameter( "YoungAdultPassenger", 0f, "The constant factor applied if the person is a young adult" )]
        public float YoungAdultPassenger;

        [RunParameter( "YoungAdultWalk", 0f, "The constant factor applied for a young adult walking" )]
        public float YoungAdultWalk;

        [RunParameter( "YouthPassenger", 0f, "The constant factor applied if the person is a youth" )]
        public float YouthPassenger;

        [RunParameter( "YouthWalk", 0f, "The constant factor applied for a youth walking" )]
        public float YouthWalk;

        private string AvailableZonesStr;

        private List<int> _AvailableZones;

        [DoNotAutomate]
        private INetworkData Data;

        [RunParameter( "AvailableZones", "0-2200", "Example \"0-2200,3000-3500\" is all of the zones between 0 and 2200 and all of the zones between 3000 and 3500." )]
        public string AvailableZones
        {
            get
            {
                return AvailableZonesStr;
            }

            set
            {
                AvailableZonesStr = value;
                _AvailableZones = Common.ConvertToIntList( value );
            }
        }

        [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
        public float CurrentlyFeasible { get; set; }

        [RunParameter( "Name", "SchoolBus", "The name of the mode" )]
        public string ModeName { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        public string NetworkType
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

        [RunParameter( "Observed Mode Character Code", 'A', "The character code used for model estimation." )]
        public char ObservedMode
        {
            get;
            set;
        }

        /// <summary>
        /// School bus should be 11 but return type should be changed to string for this
        /// </summary>
        [RunParameter( "Observed Signature Code", 'A', "The character code used for model output." )]
        public char OutputSignature
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
        /// This does not require a personal vehicle
        /// </summary>
        [DoNotAutomate]
        public IVehicleType RequiresVehicle
        {
            get { return null; }
        }

        [RunParameter( "Variance Scale", 1.0f, "The scale for varriance used for variance testing." )]
        public double VarianceScale
        {
            get;
            set;
        }

        /// <summary>
        ///
        /// </summary>
        public double CalculateV(ITrip trip)
        {
            double v = 0;

            v += CSchoolBus;

            if ( trip.TripChain.Person.Licence )
                v += DriversLicence;

            if ( trip.TripChain.Person.Youth )
            {
                v += YouthPassenger;
                v += YouthWalk;
            }
            if ( trip.TripChain.Person.YoungAdult )
            {
                v += YoungAdultPassenger;
                v += YoungAdultWalk;
            }

            if ( trip.Purpose == Activity.School )
            {
                v += SchoolPurpose;
            }
            else if ( trip.TripNumber > 1 && trip.TripChain.Trips[trip.TripChain.Trips.IndexOf( trip ) - 1].Purpose == Activity.School )
            {
                v += SchoolPurpose;
            }
            v += Data.TravelTime( trip.OriginalZone, trip.DestinationZone, trip.ActivityStartTime ).ToMinutes() * Distance;
            v += trip.TripChain.Person.Age * Age;
            return v;
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            float v = 0;
            v += CSchoolBus;
            v += Data.TravelTime( origin, destination, time ).ToMinutes() * Distance;
            return v;
        }

        /// <summary>
        /// School buses are free I believe
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="destination"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        public float Cost(IZone origin, IZone destination, Time time)
        {
            return 0;
        }

        public bool Feasible(IZone origin, IZone destination, Time timeOfDay)
        {
            return CurrentlyFeasible > 0;
        }

        /// <summary>
        /// Checking if the trip is feasible
        /// </summary>
        /// <param name="trip">the trip to test feasibility on</param>
        /// <returns>Is this trip feasible?</returns>
        public bool Feasible(ITrip trip)
        {
            int indexOfthisTrip = trip.TripChain.Trips.IndexOf( trip );

            return ( _AvailableZones.Contains( trip.OriginalZone.ZoneNumber )
                && _AvailableZones.Contains( trip.DestinationZone.ZoneNumber )
                && trip.TripChain.Person.StudentStatus != StudentStatus.NotStudent
                && DistanceRequirement( trip.OriginalZone, trip.DestinationZone, trip.TripChain.Person )
                && ( ( trip.Purpose == Activity.Home && trip.TripChain.Trips[indexOfthisTrip - 1].Purpose == Activity.School ) || trip.Purpose == Activity.School )
                && TravelTime( trip.OriginalZone, trip.DestinationZone, trip.TripStartTime ) > Time.Zero
                );
        }

        /// <summary>
        ///
        /// </summary>
        public bool Feasible(ITripChain tripChain)
        {
            return true;
        }

        /// <summary>
        ///
        /// </summary>
        public bool IsObservedMode(char observedMode)
        {
            return ( observedMode == ObservedMode );
        }

        /// <summary>
        ///
        /// </summary>
        public void ModeChoiceIterationComplete()
        {
            //do nothing here
        }

        /// <summary>
        /// This is called before the start method as a way to pre-check that all of the parameters that are selected
        /// are in fact valid for this module.
        /// </summary>
        /// <param name="error">A string that should be assigned a detailed error</param>
        /// <returns>If the validation was successful or if there was a problem</returns>
        public bool RuntimeValidation(ref string error)
        {
            if ( TashaRuntime == null )
            {
                error = "The Root Model System for the taxi mode was never loaded!";
                return false;
            }
            if ( TashaRuntime.NetworkData == null )
            {
                error = "There was no network data in this model system to load from!";
                return false;
            }
            bool found = false;
            foreach ( var data in TashaRuntime.NetworkData )
            {
                if ( data.NetworkType == AutoNetworkName )
                {
                    found = true;
                    Data = data;
                    break;
                }
            }
            if ( !found )
            {
                error = "We could not find any data named " + AutoNetworkName + " to load as the auto network data for the Taxi mode!";
                return false;
            }
            return true;
        }

        /// <summary>
        ///
        /// </summary>
        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return Data.TravelTime( origin, destination, time );
        }

        private bool DistanceRequirement(IZone origin, IZone destination, ITashaPerson iPerson)
        {
            int grade = GetGrade( iPerson );
            double distance = origin.Distance( destination );
            if ( ( grade > 0 ) & ( grade < 6 ) )
                return distance > 1600;
            if ( grade < 9 )
                return distance > 3200;
            if ( grade < 13 )
                return distance > 4800;
            return false;
        }

        private int GetGrade(ITashaPerson iPerson)
        {
            // ignore the problem of starting in September since people on the other end should balance this out
            int baseYear = 5;
            return iPerson.Age - baseYear;
        }
    }
}