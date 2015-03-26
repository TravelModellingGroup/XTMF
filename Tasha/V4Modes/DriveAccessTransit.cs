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
using Datastructure;
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.V4Modes
{
    [ModuleInformation(Description =
        @"This module is designed to implement the Drive access transit mode for GTAModel V4.0+.")]
    public class DriveAccessTransit : ITourDependentMode, IIterationSensitive
    {
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

        [RunParameter("Female Flag", 0f, "Added to the utility if the person is female.")]
        public float FemaleFlag;

        [RunParameter("MarketFlag", 0f, "Added to the utility if the trip's purpose is market.")]
        public float MarketFlag;

        [RunParameter("OtherFlag", 0f, "Added to the utility if the trip's purpose is 'other'.")]
        public float OtherFlag;

        [RootModule]
        public ITashaRuntime Root;

        [RunParameter("Vehicle Type", "Auto", "The name of the type of vehicle to use.")]
        public string VehicleTypeName;

        [RunParameter("LogOfAgeFactor", 0f, "The factor applied to the log of age.")]
        public float LogOfAgeFactor;

        [RunParameter("Over65", 0f, "The factor applied if the person is over the age of 65..")]
        public float Over65;

        [RunParameter("Over55", 0f, "The factor applied if the person is over the age of 55, but less than 65.")]
        public float Over55;

        [RunParameter("aivtt", 0.0f, "The time spent in the auto vehicle")]
        public float AutoInVehicleTime;

        [RunParameter("tivtt", 0.0f, "The time spent in a public transit vehicle")]
        public float TransitInVehicleTime;

        [RunParameter("twait", 0.0f, "The time spent in a public transit vehicle")]
        public float TransitWait;

        [RunParameter("twalk", 0.0f, "The time spent in a public transit vehicle")]
        public float TransitWalk;

        [RunParameter("LogsumFactor", 0.0f, "The factor to apply to the logsum of the access station choice model.")]
        public float LogsumCorrelation;

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

        private INetworkData AutoNetwork;
        private ITripComponentData TransitNetwork;

        [SubModelInformation(Required = true, Description = "The model that determines what station we need to get off at.")]
        public IAccessStationChoiceModel AccessStationModel;

        [Parameter("Feasible", 1f, "Is the mode feasible? (Set to 1)")]
        public float CurrentlyFeasible { get; set; }

        [Parameter("Mode Name", "DAT", "The name of the mode.")]
        public string ModeName { get; set; }

        public string Name { get; set; }

        public string NetworkType { get; set; }

        public bool NonPersonalVehicle
        {
            get { return RequiresVehicle == null; }
        }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        [DoNotAutomate]
        public IVehicleType RequiresVehicle { get; set; }

        [RunParameter("Variance Scale", 1.0, "The factor applied to the error term.")]
        public double VarianceScale { get; set; }

        [RunParameter("Random Seed", 12345, "The random seed to use for selecting a discreet station.")]
        public int RandomSeed;

        private float[] AgeUtilLookup;

        public double CalculateV(ITrip trip)
        {
            // compute the non human factors
            var zoneSystem = Root.ZoneSystem;
            var zoneArray = zoneSystem.ZoneArray;
            var o = zoneArray.GetFlatIndex( trip.OriginalZone.ZoneNumber );
            var d = zoneArray.GetFlatIndex( trip.DestinationZone.ZoneNumber );
            
            // if Intrazonal
            if ( o == d )
            {
                return float.NaN;
            }
            // Apply personal factors
            var p = trip.TripChain.Person;
            float constant;
            GetPersonVariables(p, out constant);
            float v = constant;
            if ( p.Female )
            {
                v += FemaleFlag;
            }
            var age = p.Age;
            v += AgeUtilLookup[Math.Min( Math.Max( age - 15, 0 ), 15 )];
            if ( age >= 65 )
            {
                v += Over65;
            }
            else if ( age >= 55 )
            {
                v += Over55;
            }
            //Apply trip purpose factors
            switch ( trip.Purpose )
            {
                case Activity.Market:
                    v += MarketFlag;
                    break;
                case Activity.IndividualOther:
                    v += OtherFlag;
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

        private float GetTravelCostFactor(ITashaPerson person)
        {
            if ( person.EmploymentStatus == TTSEmploymentStatus.FullTime )
            {
                switch ( person.Occupation )
                {
                    case Occupation.Professional:
                        return ProfessionalCostFactor;
                    case Occupation.Office:
                        return GeneralCostFactor;
                    case Occupation.Retail:
                        return SalesCostFactor;
                    case Occupation.Manufacturing:
                        return ManufacturingCostFactor;
                }
            }
            switch ( person.StudentStatus )
            {
                case StudentStatus.FullTime:
                case StudentStatus.PartTime:
                    return StudentCostFactor;
            }
            return NonWorkerStudentCostFactor;
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            return 0f;
        }

        public float Cost(IZone origin, IZone destination, Time time)
        {
            return float.NaN;
        }

        public bool Feasible(ITrip trip)
        {
            if ( trip.OriginalZone == trip.DestinationZone ) return false;
            return trip.TripChain.Person.Licence;
        }

        public bool Feasible(ITripChain tripChain)
        {
            return true;
        }

        public bool Feasible(IZone origin, IZone destination, Time time)
        {
            return true;
        }

        [RunParameter("Auto Network", "Auto", "The name of the auto network.")]
        public string AutoNetworkName;

        [RunParameter("Transit Network", "Transit", "The name of the transit network.")]
        public string TransitNetworkName;

        public bool RuntimeValidation(ref string error)
        {
            foreach ( var network in Root.NetworkData )
            {
                if ( network.NetworkType == AutoNetworkName )
                {
                    AutoNetwork = network;
                }
                else if ( network.NetworkType == TransitNetworkName )
                {
                    TransitNetwork = network as ITripComponentData;
                }
            }
            if ( AutoNetwork == null )
            {
                error = "In '" + Name + "' we were unable to find an auto network called '" + AutoNetworkName + "'";
                return false;
            }
            if ( TransitNetwork == null )
            {
                error = "In '" + Name + "' we were unable to find a transit network called '" + TransitNetworkName + "'";
                return false;
            }
            return true;
        }

        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return Time.Zero;
        }

        public bool CalculateTourDependentUtility(ITripChain chain, int tripIndex, out float dependentUtility, out Action<ITripChain> OnSelection)
        {
            bool first;
            var trips = chain.Trips;
            int otherIndex;
            int tripCount = CountTripsUsingThisMode( tripIndex, out first, out otherIndex, trips );

            if ( tripCount > 2 )
            {
                dependentUtility = float.NaN;
                OnSelection = null;
                return false;
            }
            if ( first )
            {
                var accessData = AccessStationModel.ProduceResult( chain );
                if ( accessData == null || !BuildUtility( trips[tripIndex].OriginalZone, trips[otherIndex].OriginalZone,
                    accessData,
                    trips[tripIndex].DestinationZone, trips[otherIndex].DestinationZone, GetTravelCostFactor(chain.Person), trips[tripIndex].TripStartTime,
                    out dependentUtility ) )
                {
                    OnSelection = null;
                    dependentUtility = float.NegativeInfinity;
                    return false;
                }
                int householdIteration = 0;
                OnSelection = (tripChain) =>
                {
                    var person = tripChain.Person;
                    var household = person.Household;
                    householdIteration++;
                    tripChain.Attach( "AccessStation", SelectAccessStation(
                            new Random( household.HouseholdId * person.Id * person.TripChains.IndexOf( tripChain ) * RandomSeed * householdIteration ),
                            accessData ) );
                };
            }
            else
            {
                dependentUtility = 0.0f;
                OnSelection = null;
            }
            return true;
        }

        private bool BuildUtility(IZone firstOrigin, IZone secondOrigin, Pair<IZone[], float[]> accessData, IZone firstDestination, IZone secondDestination, float travelCostFactor, Time time, out float dependentUtility)
        {
            var zones = accessData.First;
            var utils = accessData.Second;
            var totalUtil = 0.0f;
            bool any = false;
            for ( int i = 0; i < utils.Length && zones[i] != null; i++ )
            {
                if ( !float.IsNaN( utils[i] ) )
                {
                    totalUtil += utils[i];
                    any = true;
                }
            }
            if ( !any | totalUtil <= 0 )
            {
                dependentUtility = float.NaN;
                return false;
            }
            dependentUtility = LogsumCorrelation * (float)Math.Log( totalUtil );
            totalUtil = 1 / totalUtil;
            for ( int i = 0; i < utils.Length && zones[i] != null; i++ )
            {
                utils[i] *= totalUtil;
            }
            var zoneSystem = Root.ZoneSystem.ZoneArray;
            var fo = zoneSystem.GetFlatIndex( firstOrigin.ZoneNumber );
            var so = zoneSystem.GetFlatIndex( secondOrigin.ZoneNumber );
            var fd = zoneSystem.GetFlatIndex( firstDestination.ZoneNumber );
            var sd = zoneSystem.GetFlatIndex( secondDestination.ZoneNumber );
            totalUtil = 0;
            for ( int i = 0; i < utils.Length && zones[i] != null; i++ )
            {
                var stationIndex = zoneSystem.GetFlatIndex( zones[i].ZoneNumber );
                var probability = utils[i];
                if ( probability > 0 )
                {
                    var local = 0.0f;
                    float tivtt, twalk, twait, _unused, cost;
                    TransitNetwork.GetAllData( stationIndex, fd, time, out tivtt, out twalk, out twait, out _unused, out cost );
                    local += tivtt * TransitInVehicleTime + twalk * TransitWalk + twait * TransitWait + cost * travelCostFactor;
                    TransitNetwork.GetAllData( stationIndex, so, time, out tivtt, out twalk, out twait, out _unused, out cost );
                    local += tivtt * TransitInVehicleTime + twalk * TransitWalk + twait * TransitWait + cost * travelCostFactor;
                    local += AutoNetwork.TravelTime( fo, stationIndex, time ).ToMinutes() * AutoInVehicleTime;
                    local += AutoNetwork.TravelCost( fo, stationIndex, time ) * travelCostFactor;
                    local += AutoNetwork.TravelTime( stationIndex, sd, time ).ToMinutes() * AutoInVehicleTime;
                    local += AutoNetwork.TravelCost( stationIndex, sd, time ) * travelCostFactor;
                    totalUtil += local * probability;
                }
            }
            dependentUtility += totalUtil;
            return true;
        }

        private IZone SelectAccessStation(Random random, Pair<IZone[], float[]> accessData)
        {
            var rand = random.NextDouble();
            var utils = accessData.Second;
            var runningTotal = 0.0f;
            for ( int i = 0; i < utils.Length; i++ )
            {
                if ( !float.IsNaN( utils[i] ) )
                {
                    runningTotal += utils[i];
                    if ( runningTotal >= rand )
                    {
                        return accessData.First[i];
                    }
                }
            }
            // if we didn't find the right utility, just take the first one we can find
            for ( int i = 0; i < utils.Length; i++ )
            {
                if ( !float.IsNaN( utils[i] ) )
                {
                    return accessData.First[i];
                }
            }
            return null;
        }

        private int CountTripsUsingThisMode(int tripIndex, out bool first, out int otherIndex, List<ITrip> trips)
        {
            int tripCount = 0;
            otherIndex = -1;
            first = true;
            for ( int i = 0; i < trips.Count; i++ )
            {
                if ( trips[i].Mode == this )
                {
                    if ( i < tripIndex )
                    {
                        first = false;
                    }
                    if (tripIndex != i)
                    {
                        otherIndex = i;
                    }
                    tripCount++;
                }
            }
            return tripCount;
        }

        public void IterationStarting(int iterationNumber, int maxIterations)
        {
            AccessStationModel.Load();
            AgeUtilLookup = new float[16];
            for (int i = 0; i < AgeUtilLookup.Length; i++ )
            {
                AgeUtilLookup[i] = (float)Math.Log( i + 1, Math.E ) * LogOfAgeFactor;
            }
        }

        public void IterationEnding(int iterationNumber, int maxIterations)
        {
            AccessStationModel.Unload();
        }
    }
}
