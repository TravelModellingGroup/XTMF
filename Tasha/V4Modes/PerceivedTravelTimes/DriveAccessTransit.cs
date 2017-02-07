/*
    Copyright 2014-2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Runtime.CompilerServices;
using Datastructure;
using Tasha.Common;
using TMG.Functions;
using TMG;
using XTMF;
using System.Linq;

namespace Tasha.V4Modes.PerceivedTravelTimes
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
        [RunParameter("ToActivityDensityFactor", 0.0f, "The factor to apply to the destination of the activity's density.")]
        public float ToActivityDensityFactor;
        [RunParameter("ToHomeDensityFactor", 0.0f, "The factor to apply to the destination of the activity's density.")]
        public float ToHomeDensityFactor;

        private float ProfessionalCost;
        private float GeneralCost;
        private float SalesCost;
        private float ManufacturingCost;
        private float StudentCost;
        private float NonWorkerStudentCost;

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


        [SubModelInformation(Description = "Constants for time of day")]
        public TimePeriodSpatialConstant[] TimePeriodConstants;

        [SubModelInformation(Required = true, Description = "The density of zones for activities")]
        public IResource ZonalDensityForActivities;

        [SubModelInformation(Required = true, Description = "The density of zones for home")]
        public IResource ZonalDensityForHome;

        private float[] AgeUtilLookup;
        private float[] ZonalDensityForActivitiesArray;
        private float[] ZonalDensityForHomeArray;

        public double CalculateV(ITrip trip)
        {
            // compute the non human factors
            var zoneSystem = Root.ZoneSystem;
            var zoneArray = zoneSystem.ZoneArray;
            var o = zoneArray.GetFlatIndex(trip.OriginalZone.ZoneNumber);
            var d = zoneArray.GetFlatIndex(trip.DestinationZone.ZoneNumber);

            // if Intrazonal
            if (o == d)
            {
                return float.NaN;
            }
            // Apply personal factors
            var p = trip.TripChain.Person;
            float constant;
            GetPersonVariables(p, out constant);
            float v = constant;
            if (p.Female)
            {
                v += FemaleFlag;
            }
            var age = p.Age;
            v += AgeUtilLookup[Math.Min(Math.Max(age - 15, 0), 15)];
            if (age >= 65)
            {
                v += Over65;
            }
            else if (age >= 55)
            {
                v += Over55;
            }
            //Apply trip purpose factors
            switch (trip.Purpose)
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
            return v;
        }

        private void GetPersonVariables(ITashaPerson person, out float constant)
        {
            var empStat = person.EmploymentStatus;
            var stuStat = person.StudentStatus;
            if (empStat == TTSEmploymentStatus.FullTime)
            {
                switch (person.Occupation)
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
            switch (person.StudentStatus)
            {
                case StudentStatus.FullTime:
                case StudentStatus.PartTime:
                    constant = StudentConstant;
                    return;
            }
            if (empStat == TTSEmploymentStatus.PartTime)
            {
                switch (person.Occupation)
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
            constant = NonWorkerStudentConstant;
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
            if (trip.OriginalZone.PlanningDistrict == trip.DestinationZone.PlanningDistrict) return false;
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
            foreach (var network in Root.NetworkData)
            {
                if (network.NetworkType == AutoNetworkName)
                {
                    AutoNetwork = network;
                }
                else if (network.NetworkType == TransitNetworkName)
                {
                    TransitNetwork = network as ITripComponentData;
                }
            }
            if (AutoNetwork == null)
            {
                error = "In '" + Name + "' we were unable to find an auto network called '" + AutoNetworkName + "'";
                return false;
            }
            if (TransitNetwork == null)
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
            int tripCount = CountTripsUsingThisMode(tripIndex, out first, out otherIndex, trips);

            if (tripCount > 2)
            {
                dependentUtility = float.NaN;
                OnSelection = null;
                return false;
            }
            if (first)
            {
                var accessData = AccessStationModel.ProduceResult(chain);
                if (accessData == null || !BuildUtility(trips[tripIndex].OriginalZone, trips[otherIndex].OriginalZone,
                    accessData,
                    trips[tripIndex].DestinationZone, trips[otherIndex].DestinationZone, chain.Person, trips[tripIndex].ActivityStartTime, trips[otherIndex].ActivityStartTime,
                    out dependentUtility))
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
                    tripChain.Attach("AccessStation", SelectAccessStation(
                            new Random(household.HouseholdId * person.Id * person.TripChains.IndexOf(tripChain) * RandomSeed * householdIteration),
                            accessData));
                };
            }
            else
            {
                dependentUtility = 0.0f;
                OnSelection = null;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float GetPlanningDistrictConstant(Time startTime, int pdO, int pdD)
        {
            for (int i = 0; i < TimePeriodConstants.Length; i++)
            {
                if (startTime >= TimePeriodConstants[i].StartTime && startTime < TimePeriodConstants[i].EndTime)
                {
                    var value = TimePeriodConstants[i].GetConstant(pdO, pdD);
                    return value;
                }
            }
            return 0f;
        }

        private int[] StationIndexLookup;

        private bool BuildUtility(IZone firstOrigin, IZone secondOrigin, Pair<IZone[], float[]> accessData, IZone firstDestination, IZone secondDestination,
            ITashaPerson person, Time firstTime, Time secondTime, out float dependentUtility)
        {
            var zones = accessData.First;
            var utils = accessData.Second;
            var totalUtil = 0.0f;
            float ivttBeta, costBeta, constant;
            GetPersonVariables(person, out constant, out ivttBeta, out costBeta);
            totalUtil = VectorHelper.Sum(utils, 0, utils.Length);
            if (totalUtil <= 0)
            {
                dependentUtility = float.NaN;
                return false;
            }
            dependentUtility = GetPlanningDistrictConstant(firstTime, firstOrigin.PlanningDistrict, firstDestination.PlanningDistrict)
                + GetPlanningDistrictConstant(secondTime, secondOrigin.PlanningDistrict, secondDestination.PlanningDistrict);
            totalUtil = 1 / totalUtil;
            // we still need to do this in order to reduce time for computing the selected access station
            VectorHelper.Multiply(utils, 0, utils, 0, totalUtil, utils.Length);
            var zoneSystem = Root.ZoneSystem.ZoneArray;
            var fo = zoneSystem.GetFlatIndex(firstOrigin.ZoneNumber);
            var so = zoneSystem.GetFlatIndex(secondOrigin.ZoneNumber);
            var fd = zoneSystem.GetFlatIndex(firstDestination.ZoneNumber);
            var sd = zoneSystem.GetFlatIndex(secondDestination.ZoneNumber);
            totalUtil = 0;
            var fastTransit = TransitNetwork as ITripComponentCompleteData;
            var fastAuto = AutoNetwork as INetworkCompleteData;
            var stationIndexLookup = StationIndexLookup;
            if (stationIndexLookup == null)
            {
                stationIndexLookup = CreateStationIndexLookup(zoneSystem, zones);
            }
            if (fastTransit == null | fastAuto == null)
            {

                for (int i = 0; i < utils.Length; i++)
                {
                    var stationIndex = StationIndexLookup[i];
                    var probability = utils[i];
                    if (probability > 0)
                    {
                        var local = 0.0f;
                        float perceivedTime, cost, twalk, twait, trueTime;
                        TransitNetwork.GetAllData(stationIndex, fd, firstTime, out trueTime, out twalk, out twait, out perceivedTime, out cost);
                        local += perceivedTime * ivttBeta + cost * costBeta;
                        TransitNetwork.GetAllData(stationIndex, so, secondTime, out perceivedTime, out twalk, out twait, out perceivedTime, out cost);
                        local += perceivedTime * ivttBeta + cost * costBeta;
                        AutoNetwork.GetAllData(fo, stationIndex, firstTime, out perceivedTime, out cost);
                        local += perceivedTime * ivttBeta + costBeta * cost;
                        AutoNetwork.GetAllData(stationIndex, sd, secondTime, out perceivedTime, out cost);
                        local += perceivedTime * ivttBeta + costBeta * cost;
                        totalUtil += local * probability;
                    }
                }
            }
            else
            {
                int numberOfZones = zoneSystem.GetFlatData().Length;
                // fo, and so are constant across stations, so we can pull that part of the computation out
                fo = fo * numberOfZones;
                so = so * numberOfZones;
                float[] firstAutoMatrix = fastAuto.GetTimePeriodData(firstTime);
                float[] firstTransitMatrix = fastTransit.GetTimePeriodData(firstTime);
                float[] secondAutoMatrix = fastAuto.GetTimePeriodData(secondTime);
                float[] secondTransitMatrix = fastTransit.GetTimePeriodData(secondTime);
                if (firstTransitMatrix == null || secondTransitMatrix == null)
                {
                    dependentUtility = float.NaN;
                    return false;
                }
                for (int i = 0; i < utils.Length; i++)
                {
                    var stationIndex = stationIndexLookup[i];
                    int origin1ToStation = (fo + stationIndex) << 1;
                    int stationToDestination1 = ((stationIndex * numberOfZones) + fd) * 5;
                    int origin2ToStation = (so + stationIndex) * 5;
                    int stationToDestination2 = ((stationIndex * numberOfZones) + sd) << 1;
                    if (utils[i] > 0)
                    {
                        // transit utility
                        var tivtt = firstTransitMatrix[stationToDestination1] + secondTransitMatrix[origin2ToStation];
                        var tcost = firstTransitMatrix[stationToDestination1 + 3] + secondTransitMatrix[origin2ToStation + 3];
                        var aivtt = firstAutoMatrix[origin1ToStation] + secondAutoMatrix[stationToDestination2];
                        var acost = firstAutoMatrix[origin1ToStation + 1] + secondAutoMatrix[stationToDestination2 + 1];
                        var utility = (tivtt + aivtt) * ivttBeta + (acost + tcost) * costBeta;
                        totalUtil += utility * utils[i];
                    }
                }
            }
            dependentUtility += totalUtil;
            return true;
        }

        private int[] CreateStationIndexLookup(SparseArray<IZone> zoneSystem, IZone[] zones)
        {
            var lookup = zones.Select(z => zoneSystem.GetFlatIndex(z.ZoneNumber)).ToArray();
            StationIndexLookup = lookup;
            return lookup;
        }

        private void GetPersonVariables(ITashaPerson person, out float constant, out float time, out float cost)
        {
            if (person.EmploymentStatus == TTSEmploymentStatus.FullTime)
            {
                switch (person.Occupation)
                {
                    case Occupation.Professional:
                        cost = ProfessionalCost;
                        constant = ProfessionalConstant;
                        time = ProfessionalTimeFactor;
                        return;
                    case Occupation.Office:
                        cost = GeneralCost;
                        constant = GeneralConstant;
                        time = GeneralTimeFactor;
                        return;
                    case Occupation.Retail:
                        cost = SalesCost;
                        constant = SalesConstant;
                        time = SalesTimeFactor;
                        return;
                    case Occupation.Manufacturing:
                        cost = ManufacturingCost;
                        constant = ManufacturingConstant;
                        time = ManufacturingTimeFactor;
                        return;
                }
            }
            switch (person.StudentStatus)
            {
                case StudentStatus.FullTime:
                case StudentStatus.PartTime:
                    cost = StudentCost;
                    constant = StudentConstant;
                    time = StudentTimeFactor;
                    return;
            }
            if (person.EmploymentStatus == TTSEmploymentStatus.PartTime)
            {
                switch (person.Occupation)
                {
                    case Occupation.Professional:
                        cost = ProfessionalCost;
                        constant = ProfessionalConstant;
                        time = ProfessionalTimeFactor;
                        return;
                    case Occupation.Office:
                        cost = GeneralCost;
                        constant = GeneralConstant;
                        time = GeneralTimeFactor;
                        return;
                    case Occupation.Retail:
                        cost = SalesCost;
                        constant = SalesConstant;
                        time = SalesTimeFactor;
                        return;
                    case Occupation.Manufacturing:
                        cost = ManufacturingCost;
                        constant = ManufacturingConstant;
                        time = ManufacturingTimeFactor;
                        return;
                }
            }
            cost = NonWorkerStudentCost;
            constant = NonWorkerStudentConstant;
            time = NonWorkerStudentTimeFactor;
        }

        private IZone SelectAccessStation(Random random, Pair<IZone[], float[]> accessData)
        {
            var rand = (float)random.NextDouble();
            var utils = accessData.Second;
            var runningTotal = 0.0f;
            for (int i = 0; i < utils.Length; i++)
            {
                runningTotal += utils[i];
                if (runningTotal >= rand)
                {
                    return accessData.First[i];
                }
            }
            // if we didn't find the right utility, just take the first one we can find
            for (int i = 0; i < utils.Length; i++)
            {
                if (utils[i] > 0)
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
            for (int i = 0; i < trips.Count; i++)
            {
                if (trips[i].Mode == this)
                {
                    if (i < tripIndex)
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
            if (iterationNumber == 0)
            {
                StationIndexLookup = null;
            }
            if (!AccessStationChoiceLoaded | UnloadAccessStationModelEachIteration)
            {
                AccessStationModel.Load();
                AccessStationChoiceLoaded = true;
            }
            // We do this here instead of the RuntimeValidation so that we don't run into issues with estimation
            if (AgeUtilLookup == null)
            {
                AgeUtilLookup = new float[16];
            }
            for (int i = 0; i < AgeUtilLookup.Length; i++)
            {
                AgeUtilLookup[i] = (float)Math.Log(i + 1, Math.E) * LogOfAgeFactor;
            }
            //build the region constants
            for (int i = 0; i < TimePeriodConstants.Length; i++)
            {
                TimePeriodConstants[i].BuildMatrix();
            }


            ProfessionalCost = ConvertCostFactor(ProfessionalCostFactor, ProfessionalTimeFactor);
            GeneralCost = ConvertCostFactor(GeneralCostFactor, GeneralTimeFactor);
            SalesCost = ConvertCostFactor(SalesCostFactor, SalesTimeFactor);
            ManufacturingCost = ConvertCostFactor(ManufacturingCostFactor, ManufacturingTimeFactor);
            StudentCost = ConvertCostFactor(StudentCostFactor, StudentTimeFactor);
            NonWorkerStudentCost = ConvertCostFactor(NonWorkerStudentCostFactor, NonWorkerStudentTimeFactor);


            ZonalDensityForActivitiesArray = ZonalDensityForActivities.AcquireResource<SparseArray<float>>().GetFlatData().Clone() as float[];
            ZonalDensityForHomeArray = ZonalDensityForHome.AcquireResource<SparseArray<float>>().GetFlatData().Clone() as float[];
            for (int i = 0; i < ZonalDensityForActivitiesArray.Length; i++)
            {
                ZonalDensityForActivitiesArray[i] *= ToActivityDensityFactor;
                ZonalDensityForHomeArray[i] *= ToHomeDensityFactor;
            }
        }

        private float ConvertCostFactor(float costFactor, float timeFactor)
        {
            var ret = costFactor * timeFactor;
            if (ret > 0)
            {
                throw new XTMFRuntimeException("In '" + Name + "' we ended up with a beta to apply to cost that was greater than 0!");
            }
            return ret;
        }

        [RunParameter("Unload Access Station Per Iteration", true, "Should we unload the access station choice model or keep it between iterations?")]
        public bool UnloadAccessStationModelEachIteration;

        public bool AccessStationChoiceLoaded;

        public void IterationEnding(int iterationNumber, int maxIterations)
        {
            if (UnloadAccessStationModelEachIteration)
            {
                AccessStationModel.Unload();
            }
            ZonalDensityForActivities.ReleaseResource();
            ZonalDensityForHome.ReleaseResource();
        }
    }
}
