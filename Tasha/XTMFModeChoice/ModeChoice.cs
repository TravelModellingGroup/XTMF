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
using XTMF;

namespace Tasha.XTMFModeChoice
{
    [ModuleInformation(Description = "This is the primary mode choice module for GTAModel V4.0.  It provides support for passenger and rideshare modes as well as tour-dependent utility modes.")]
    public class ModeChoice : ITashaModeChoice
    {
        [RunParameter("Rideshare's Base Mode Name", "Auto", "The name of the mode that will turn into rideshare, this is ignored if there is no rideshare mode selected.")]
        public string AutoModeName;

        [RunParameter("Household Iterations", 1, "The number of household iterations to complete.")]
        public int HouseholdIterations;

        [RunParameter("Passenger Mode Name", "", "The name of the passenger mode, leave blank to not process.")]
        public string PassengerModeName;

        [SubModelInformation(Description = "The modules used for processing in between household iterations.", Required = false)]
        public IPostHouseholdIteration[] PostHouseholdIteration;

        [RunParameter("Random Seed", 12345, "The random seed for this mode choice algorithm.")]
        public int RandomSeed;

        [RunParameter("Rideshare Mode Name", "", "The name of the rideshare mode, leave blank to not process.")]
        public string RideshareModeName;

        [RootModule]
        public ITashaRuntime Root;

        private ITashaMode AutoMode;
        private ITashaPassenger PassengerMode;
        private ISharedMode Rideshare;

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        private ITashaMode[] NonSharedModes;
        private ITashaMode[] AllModes;
        private IVehicleType[] VehicleTypes;

        public void LoadOneTimeLocalData()
        {
            AllModes = Root.AllModes.ToArray();
            NonSharedModes = Root.NonSharedModes.ToArray();
            VehicleTypes = Root.VehicleTypes.ToArray();
        }

        private float[] VarianceScale;

        public void IterationStarted(int currentIteration, int totalIterations)
        {
            foreach (var module in PostHouseholdIteration)
            {
                module.IterationStarting(currentIteration, totalIterations);
            }

            VarianceScale = new float[AllModes.Length];
            for (int i = 0; i < VarianceScale.Length; i++)
            {
                VarianceScale[i] = (float)AllModes[i].VarianceScale;
            }
        }

        [RunParameter("Max Trip Chain Size", 10, "The maximum trip chain size that will be processed.")]
        public int MaxTripChainSize;

        public bool Run(ITashaHousehold household)
        {
            if (MaxTripChainSize > 0)
            {
                if (AnyOverMaxTripChainSize(household))
                {
                    return false;
                }
            }
            Random random = new Random(RandomSeed + household.HouseholdId);
            ModeChoiceHouseholdData householdData = new ModeChoiceHouseholdData(household, AllModes.Length, VehicleTypes.Length + 1);
            HouseholdResourceAllocator householdResourceAllocator = new HouseholdResourceAllocator(household, AllModes);
            PassengerMatchingAlgorithm passengerMatchingAlgorithm = null;
            // attach this so analysis modules can look at it later
            household.Attach("ModeChoiceData", householdData);
            household.Attach("ResourceAllocator", householdResourceAllocator);
            if (PassengerMode != null)
            {
                passengerMatchingAlgorithm = new PassengerMatchingAlgorithm(household, householdData, PassengerMode, AllModes);
                household.Attach("PassengerMatchingAlgorithm", passengerMatchingAlgorithm);
            }

            for (int i = 0; i < PostHouseholdIteration.Length; i++)
            {
                PostHouseholdIteration[i].HouseholdStart(household, HouseholdIterations);
            }
            if (!Pass1(householdData, random))
            {
                for (int i = 0; i < PostHouseholdIteration.Length; i++)
                {
                    PostHouseholdIteration[i].HouseholdComplete(household, false);
                }
                return false;
            }
            for (int householdIteration = 0; householdIteration < HouseholdIterations; householdIteration++)
            {
                if (householdIteration > 0)
                {
                    RegenerateErrorTerms(householdData, random);
                }
                // Start of Pass 2
                AssignBestPerVehicle(VehicleTypes, householdData);
                var resolution = householdResourceAllocator.Resolve(householdData, VehicleTypes, householdIteration);
                if (resolution == null)
                {
                    for (int i = 0; i < PostHouseholdIteration.Length; i++)
                    {
                        PostHouseholdIteration[i].HouseholdComplete(household, false);
                    }
                    // failure
                    return false;
                }
                AssignModes(random, resolution, householdData);
                householdResourceAllocator.BuildVehicleAvailabilities(householdData, household.Vehicles);
                // Start of Pass 2.5 ( rideshare )
                ProcessRideshare(householdData);

                // Start of Pass 3 (Passenger attaching to trip chains)
                if (passengerMatchingAlgorithm != null)
                {
                    passengerMatchingAlgorithm.GeneratePotentialPassengerTrips(random);
                    passengerMatchingAlgorithm.ResolvePassengerTrips();
                    // Start of Pass 4 (Passenger attaching to new trips coming from home)
                    passengerMatchingAlgorithm.GeneratePotentialPassengerTrips(random, false, householdResourceAllocator);
                    passengerMatchingAlgorithm.ResolvePassengerTrips();
                }
                // Now at the end add to chosen modes (And assign joint trips)
                FinalAssignment(householdData, householdIteration);
                for (int i = 0; i < PostHouseholdIteration.Length; i++)
                {
                    PostHouseholdIteration[i].HouseholdIterationComplete(household, householdIteration, HouseholdIterations);
                }
            }
            for (int i = 0; i < PostHouseholdIteration.Length; i++)
            {
                PostHouseholdIteration[i].HouseholdComplete(household, true);
            }
            return true;
        }

        private bool AnyOverMaxTripChainSize(ITashaHousehold household)
        {
            ITashaPerson[] persons = household.Persons;
            for (int i = 0; i < persons.Length; i++)
            {
                for (int j = 0; j < persons[i].TripChains.Count; j++)
                {
                    if (persons[i].TripChains[j].Trips.Count > MaxTripChainSize)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public bool RuntimeValidation(ref string error)
        {
            if (!string.IsNullOrWhiteSpace(RideshareModeName))
            {
                bool found = false;
                foreach (var sharedMode in Root.SharedModes)
                {
                    if (sharedMode.ModeName == RideshareModeName)
                    {
                        Rideshare = sharedMode;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    error = "In '" + Name + "' we were unable to find a shared mode called '" + RideshareModeName + "' to use for rideshare";
                    return false;
                }
                found = false;
                foreach (var nonSharedMode in Root.NonSharedModes)
                {
                    if (nonSharedMode.ModeName == AutoModeName)
                    {
                        AutoMode = nonSharedMode;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    error = "In '" + Name + "' we were unable to find a non shared mode called '" + AutoModeName + "' to use replace with rideshare";
                    return false;
                }
            }
            if (!string.IsNullOrWhiteSpace(PassengerModeName))
            {
                foreach (var sharedMode in Root.SharedModes)
                {
                    if (sharedMode.ModeName == PassengerModeName)
                    {
                        PassengerMode = sharedMode as ITashaPassenger;
                        break;
                    }
                }
                if (PassengerMode == null)
                {
                    error = "In '" + Name + "' we were unable to find a shared mode called '" + PassengerModeName + "' to use for passenger";
                    return false;
                }
            }
            return true;
        }

        private void AssignBestPerVehicle(IVehicleType[] list, ModeChoiceHouseholdData householdData)
        {
            var modes = NonSharedModes;
            // Go through all of the possible assignments and get the best one per vehicle
            for (int i = 0; i < householdData.PersonData.Length; i++)
            {
                var person = householdData.PersonData[i];
                for (int j = 0; j < person.TripChainData.Length; j++)
                {
                    var tripChain = person.TripChainData[j];
                    ITripChain tashaTripChain = tripChain.TripChain;
                    if (!(tashaTripChain.JointTrip && !tashaTripChain.JointTripRep))
                    {
                        tripChain.SelectBestPerVehicleType(modes, list);
                    }
                }
            }
        }

        private void AssignModes(Random random, int[][] resolution, ModeChoiceHouseholdData householdData)
        {
            var modes = NonSharedModes;
            var numberOfPeople = resolution.Length;
            for (int i = 0; i < numberOfPeople; i++)
            {
                var tripChainData = householdData.PersonData[i].TripChainData;
                var numberOfTripChains = tripChainData.Length;
                for (int j = 0; j < numberOfTripChains; j++)
                {
                    if (!(tripChainData[j].TripChain.JointTrip && !tripChainData[j].TripChain.JointTripRep))
                    {
                        tripChainData[j].Assign(random, resolution[i][j], modes);
                    }
                }
            }
        }

        private void FinalAssignment(ModeChoiceHouseholdData householdData, int householdIteration)
        {
            ModeChoicePersonData[] personData = householdData.PersonData;
            for (int i = 0; i < personData.Length; i++)
            {
                var tripChainData = personData[i].TripChainData;
                for (int j = 0; j < tripChainData.Length; j++)
                {
                    tripChainData[j].FinalAssignment(householdIteration);
                }
            }
        }

        private bool Pass1(ModeChoiceHouseholdData householdData, Random random)
        {
            var nonSharedModes = NonSharedModes;

            for (int i = 0; i < householdData.PersonData.Length; i++)
            {
                var person = householdData.PersonData[i];
                for (int j = 0; j < person.TripChainData.Length; j++)
                {
                    var tripChain = person.TripChainData[j];
                    if (!(tripChain.TripChain.JointTrip && !tripChain.TripChain.JointTripRep))
                    {
                        if (!tripChain.Pass1(nonSharedModes))
                        {
                            return false;
                        }
                        // now we can compute the random terms
                        tripChain.GenerateRandomTerms(random, VarianceScale);
                    }
                }
            }
            return true;
        }

        private void ProcessRideshare(ModeChoiceHouseholdData householdData)
        {
            if (Rideshare == null)
            {
                return;
            }
            var numberOfPeople = householdData.PersonData.Length;
            var autoIndex = Array.IndexOf(AllModes, AutoMode);
            var rideshareIndex = Array.IndexOf(AllModes, Rideshare);
            for (int i = 0; i < numberOfPeople; i++)
            {
                var tripChainData = householdData.PersonData[i].TripChainData;
                var numberOfTripChains = tripChainData.Length;
                for (int j = 0; j < numberOfTripChains; j++)
                {
                    if (tripChainData[j].TripChain.JointTripRep)
                    {
                        var trips = tripChainData[j].TripChain.Trips;
                        var numberOfTrips = trips.Count;
                        for (int k = 0; k < numberOfTrips; k++)
                        {
                            if (trips[k].Mode == AutoMode)
                            {
                                trips[k].Mode = Rideshare;
                                householdData.PersonData[i].TripChainData[j].TripData[k].V[rideshareIndex]
                                    = householdData.PersonData[i].TripChainData[j].TripData[k].V[autoIndex];
                                householdData.PersonData[i].TripChainData[j].TripData[k].Error[rideshareIndex]
                                    = householdData.PersonData[i].TripChainData[j].TripData[k].Error[autoIndex];
                            }
                        }
                    }
                }
            }
        }

        private void RegenerateErrorTerms(ModeChoiceHouseholdData householdData, Random random)
        {
            for (int i = 0; i < householdData.PersonData.Length; i++)
            {
                var person = householdData.PersonData[i];
                for (int j = 0; j < person.TripChainData.Length; j++)
                {
                    var tripChain = person.TripChainData[j];
                    if (!(tripChain.TripChain.JointTrip && !tripChain.TripChain.JointTripRep))
                    {
                        // now we can compute the random terms
                        tripChain.GenerateRandomTerms(random, VarianceScale);
                    }
                }
            }
        }

        public void IterationFinished(int currentIteration, int totalIterations)
        {
            foreach (var module in PostHouseholdIteration)
            {
                module.IterationFinished(currentIteration, totalIterations);
            }
        }
    }
}