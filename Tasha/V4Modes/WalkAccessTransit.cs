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
using Datastructure;
namespace Tasha.V4Modes
{
    [ModuleInformation(Description =
        @"This module is designed to implement the Walk access transit mode for GTAModel V4.0+.")]
    public class WalkAccessTransit : ITashaMode, IIterationSensitive
    {
        [RunParameter("Female Flag", 0f, "Added to the utility if the person is female.")]
        public float FemaleFlag;

        [RunParameter("IntrazonalConstant", 0f, "The mode constant.")]
        public float IntrazonalConstant;

        [RunParameter("IntrazonalTripDistanceFactor", 0f, "The factor to apply to the intrazonal trip distance.")]
        public float IntrazonalTripDistanceFactor;

        [RunParameter("MarketFlag", 0f, "Added to the utility if the trip's purpose is market.")]
        public float MarketFlag;

        [RunParameter("OtherFlag", 0f, "Added to the utility if the trip's purpose is 'other'.")]
        public float OtherFlag;

        [RootModule]
        public ITashaRuntime Root;

        [RunParameter("ProfessionalTravelCostFactor", 0f, "The factor applied to the travel cost ($'s).")]
        public float ProfessionalCostFactor;

        [RunParameter("GeneralTravelCostFactor", 0f, "The factor applied to the travel cost ($'s).")]
        public float GeneralCostFactor;

        [RunParameter("SalesTravelCostFactor", 0f, "The factor applied to the travel cost ($'s).")]
        public float SalesCostFactor;

        [RunParameter("ManufacturingTravelCostFactor", 0f, "The factor applied to the travel cost ($'s).")]
        public float ManufacturingCostFactor;

        [RunParameter("StudentTravelCostFactor", 0f, "The factor applied to the travel cost ($'s).")]
        public float StudentCostFactor;

        [RunParameter("NonWorkerStudentTravelCostFactor", 0f, "The factor applied to the travel cost ($'s).")]
        public float NonWorkerStudentCostFactor;

        [RunParameter("Use Cost As Factor Of Time", false, "Should we treat the cost factors as a factor of their in vehicle time weighting.")]
        public bool UseCostAsFactorOfTime;

        private float ProfessionalCost;
        private float GeneralCost;
        private float SalesCost;
        private float ManufacturingCost;
        private float StudentCost;
        private float NonWorkerStudentCost;

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

        [RunParameter("ProfessionalTimeFactor", 0f, "The TimeFactor applied to the person type.")]
        public float ProfessionalTimeFactor;
        [RunParameter("GeneralTimeFactor", 0f, "The TimeFactor applied to the person type.")]
        public float GeneralTimeFactor;
        [RunParameter("SalesTimeFactor", 0f, "The TimeFactor applied to the person type.")]
        public float SalesTimeFactor;
        [RunParameter("ManufacturingTimeFactor", 0f, "The TimeFactor applied to the person type.")]
        public float ManufacturingTimeFactor;
        [RunParameter("StudentTimeFactor", 0f, "The TimeFactor applied to the person type.")]
        public float StudentTimeFactor;
        [RunParameter("NonWorkerStudentTimeFactor", 0f, "The TimeFactor applied to the person type.")]
        public float NonWorkerStudentTimeFactor;

        [RunParameter("WalkTimeFactor", 0f, "The factor applied to the walk time (minutes).")]
        public float WalkTimeFactor;

        [RunParameter("WaitTimeFactor", 0f, "The factor applied to the wait time (minutes).")]
        public float WaitTimeFactor;

        [RunParameter("BoardingFactor", 0f, "The factor applied to the boarding penalties.")]
        public float BoardingFactor;

        [RunParameter("Vehicle Type", "Auto", "The name of the type of vehicle to use.")]
        public string VehicleTypeName;

        [RunParameter("LogOfAgeFactor", 0f, "The factor applied to the log of age.")]
        public float LogOfAgeFactor;

        [RunParameter("LogOfAgeBase", (float)Math.E, "The base used for computing the log of age.")]
        public float LogOfAgeBase;

        [RunParameter("InterRegionalTrip", 0.0f, "A constant applied to trips that go between regions.")]
        public float InterRegionalTrip;

        private ITripComponentData Network;

        [Parameter("Feasible", 1f, "Is the mode feasible?(1)")]
        public float CurrentlyFeasible { get; set; }

        [Parameter("Mode Name", "WAT", "The name of the mode.")]
        public string ModeName { get; set; }

        public string Name { get; set; }

        [RunParameter("Network Name", "Transit", "The name of the network to use for times.")]
        public string NetworkType { get; set; }

        [RunParameter("ToActivityDensityFactor", 0.0f, "The factor to apply to the destination of the activity's density.")]
        public float ToActivityDensityFactor;

        [RunParameter("ToHomeDensityFactor", 0.0f, "The factor to apply to the destination of the activity's density.")]
        public float ToHomeDensityFactor;

        public bool NonPersonalVehicle
        {
            get { return RequiresVehicle == null; }
        }

        public float Progress
        {
            get { return 0f; }
        }

        private float[] AgeUtilLookup;

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        [DoNotAutomate]
        public IVehicleType RequiresVehicle { get; set; }

        [RunParameter("Variance Scale", 1.0, "The factor applied to the error term.")]
        public double VarianceScale { get; set; }

        public double CalculateV(ITrip trip)
        {
            // compute the non human factors
            var zoneSystem = Root.ZoneSystem;
            var zoneArray = zoneSystem.ZoneArray;
            IZone originalZone = trip.OriginalZone;
            var o = zoneArray.GetFlatIndex(originalZone.ZoneNumber );
            IZone destinationZone = trip.DestinationZone;
            var d = zoneArray.GetFlatIndex(destinationZone.ZoneNumber );
            var p = trip.TripChain.Person;
            float timeFactor, constant, costFactor;
            GetPersonVariables(p, out timeFactor, out constant, out costFactor);
            float v = constant;
            // if Intrazonal
            if ( o == d )
            {
                v += IntrazonalConstant;
                v += IntrazonalTripDistanceFactor * zoneSystem.Distances.GetFlatData()[o][d] * 0.001f;
            }
            else
            {
                // if not intrazonal
                if(originalZone.RegionNumber != destinationZone.RegionNumber)
                {
                    v += InterRegionalTrip;
                }
                float ivtt, walk, wait, boarding, cost;
                if ( Network.GetAllData( o, d, trip.TripStartTime, out ivtt, out walk, out wait, out boarding, out cost) )
                {
                    v += ivtt * timeFactor
                        + walk * WalkTimeFactor
                        + wait * WaitTimeFactor
                        + boarding * BoardingFactor
                        + cost * costFactor;
                }
                else
                {
                    return float.NegativeInfinity;
                }
            }
            // Apply personal factors
            if ( p.Female )
            {
                v += FemaleFlag;
            }
            var age = p.Age;
            v += AgeUtilLookup[Math.Min( Math.Max( age - 15, 0 ), 15 )];
            //Apply trip purpose factors
            switch ( trip.Purpose )
            {
                case Activity.Market:
                    v += MarketFlag + ZonalDensityForActivitiesArray[d];
                    break;
                case Activity.IndividualOther:
                    v += OtherFlag + ZonalDensityForActivitiesArray[d];
                    break;
                case Activity.Home:
                    v += ZonalDensityForHomeArray[d];
                    break;
                default:
                    v += ZonalDensityForActivitiesArray[d];
                    break;
            }
            v += GetPlanningDistrictConstant(trip.TripStartTime, originalZone.PlanningDistrict, destinationZone.PlanningDistrict);
            return (double)v;
        }

        private void GetPersonVariables(ITashaPerson person, out float time, out float constant, out float cost)
        {
            if(person.EmploymentStatus == TTSEmploymentStatus.FullTime)
            {
                switch(person.Occupation)
                {
                    case Occupation.Professional:
                        cost = ProfessionalCostFactor;
                        constant = ProfessionalConstant;
                        time = ProfessionalTimeFactor;
                        return;
                    case Occupation.Office:
                        cost = GeneralCostFactor;
                        constant = GeneralConstant;
                        time = GeneralTimeFactor;
                        return;
                    case Occupation.Retail:
                        cost = SalesCostFactor;
                        constant = SalesConstant;
                        time = SalesTimeFactor;
                        return;
                    case Occupation.Manufacturing:
                        cost = ManufacturingCostFactor;
                        constant = ManufacturingConstant;
                        time = ManufacturingTimeFactor;
                        return;
                }
            }
            switch(person.StudentStatus)
            {
                case StudentStatus.FullTime:
                case StudentStatus.PartTime:
                    cost = StudentCostFactor;
                    constant = StudentConstant;
                    time = StudentTimeFactor;
                    return;
            }
            if(person.EmploymentStatus == TTSEmploymentStatus.PartTime)
            {
                switch(person.Occupation)
                {
                    case Occupation.Professional:
                        cost = ProfessionalCostFactor;
                        constant = ProfessionalConstant;
                        time = ProfessionalTimeFactor;
                        return;
                    case Occupation.Office:
                        cost = GeneralCostFactor;
                        constant = GeneralConstant;
                        time = GeneralTimeFactor;
                        return;
                    case Occupation.Retail:
                        cost = SalesCostFactor;
                        constant = SalesConstant;
                        time = SalesTimeFactor;
                        return;
                    case Occupation.Manufacturing:
                        cost = ManufacturingCostFactor;
                        constant = ManufacturingConstant;
                        time = ManufacturingTimeFactor;
                        return;
                }
            }
            cost = NonWorkerStudentCostFactor;
            constant = NonWorkerStudentConstant;
            time = NonWorkerStudentTimeFactor;
            return;
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            return 0f;
        }

        public float Cost(IZone origin, IZone destination, Time time)
        {
            return Network.TravelCost( origin, destination, time );
        }

        public bool Feasible(ITrip trip)
        {
            return true;
        }

        public bool Feasible(ITripChain tripChain)
        {
            return true;
        }

        public bool Feasible(IZone origin, IZone destination, Time time)
        {
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            var networks = Root.NetworkData;
            if ( string.IsNullOrWhiteSpace( NetworkType ) )
            {
                error = "There was no network type selected for the " + ( string.IsNullOrWhiteSpace( ModeName ) ? "Walk access transit" : ModeName ) + " mode!";
                return false;
            }
            if(!ZonalDensityForActivities.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the resource for Zonal Density For Activities was of the wrong type!";
                return false;
            }
            if(!ZonalDensityForHome.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the resource for Zonal Density For Home was of the wrong type!";
                return false;
            }
            if ( networks == null )
            {
                error = "There was no Auto Network loaded for the Transit Mode!";
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
                    Network = network as ITripComponentData;
                    return Network != null;
                }
            }
            return false;
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

        [SubModelInformation(Description = "Constants for time of day")]
        public TimePeriodSpatialConstant[] TimePeriodConstants;

        [SubModelInformation(Required = true, Description = "The density of zones for activities")]
        public IResource ZonalDensityForActivities;

        [SubModelInformation(Required = true, Description = "The density of zones for home")]
        public IResource ZonalDensityForHome;

        private float[] ZonalDensityForActivitiesArray;
        private float[] ZonalDensityForHomeArray;

        public void IterationStarting(int iterationNumber, int maxIterations)
        {
            // We do this here instead of the RuntimeValidation so that we don't run into issues with estimation
            AgeUtilLookup = new float[16];
            for ( int i = 0; i < AgeUtilLookup.Length; i++ )
            {
                AgeUtilLookup[i] = (float)Math.Log( i + 1, Math.E ) * LogOfAgeFactor;
            }
            for(int i = 0; i < TimePeriodConstants.Length; i++)
            {
                TimePeriodConstants[i].BuildMatrix();
            }
            ZonalDensityForActivitiesArray = ZonalDensityForActivities.AquireResource<SparseArray<float>>().GetFlatData().Clone() as float[];
            ZonalDensityForHomeArray = ZonalDensityForHome.AquireResource<SparseArray<float>>().GetFlatData().Clone() as float[];
            for(int i = 0; i < ZonalDensityForActivitiesArray.Length; i++)
            {
                ZonalDensityForActivitiesArray[i] *= ToActivityDensityFactor;
                ZonalDensityForHomeArray[i] *= ToHomeDensityFactor;
            }

            if(UseCostAsFactorOfTime)
            {
                ProfessionalCost = ProfessionalCostFactor * ProfessionalTimeFactor;
                GeneralCost = GeneralCostFactor * ProfessionalTimeFactor;
                SalesCost = SalesCostFactor * ProfessionalTimeFactor;
                ManufacturingCost = ManufacturingCostFactor * ProfessionalTimeFactor;
                StudentCost = StudentCostFactor * ProfessionalTimeFactor;
                NonWorkerStudentCost = NonWorkerStudentCostFactor * ProfessionalTimeFactor;
            }
            else
            {
                ProfessionalCost = ProfessionalCostFactor;
                GeneralCost = GeneralCostFactor;
                SalesCost = SalesCostFactor;
                ManufacturingCost = ManufacturingCostFactor;
                StudentCost = StudentCostFactor;
                NonWorkerStudentCost = NonWorkerStudentCostFactor;
            }
        }

        public void IterationEnding(int iterationNumber, int maxIterations)
        {
            ZonalDensityForActivities.ReleaseResource();
            ZonalDensityForHome.ReleaseResource();
        }
    }
}
