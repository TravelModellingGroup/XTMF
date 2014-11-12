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
using Datastructure;
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.V4Modes
{
    /// <summary>
    ///
    /// </summary>
    [ModuleInformation( Description =
        @"This module is designed to implement the Walk mode for GTAModel V4.0+." )]
    public sealed class Walk : ITashaMode
    {
        [RootModule]
        public ITashaRuntime Root;

        [RunParameter( "Average walking speed", 4.5f, "The walking speed in km/h." )]
        public float AvgWalkSpeedInKmPerHour;

        [RunParameter("ProfessionalConstant", 0f, "The constant applied to the person type.")]
        public float ProfessionalConstant;
        [RunParameter("GeneralConstant", 0f, "The constant applied to the person type.")]
        public float GeneralConstant;
        [RunParameter("SalesConstant", 0f, "The constant applied to the person type.")]
        public float SalesConstant;
        [RunParameter("ManufacturingConstant", 0f, "The constant applied to the person type.")]
        public float ManufacturingConstant;
        [RunParameter("StudentConstant", 0f, "The constant applied to the person type.")]
        public float StudentConstant;
        [RunParameter("NonWorkerStudentConstant", 0f, "The constant applied to the person type.")]
        public float NonWorkerStudentConstant;

        [RunParameter( "DriversLicenseFlag", 0.0f, "The constant factor for having a driver's license" )]
        public float DriversLicenseFlag;

        [RunParameter( "Intrazonal", 0f, "The factor applied for being an intrazonal trip" )]
        public float IntrazonalConstant;

        [RunParameter( "MarketFlag", 0f, "Added to the utility if the trip's purpose is market." )]
        public float MarketFlag;

        [RunParameter( "Max Walking Distance", 4000, "The largest distance (Manhattan) allowed for walking" )]
        public float MaxWalkDistance;

        [RunParameter( "NoVehicleFlag", 0.0f, "Added to the utility if the household has no vehicle" )]
        public float NoVehicleFlag;

        [RunParameter( "OtherFlag", 0f, "Added to the utility if the trip's purpose is 'other'." )]
        public float OtherFlag;

        [RunParameter( "SchoolFlag", 0f, "Added to the utility if the trip's purpose is 'School'." )]
        public float SchoolFlag;

        [RunParameter( "TravelTimeFactor", 0.0f, "The factor for the distance walked" )]
        public float TravelTimeFactor;

        [RunParameter( "YoungAdultFlag", 0.0f, "The constant factor for being a young adult" )]
        public float YoungAdultFlag;

        [RunParameter( "YouthFlag", 0.0f, "The constant factor for being a youth" )]
        public float YouthFlag;

        [RunParameter("ChildFlag", 0f, "Added to the utility if the person is a child.")]
        public float ChildFlag;

        private float AvgWalkSpeed;

        [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
        public float CurrentlyFeasible { get; set; }

        [RunParameter( "Mode Name", "Walk", "The name of the mode" )]
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
            double v = 0;
            ITashaPerson Person = trip.TripChain.Person;
            GetPersonVariables(Person, out float constant);
            v += constant;

            //if person has a license
            if ( Person.Licence )
            {
                v += this.DriversLicenseFlag;
            }

            v += TravelTime( trip.OriginalZone, trip.DestinationZone, trip.ActivityStartTime ).ToMinutes() 
                * this.TravelTimeFactor;

            //checking if child
            if ( Person.Youth )
            {
                v += YouthFlag;
            }
            else if ( Person.YoungAdult )
            {
                v += YoungAdultFlag;
            }
            else if ( Person.Child )
            {
                v += ChildFlag;
            }

            //if intrazonal trip
            if ( trip.OriginalZone == trip.DestinationZone )
            {
                v += this.IntrazonalConstant;
            }

            //if no vehicles
            if ( Person.Household.Vehicles.Length == 0 )
            {
                v += NoVehicleFlag;
            }
            switch ( trip.Purpose )
            {
                case Activity.Market:
                case Activity.JointMarket:
                    v += this.MarketFlag;
                    break;

                case Activity.JointOther:
                case Activity.IndividualOther:
                    v += this.OtherFlag;
                    break;

                case Activity.School:
                    v += this.SchoolFlag;
                    break;
            }
            return v;
        }

        private void GetPersonVariables(ITashaPerson person, out float constant)
        {
            if(person.EmploymentStatus == TTSEmploymentStatus.FullTime)
            {
                switch(person.Occupation)
                {
                    case Occupation.Professional:
                        constant = ProfessionalConstant;
                        return;
                    case Occupation.Office:
                        constant = GeneralConstant;
                        return;
                    case Occupation.Retail:
                        constant = SalesConstant;
                        return;
                    case Occupation.Manufacturing:
                        constant = ManufacturingConstant;
                        return;
                }
            }
            switch(person.StudentStatus)
            {
                case StudentStatus.FullTime:
                case StudentStatus.PartTime:
                    constant = StudentConstant;
                    return;
            }
            constant = NonWorkerStudentConstant;
            return;
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            return float.NaN;
        }

        public float Cost(IZone origin, IZone destination, Time time)
        {
            return 0;
        }

        public bool Feasible(IZone origin, IZone destination, Time timeOfDay)
        {
            return this.Root.ZoneSystem.Distances[origin.ZoneNumber, destination.ZoneNumber]
                <= MaxWalkDistance;
        }

        /// <summary>
        /// The Feasibility of Walking for a given Trip
        /// </summary>
        /// <param name="trip">The Trip to calculate feasibility on</param>
        /// <returns>true if the Trip is feasible for walking</returns>
        public bool Feasible(ITrip trip)
        {
            return this.Feasible( trip.OriginalZone, trip.DestinationZone, trip.ActivityStartTime );
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
            double distance = origin == destination ? origin.InternalDistance
                : this.Root.ZoneSystem.Distances[origin.ZoneNumber, destination.ZoneNumber];
            Time ret = Time.FromMinutes( (float)( distance / this.AvgWalkSpeed ) );
            return ret;
        }

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
        public double VarianceScale { get; set; }

        /// <summary>
        /// This is called before the start method as a way to pre-check that all of the parameters that are selected
        /// are in fact valid for this module.
        /// </summary>
        /// <param name="error">A string that should be assigned a detailed error</param>
        /// <returns>If the validation was successful or if there was a problem</returns>
        public bool RuntimeValidation(ref string error)
        {
            this.AvgWalkSpeed = this.AvgWalkSpeedInKmPerHour * 1000f / 60f;
            return true;
        }
    }
}