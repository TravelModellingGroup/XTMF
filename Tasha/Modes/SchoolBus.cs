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

        private string _availableZones;

        private List<int> AvailableZones;

        [DoNotAutomate]
        private INetworkData data;

        /// <summary>
        ///
        /// </summary>
        public SchoolBus()
        {
        }

        [RunParameter( "AvailableZones", "0-2200", "Example \"0-2200,3000-3500\" is all of the zones between 0 and 2200 and all of the zones between 3000 and 3500." )]
        public string availableZones
        {
            get
            {
                return _availableZones;
            }

            set
            {
                this._availableZones = value;
                this.AvailableZones = Common.ConvertToIntList( value );
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

        [DoNotAutomate]
        /// <summary>
        /// This does not require a personal vehicle
        /// </summary>
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
            double V = 0;

            V += this.CSchoolBus;

            if ( trip.TripChain.Person.Licence )
                V += this.DriversLicence;

            if ( trip.TripChain.Person.Youth )
            {
                V += this.YouthPassenger;
                V += this.YouthWalk;
            }
            if ( trip.TripChain.Person.YoungAdult )
            {
                V += this.YoungAdultPassenger;
                V += this.YoungAdultWalk;
            }

            if ( trip.Purpose == Activity.School )
            {
                V += SchoolPurpose;
            }
            else if ( trip.TripNumber > 1 && trip.TripChain.Trips[trip.TripChain.Trips.IndexOf( trip ) - 1].Purpose == Activity.School )
            {
                V += SchoolPurpose;
            }
            V += this.data.TravelTime( trip.OriginalZone, trip.DestinationZone, trip.ActivityStartTime ).ToMinutes() * this.Distance;
            V += trip.TripChain.Person.Age * this.Age;
            return V;
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            float V = 0;
            V += this.CSchoolBus;
            V += this.data.TravelTime( origin, destination, time ).ToMinutes() * this.Distance;
            return V;
        }

        /// <summary>
        /// School buses are free I believe
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="destination"></param>
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

            return ( this.AvailableZones.Contains( trip.OriginalZone.ZoneNumber )
                && this.AvailableZones.Contains( trip.DestinationZone.ZoneNumber )
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
            return ( observedMode == this.ObservedMode );
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
            if ( this.TashaRuntime == null )
            {
                error = "The Root Model System for the taxi mode was never loaded!";
                return false;
            }
            if ( this.TashaRuntime.NetworkData == null )
            {
                error = "There was no network data in this model system to load from!";
                return false;
            }
            bool found = false;
            foreach ( var data in this.TashaRuntime.NetworkData )
            {
                if ( data.NetworkType == this.AutoNetworkName )
                {
                    found = true;
                    this.data = data;
                    break;
                }
            }
            if ( !found )
            {
                error = "We could not find any data named " + this.AutoNetworkName + " to load as the auto network data for the Taxi mode!";
                return false;
            }
            return true;
        }

        /// <summary>
        ///
        /// </summary>
        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return this.data.TravelTime( origin, destination, time );
        }

        private bool DistanceRequirement(IZone iZone, IZone iZone_2, ITashaPerson iPerson)
        {
            int grade = GetGrade( iPerson );

            double distance = iZone.Distance( iZone_2 );

            if ( ( grade > 0 ) & ( grade < 6 ) )
                return distance > 1600;
            else if ( grade < 9 )
                return distance > 3200;
            else if ( grade < 13 )
                return distance > 4800;
            else
                return false;
        }

        private int GetGrade(ITashaPerson iPerson)
        {
            // ignore the problem of staritng in september since people on the other end should balance this out
            int baseYear = 5;
            int grade = iPerson.Age - baseYear;
            return grade;
        }
    }
}