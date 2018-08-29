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
using Datastructure;
using System;
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
    public sealed class Walk : ITashaMode, IIterationSensitive
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

        [RunParameter("ProfessionalWalkTimeFactor", 0f, "The Walk applied to the person type.")]
        public float ProfessionalWalk;
        [RunParameter("GeneralWalkTimeFactor", 0f, "The Walk applied to the person type.")]
        public float GeneralWalk;
        [RunParameter("SalesWalkTimeFactor", 0f, "The Walk applied to the person type.")]
        public float SalesWalk;
        [RunParameter("ManufacturingWalkTimeFactor", 0f, "The Walk applied to the person type.")]
        public float ManufacturingWalk;
        [RunParameter("StudentWalkTimeFactor", 0f, "The Walk applied to the person type.")]
        public float StudentWalk;
        [RunParameter("NonWorkerStudentWalkTimeFactor", 0f, "The Walk applied to the person type.")]
        public float NonWorkerStudentWalk;

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

        [SubModelInformation(Required = false, Description = "Constants for time of day")]
        public TimePeriodSpatialConstant[] TimePeriodConstants;

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

        /// <summary>
        /// Does not require any kind of vehicle
        /// </summary>
        [DoNotAutomate]
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
            ITashaPerson person = trip.TripChain.Person;
            GetPersonVariables(person, out float constant, out float walkBeta);
            v += constant;

            //if person has a license
            if ( person.Licence )
            {
                v += DriversLicenseFlag;
            }

            IZone origin = trip.OriginalZone;
            IZone destination = trip.DestinationZone;
            Time startTime = trip.ActivityStartTime;
            v += TravelTime(origin, destination, startTime).ToMinutes() * walkBeta;


            //checking if child
            if ( person.Youth )
            {
                v += YouthFlag;
            }
            else if ( person.YoungAdult )
            {
                v += YoungAdultFlag;
            }
            else if ( person.Child )
            {
                v += ChildFlag;
            }

            //if intrazonal trip
            if (origin == destination)
            {
                v += IntrazonalConstant;
            }

            //if no vehicles
            if ( person.Household.Vehicles.Length == 0 )
            {
                v += NoVehicleFlag;
            }
            switch ( trip.Purpose )
            {
                case Activity.Market:
                case Activity.JointMarket:
                    v += MarketFlag;
                    break;

                case Activity.JointOther:
                case Activity.IndividualOther:
                    v += OtherFlag;
                    break;

                case Activity.School:
                    v += SchoolFlag;
                    break;
            }
            return v + GetPlanningDistrictConstant(startTime, origin.PlanningDistrict, destination.PlanningDistrict);
        }

        public float GetPlanningDistrictConstant(Time startTime, int pdO, int pdD)
        {
            for(int i = 0; i < TimePeriodConstants.Length; i++)
            {
                if(startTime >= TimePeriodConstants[i].StartTime && startTime < TimePeriodConstants[i].EndTime)
                {
                    return TimePeriodConstants[i].GetConstant(pdO, pdD);
                }
            }
            return 0f;
        }

        private void GetPersonVariables(ITashaPerson person, out float constant, out float walk)
        {
            if(person.EmploymentStatus == TTSEmploymentStatus.FullTime)
            {
                switch(person.Occupation)
                {
                    case Occupation.Professional:
                        constant = ProfessionalConstant;
                        walk = ProfessionalWalk;
                        return;
                    case Occupation.Office:
                        constant = GeneralConstant;
                        walk = GeneralWalk;
                        return;
                    case Occupation.Retail:
                        constant = SalesConstant;
                        walk = SalesWalk;
                        return;
                    case Occupation.Manufacturing:
                        constant = ManufacturingConstant;
                        walk = ManufacturingWalk;
                        return;
                }
            }
            switch(person.StudentStatus)
            {
                case StudentStatus.FullTime:
                case StudentStatus.PartTime:
                    constant = StudentConstant;
                    walk = StudentWalk;
                    return;
            }
            if(person.EmploymentStatus == TTSEmploymentStatus.PartTime)
            {
                switch(person.Occupation)
                {
                    case Occupation.Professional:
                        constant = ProfessionalConstant;
                        walk = ProfessionalWalk;
                        return;
                    case Occupation.Office:
                        constant = GeneralConstant;
                        walk = GeneralWalk;
                        return;
                    case Occupation.Retail:
                        constant = SalesConstant;
                        walk = SalesWalk;
                        return;
                    case Occupation.Manufacturing:
                        constant = ManufacturingConstant;
                        walk = ManufacturingWalk;
                        return;
                }
            }
            constant = NonWorkerStudentConstant;
            walk = NonWorkerStudentWalk;
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
            return _distances[origin.ZoneNumber, destination.ZoneNumber]
                <= MaxWalkDistance;
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

        [SubModelInformation(Required = false, Description = "A custom set of distances if the paths differ from the zone system's distance matrix")]
        public IDataSource<SparseTwinIndex<float>> CustomDistances;

        private SparseTwinIndex<float> _distances;

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
            float distance = _distances[origin.ZoneNumber, destination.ZoneNumber];
            return Time.FromMinutes( (float)( distance / AvgWalkSpeed ) );
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
            AvgWalkSpeed = AvgWalkSpeedInKmPerHour * 1000f / 60f;
            return true;
        }

        public void IterationEnding(int iterationNumber, int maxIterations)
        {
            
        }

        public void IterationStarting(int iterationNumber, int maxIterations)
        {
            if(CustomDistances != null)
            {
                if(!CustomDistances.Loaded)
                {
                    CustomDistances.LoadData();
                    _distances = CustomDistances.GiveData();
                    CustomDistances.UnloadData();
                }
                else
                {
                    _distances = CustomDistances.GiveData();
                }
            }
            else
            {
                _distances = Root.ZoneSystem.Distances;
            }
            for(int i = 0; i < TimePeriodConstants.Length; i++)
            {
                TimePeriodConstants[i].BuildMatrix();
            }
        }
    }
}