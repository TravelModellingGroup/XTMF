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
using Datastructure;
using Tasha.Common;
using TMG;
using XTMF;
using TMG.Functions;
namespace Tasha.V4Modes
{
    [ModuleInformation(Description =
        @"This module is designed to implement the Drive access transit mode for GTAModel V4.0+.")]
    public class DriveAccessTransitLogsumOnly : ITourDependentMode, IIterationSensitive
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

        [RunParameter("LogsumFactor", 0.0f, "The factor to apply to the logsum of the access station choice model.")]
        public float LogsumCorrelation;

        [RunParameter("ToActivityDensityFactor", 0.0f, "The factor to apply to the destination of the activity's density.")]
        public float ToActivityDensityFactor;

        [RunParameter("ToHomeDensityFactor", 0.0f, "The factor to apply to the destination of the activity's density.")]
        public float ToHomeDensityFactor;


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
            var o = zoneArray.GetFlatIndex(trip.OriginalZone.ZoneNumber);
            var d = zoneArray.GetFlatIndex(trip.DestinationZone.ZoneNumber);
            float v;
            // if Intrazonal
            if (o == d)
            {
                return float.NaN;
            }
            // Apply personal factors
            var p = trip.TripChain.Person;
            float constant;
            GetPersonVariables(p, out constant);
            v = constant;
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
            if (!ZonalDensityForActivities.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the resource for Zonal Density For Activities was of the wrong type!";
                return false;
            }
            if (!ZonalDensityForHome.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the resource for Zonal Density For Home was of the wrong type!";
                return false;
            }
            return true;
        }

        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return Time.Zero;
        }

        public bool CalculateTourDependentUtility(ITripChain chain, int tripIndex, out float dependentUtility, out Action<ITripChain> onSelection)
        {
            bool first;
            var trips = chain.Trips;
            int otherIndex;
            int tripCount = CountTripsUsingThisMode(tripIndex, out first, out otherIndex, trips);

            if (tripCount > 2)
            {
                dependentUtility = float.NaN;
                onSelection = null;
                return false;
            }
            if (first)
            {
                var accessData = AccessStationModel.ProduceResult(chain);
                if (accessData == null || !BuildUtility(trips[tripIndex].OriginalZone,
                    accessData, trips[tripIndex].DestinationZone, trips[tripIndex].TripStartTime, out dependentUtility))
                {
                    onSelection = null;
                    dependentUtility = float.NegativeInfinity;
                    return false;
                }
                int householdIteration = 0;
                onSelection = (tripChain) =>
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
                onSelection = null;
            }
            return true;
        }

        private bool BuildUtility(IZone firstOrigin, Pair<IZone[], float[]> accessData, IZone firstDestination, Time firstStartTime, out float dependentUtility)
        {
            var utils = accessData.Second;
            var totalUtil = VectorHelper.Sum(utils, 0, utils.Length);
            if (totalUtil <= 0)
            {
                dependentUtility = float.NaN;
                return false;
            }
            dependentUtility = LogsumCorrelation * (float)Math.Log(totalUtil) + GetPlanningDistrictConstant(firstStartTime, firstOrigin.PlanningDistrict, firstDestination.PlanningDistrict);
            VectorHelper.Multiply(utils, 0, utils, 0, 1.0f / totalUtil, utils.Length);
            return true;
        }

        private IZone SelectAccessStation(Random random, Pair<IZone[], float[]> accessData)
        {
            var rand = random.NextDouble();
            var utils = accessData.Second;
            var runningTotal = 0.0f;
            for (int i = 0; i < utils.Length; i++)
            {
                if (!float.IsNaN(utils[i]))
                {
                    runningTotal += utils[i];
                    if (runningTotal >= rand)
                    {
                        return accessData.First[i];
                    }
                }
            }
            // if we didn't find the right utility, just take the first one we can find
            for (int i = 0; i < utils.Length; i++)
            {
                if (!float.IsNaN(utils[i]))
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
            if (!AccessStationChoiceLoaded | UnloadAccessStationModelEachIteration)
            {
                AccessStationModel.Load();
                AccessStationChoiceLoaded = true;
            }
            // We do this here instead of the RuntimeValidation so that we don't run into issues with estimation
            AgeUtilLookup = new float[16];
            for (int i = 0; i < AgeUtilLookup.Length; i++)
            {
                AgeUtilLookup[i] = (float)Math.Log(i + 1, Math.E) * LogOfAgeFactor;
            }
            //build the region constants
            for (int i = 0; i < TimePeriodConstants.Length; i++)
            {
                TimePeriodConstants[i].BuildMatrix();
            }
            ZonalDensityForActivitiesArray = (float[]) ZonalDensityForActivities.AcquireResource<SparseArray<float>>().GetFlatData().Clone();
            ZonalDensityForHomeArray = (float[]) ZonalDensityForHome.AcquireResource<SparseArray<float>>().GetFlatData().Clone();
            for (int i = 0; i < ZonalDensityForActivitiesArray.Length; i++)
            {
                ZonalDensityForActivitiesArray[i] *= ToActivityDensityFactor;
                ZonalDensityForHomeArray[i] *= ToHomeDensityFactor;
            }
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
