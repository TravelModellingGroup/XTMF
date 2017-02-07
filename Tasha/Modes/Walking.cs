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
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Modes
{
    /// <summary>
    ///
    /// </summary>
    public sealed class Walking : ITashaMode
    {
        #region IMode Members

        public float AvgWalkSpeed;

        [RunParameter( "Average walking speed", 4.5f, "The walking speed in km/h." )]
        public float AvgWalkSpeedInKmPerHour;

        //config files
        [RunParameter( "Constant", 0.0f, "The constant factor for walking" )]
        public float CWalk;

        [RunParameter( "Drivers Licence", 0.0f, "The constant factor for having a driver's licence" )]
        public float DLicense;

        [RunParameter( "dpurp_oth_drive", 0f, "The weight for the cost of doing an other drive (ITashaRuntime only)" )]
        public float dpurp_oth_drive;

        [RunParameter( "dpurp_shop_drive", 0f, "The weight for the cost of doing a shopping drive (ITashaRuntime only)" )]
        public float dpurp_shop_drive;

        [RunParameter( "Intrazonal", 0f, "The factor applied for being an intrazonal trip" )]
        public float Intrazonal;

        [RunParameter( "Max Walking Distance", 4000, "The largest distance (Manhatten) allowed for walking" )]
        public float MaxWalkDistance;

        [RunParameter( "No Vehicle Constant", 0.0f, "The constant factor for being in a household with no vehicle" )]
        public float NoVehicle;

        /// <summary>
        /// Not sure what these stands for exactly?
        /// Should be replaced with a better name
        /// </summary>
        [RunParameter( "Peak Time Time", 0.0f, "The constant factor for travelling at a peak period" )]
        public float PeakTrip;

        [RunParameter( "Travel Time", 0.0f, "The factor for the distance walked" )]
        public float TravelTimeWeight;

        [RunParameter( "Young Adult Constant", 0.0f, "The constant factor for being a young adult" )]
        public float YoungAdult;

        [RunParameter( "Youth Constant", 0.0f, "The constant factor for being a youth" )]
        public float Youth;

        [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
        public float CurrentlyFeasible { get; set; }

        [RunParameter( "Name", "Walking", "The name of the mode" )]
        public string ModeName { get; set; }

        /// <summary>
        /// What is the name of this mode?
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        public bool NonPersonalVehicle
        {
            get { return true; }
        }

        /// <summary>
        ///
        /// </summary>
        [RunParameter( "Output Signature", 'A', "The character that should be used in output to represent this mode." )]
        public char OutputSignature
        {
            get;
            set;
        }

        [DoNotAutomate]
        /// <summary>
        /// Does not require any kind of vehicle
        /// </summary>
        public IVehicleType RequiresVehicle
        {
            get { return null; }
        }

        /// <summary>
        /// Calculates V Value for a given trip
        /// </summary>
        /// <param name="trip">The trip to calculate for</param>
        /// <returns>The V for the trip</returns>
        public double CalculateV(ITrip trip)
        {
            double V = 0;
            V += CWalk;

            ITashaPerson Person = trip.TripChain.Person;

            //if person has a license
            if ( Person.Licence )
            {
                V += DLicense;
            }

            V += TravelTime( trip.OriginalZone, trip.DestinationZone, trip.ActivityStartTime ).ToMinutes() * TravelTimeWeight;

            //if its Morning or Afternoon
            if ( ( Common.GetTimePeriod( trip.ActivityStartTime ) == TravelTimePeriod.Morning ) ||
                ( Common.GetTimePeriod( trip.ActivityStartTime ) == TravelTimePeriod.Afternoon ) )
            {
                V += PeakTrip;
            }

            //checking if child
            if ( Person.Youth )
            {
                V += Youth;
            }

            //checking if young adult
            if ( Person.YoungAdult )
            {
                V += YoungAdult;
            }

            //if intrazonal trip
            if ( trip.OriginalZone == trip.DestinationZone )
            {
                V += Intrazonal;
            }

            //if no vehicles
            if ( Person.Household.Vehicles.Length == 0 )
            {
                V += NoVehicle;
            }
            if ( trip.Purpose == Activity.Market | trip.Purpose == Activity.JointMarket )
            {
                V += dpurp_shop_drive;
            }
            else if ( trip.Purpose == Activity.IndividualOther | trip.Purpose == Activity.JointOther )
            {
                V += dpurp_oth_drive;
            }

            return V;
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            float V = 0;
            V += CWalk;

            V += TravelTime( origin, destination, time ).ToMinutes() * TravelTimeWeight;

            //if its Morning or Afternoon
            if ( ( Common.GetTimePeriod( time ) == TravelTimePeriod.Morning ) ||
                ( Common.GetTimePeriod( time ) == TravelTimePeriod.Afternoon ) )
            {
                V += PeakTrip;
            }

            if ( origin.ZoneNumber == destination.ZoneNumber )
            {
                V += Intrazonal;
            }

            return V;
        }

        public float Cost(IZone origin, IZone destination, Time time)
        {
            return 0;
        }

        public bool Feasible(IZone origin, IZone destination, Time timeOfDay)
        {
            return CurrentlyFeasible > 0 && origin.Distance( destination ) <= MaxWalkDistance;
        }

        /// <summary>
        /// The Feasibility of Walking for a given Trip
        /// </summary>
        /// <param name="trip">The Trip to calculate feasibility on</param>
        /// <returns>true if the Trip is feasible for walking</returns>
        public bool Feasible(ITrip trip)
        {
            return Feasible( trip.OriginalZone, trip.DestinationZone, trip.ActivityStartTime );
        }

        public bool Feasible(ITripChain tripChain)
        {
            return true;
        }

        /// <summary>
        /// The Time it takes to walk between two zones
        /// Time of day does not effect this for walking
        /// </summary>
        /// <param name="origin">The origin of Travel</param>
        /// <param name="destination">The destination of Travel</param>
        /// <param name="time">The Time of Day</param>
        /// <returns>The Time it takes to walk from origin to destination</returns>
        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            double distance = origin == destination ? origin.InternalDistance : origin.Distance( destination );
            Time ret = Time.FromMinutes( (float)( distance / AvgWalkSpeed ) );
            return ret;
        }

        /// <summary>
        /// Checks to see if all the trips start at the previous trips
        /// Destination in the trip chain. (ie. all trips in trip chain are connected)
        /// </summary>
        /// <param name="tripChain">TripChain to to check feasibility with</param>
        /// <returns>if its feasible</returns>

        #endregion IMode Members

        public string NetworkType
        {
            get { return null; }
        }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>( 100, 200, 100 ); }
        }

        [RunParameter( "Variance Scale", 1.0, "The scaling of the random term for this mode." )]
        public double VarianceScale
        {
            get;
            set;
        }

        /// <summary>
        /// This is called before the start method as a way to pre-check that all of the parameters that are selected
        /// are in fact valid for this module.
        /// </summary>
        /// <param name="error">A string that should be assigned a detailed error</param>
        /// <returns>If the validation was successful or if there was a problem</returns>
        public bool RuntimeValidation(ref string error)
        {
            AvgWalkSpeed = AvgWalkSpeedInKmPerHour * 1000f / 60f;
            return true;
        }
    }
}