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

namespace Tasha.XTMFModeChoice
{
    public sealed class PassengerMatchingAlgorithm
    {
        public List<Conflict> Conflicts;
        public List<PotentialPassengerTrip> PotentialTrips;
        private ITashaHousehold Household;
        private ModeChoiceHouseholdData HouseholdData;
        private ITashaMode[] Modes;
        private ITashaPassenger PassengerMode;
        private readonly int IndexOfPassenger;

        public PassengerMatchingAlgorithm(ITashaHousehold household, ModeChoiceHouseholdData householdData, ITashaPassenger passengerMode, ITashaMode[] modes)
        {
            Household = household;
            HouseholdData = householdData;
            PassengerMode = passengerMode;
            Modes = modes;
            PotentialTrips = new List<PotentialPassengerTrip>();
            Conflicts = new List<Conflict>();
            IndexOfPassenger = IndexOf(passengerMode, modes);
        }

        public void GeneratePotentialPassengerTrips(Random random, bool pass3 = true, HouseholdResourceAllocator resourceAllocator = null)
        {
            // the first step is to clear out all of our potential trips from the last household iteration
            PotentialTrips.Clear();
            if(pass3)
            {
                Conflicts.Clear();
            }
            // for each person, for each trip chain
            for(int i = 0; i < HouseholdData.PersonData.Length; i++)
            {
                var personData = HouseholdData.PersonData[i];
                for(int j = 0; j < personData.TripChainData.Length; j++)
                {
                    // if it does not use a vehicle, then we can see if we can become a passenger
                    var tripChainData = personData.TripChainData[j];
                    if(tripChainData.TripChain.JointTrip && !tripChainData.TripChain.JointTripRep)
                    {
                        continue;
                    }
                    // make sure that it doesn't require a vehicle
                    if(tripChainData.TripChain.Trips[0].Mode.RequiresVehicle == null)
                    {
                        // if we are the representative for the trip (or no one is)
                        if(!tripChainData.TripChain.JointTrip || tripChainData.TripChain.JointTripRep)
                        {
                            if(pass3)
                            {
                                FindPotentialOnTourDriverForPassengerTrips(tripChainData, i, j, random);
                            }
                            else
                            {
                                FindPotentialAtHomeDriverForPassengerTrips(tripChainData, i, j, resourceAllocator, random);
                            }
                        }
                    }
                }
            }
        }

        public void ResolvePassengerTrips()
        {
            if(PotentialTrips.Count == 0)
            {
                return;
            }
            if(PotentialTrips.Count == 1)
            {
                AssignPassengerTrips(PotentialTrips);
            }
            else
            {
                HardCase();
            }
        }

        private void AssignPassengerTrips(List<PotentialPassengerTrip> assignList)
        {
            for(int i = 0; i < assignList.Count; i++)
            {
                var tripData = HouseholdData.PersonData[assignList[i].PassengerIndex].TripChainData[assignList[i].PassengerTripChainIndex].TripData[assignList[i].PassengerTripIndex];
                var otherModeIndex = IndexOf(assignList[i].PassengerDestinationTrip.Mode, Modes);
                // add the change of utility to the old data to compute our utility
                var otherU = tripData.V[otherModeIndex] + tripData.Error[otherModeIndex];
                tripData.V[IndexOfPassenger] = otherU + assignList[i].DeltaUtility;
                assignList[i].PassengerDestinationTrip.Mode = PassengerMode;
                assignList[i].PassengerDestinationTrip.Attach("Driver", assignList[i].DriverDestinationTrip);
            }
        }

        private void AssignPassengerTrips(PotentialPassengerTrip[][] feasible, List<ITrip> uniquePassenger, int[] bestAssignment)
        {
            for(int i = 0; i < feasible.Length; i++)
            {
                if(bestAssignment[i] >= 0)
                {
                    var assign = feasible[i][bestAssignment[i]];
                    var tripData = HouseholdData.PersonData[assign.PassengerIndex].TripChainData[assign.PassengerTripChainIndex].TripData[assign.PassengerTripIndex];
                    var otherModeIndex = IndexOf(assign.PassengerDestinationTrip.Mode, Modes);
                    // add the change of utility to the old data to compute our utility
                    tripData.V[IndexOfPassenger] = (tripData.V[otherModeIndex] + tripData.Error[otherModeIndex]) + assign.DeltaUtility;
                    assign.PassengerDestinationTrip.Mode = PassengerMode;
                    assign.PassengerDestinationTrip.Attach("Driver", assign.DriverDestinationTrip);
                }
            }
        }

        private void CheckForCarAtHome(Time StartTime, Time EndTime, HouseholdResourceAllocator resourceAllocator,
            ModeChoiceTripChainData PassengerTripChainData, int driverIndex, int passengerIndex, int passengerTripChainIndex, Random random,
            ITashaPerson driver)
        {
            int totalVehicles = Household.Vehicles.Length;
            if(totalVehicles == 0)
            {
                return;
            }
            int allDrivers = 0;
            PurePassengerTripChain DriverTripChain;
            for(int i = 0; i < Household.Persons.Length; i++)
            {
                if(Household.Persons[i].Licence)
                {
                    allDrivers++;
                }
            }

            if(totalVehicles >= allDrivers)
            {
                //then there is always a car at home (when a driver is home), so we can add a trip chain to the driver's trip.
                DriverTripChain = CreateDriverTripChain(StartTime, EndTime, Household.HomeZone, driver);
                CheckForPurePassengerTrips(DriverTripChain, PassengerTripChainData, driverIndex, passengerIndex, passengerTripChainIndex, random);
            }
            else
            {
                var TimeSlots = resourceAllocator.VehicleAvailability;
                for(int i = 0; i < TimeSlots.Count; i++)
                {
                    if(TimeSlots[i].AvailableCars > 0)
                    {
                        if(Time.Intersection(StartTime, EndTime, TimeSlots[i].TimeSpan.Start, TimeSlots[i].TimeSpan.End, out var intersectionStart, out var intersectionEnd))
                        {
                            DriverTripChain = CreateDriverTripChain(intersectionStart, intersectionEnd, Household.HomeZone, driver);
                            CheckForPurePassengerTrips(DriverTripChain, PassengerTripChainData, driverIndex, passengerIndex, passengerTripChainIndex, random);
                        }
                    }
                }
            }
        }

        private void CheckForPotentialPassengerTrips(ModeChoiceTripChainData driverTripChainData, ModeChoiceTripChainData passengerTripChainData,
            int driverIndex, int passengerIndex, int passengerTripChainIndex, Random random)
        {
            for(int j = 0; j < passengerTripChainData.TripData.Length; j++)
            {
                float passengerEpsilon = float.NegativeInfinity;
                for(int i = 0; i < driverTripChainData.TripData.Length; i++)
                {
                    float v;
                    if(driverTripChainData.TripChain.Trips[i].Mode != PassengerMode.AssociatedMode)
                    {
                        continue;
                    }
                    if(PassengerMode.CalculateV(driverTripChainData.TripChain.Trips[i], passengerTripChainData.TripChain.Trips[j], out v))
                    {
                        // only pop a random variable for the passenger when it is needed (performance)
                        if(passengerEpsilon <= float.NegativeInfinity)
                        {
                            passengerEpsilon = GenerateEpsilon(random);
                        }
                        // This option's U is the v of the tour plus the passenger's random and a new random for the driver
                        // make sure there is an improvement in the utility for the household
                        var deltaU = v + passengerEpsilon + GenerateEpsilon(random) - GetUtilityOfTrips(passengerTripChainData, j, driverTripChainData, i);
                        if(deltaU > 0)
                        {
                            PotentialTrips.Add(new PotentialPassengerTrip(driverTripChainData.TripChain.Trips[i],
                                passengerTripChainData.TripChain.Trips[j], deltaU, driverIndex, passengerIndex, passengerTripChainIndex, j));
                        }
                    }
                }
            }
        }

        private void CheckForPurePassengerTrips(PurePassengerTripChain DriverTripChain, ModeChoiceTripChainData PassengerTripChainData, int driverIndex, int passengerIndex, int passengerTripChainIndex, Random random)
        {
            for(int j = 0; j < PassengerTripChainData.TripData.Length; j++)
            {
                float passengerEpsilon = float.NegativeInfinity;
                float v;
                if(PassengerTripChainData.TripChain.Trips[j].Mode.RequiresVehicle != null)
                {
                    continue;
                }
                if(PassengerMode.CalculateV(DriverTripChain.Trips[0], PassengerTripChainData.TripChain.Trips[j], out v))
                {
                    if(passengerEpsilon <= float.NegativeInfinity)
                    {
                        passengerEpsilon = GenerateEpsilon(random);
                    }
                    var deltaU = v + passengerEpsilon + GenerateEpsilon(random) - GetUtilityOfTrips(PassengerTripChainData, j);
                    if(deltaU > 0)
                    {
                        PotentialTrips.Add(new PotentialPassengerTrip(DriverTripChain.Trips[0],
                            PassengerTripChainData.TripChain.Trips[j], deltaU, driverIndex, passengerIndex, passengerTripChainIndex, j));
                    }
                }
            }
        }

        private int[][] ConflictTable(PotentialPassengerTrip[][] feasible, int numberOfDrivers, out int numberOfConflicts)
        {
            var ret = new int[feasible.Length][];
            numberOfConflicts = 1;
            for(int i = 0; i < feasible.Length; i++)
            {
                ret[i] = new int[feasible[i].Length];
            }
            for(int driver = 0; driver < numberOfDrivers; driver++)
            {
                for(int j = 0; j < feasible.Length; j++)
                {
                    if(feasible[j][driver] == null)
                    {
                        continue;
                    }
                    for(int k = j + 1; k < feasible.Length; k++)
                    {
                        if(feasible[k][driver] == null)
                        {
                            continue;
                        }
                        if(Time.Intersection(feasible[j][driver].DriverDestinationTrip.TripStartTime,
                                feasible[j][driver].PassengerDestinationTrip.ActivityStartTime,
                                feasible[k][driver].DriverDestinationTrip.TripStartTime,
                                feasible[k][driver].PassengerDestinationTrip.ActivityStartTime))
                        {
                            if(ret[j][driver] == 0)
                            {
                                ret[j][driver] = numberOfConflicts;
                                ret[k][driver] = numberOfConflicts;
                                numberOfConflicts++;
                            }
                            else
                            {
                                ret[k][driver] = ret[j][driver];
                            }
                        }
                    }
                }
            }
            numberOfConflicts--;
            return ret;
        }

        private PurePassengerTripChain CreateDriverTripChain(Time Start, Time End, IZone HomeZone, ITashaPerson driver)
        {
            var mode = PassengerMode.AssociatedMode;
            var driverTrip = PurePassengerTrip.MakeDriverTrip(HomeZone, mode, Start, End);
            var driverTripChain = new PurePassengerTripChain();
            driverTrip.TripChain = driverTripChain;
            driverTripChain.Trips.Add(driverTrip);
            driverTripChain.Person = driver;
            return driverTripChain;
        }

        private bool Feasible(int[][] feasible, int[] currentAssignment, int highest)
        {
            int highestConflictIndex;
            if(currentAssignment[highest] == -1 || (highestConflictIndex = feasible[highest][currentAssignment[highest]]) == 0)
            {
                return true;
            }
            // if we do want a driver though, check to make sure no one else is using them at the same time
            for(int j = 0; j < highest; j++)
            {
                if(currentAssignment[j] >= 0)
                {
                    // check to see if they are using the same driver
                    if(highestConflictIndex == feasible[j][currentAssignment[j]])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private void FindPotentialAtHomeDriverForPassengerTrips(ModeChoiceTripChainData PassengerTripChainData, int passengerNumber, 
            int passengerTripChainIndex, HouseholdResourceAllocator resourceAllocator, Random random)
        {
            Time startTime;
            Time endTime;
            for(int i = 0; i < HouseholdData.PersonData.Length; i++)
            {
                if(passengerNumber == i)
                {
                    // you can't passenger yourself
                    continue;
                }
                if(!Household.Persons[i].Licence)
                {
                    //The person at home must be a driver
                    continue;
                }
                var personData = HouseholdData.PersonData[i];
                var numberOfTripChains = personData.TripChainData.Length;

                //Check if driver is home all day
                if(numberOfTripChains == 0)
                {
                    //check car availability and then assign trip chain
                    CheckForCarAtHome(Time.StartOfDay, Time.EndOfDay, resourceAllocator, PassengerTripChainData, i, passengerNumber, passengerTripChainIndex, random, Household.Persons[i]);
                }
                else
                {
                    for(int j = 0; j < numberOfTripChains; j++)
                    {
                        var driverTripChainData = personData.TripChainData[j];

                        if(driverTripChainData.TripChain.JointTrip && !driverTripChainData.TripChain.JointTripRep)
                        {
                            continue;
                        }

                        //Check if it is the last trip chain of the driver's day.
                        if(j >= numberOfTripChains - 1)
                        {
                            // check car availability and then assign
                            startTime = driverTripChainData.TripChain.Trips[driverTripChainData.TripChain.Trips.Count - 1].ActivityStartTime;
                            CheckForCarAtHome(startTime, Time.EndOfDay, resourceAllocator, PassengerTripChainData, i, passengerNumber, passengerTripChainIndex, random, Household.Persons[i]);
                        }
                        else
                        {
                            var CurrentTripChainTrips = personData.TripChainData[j].TripChain.Trips;
                            var NextTripChainTrips = personData.TripChainData[j + 1].TripChain.Trips;

                            //First trip chain of the day
                            if(j == 0)
                            {
                                // that means that you can have them transport a passenger before they leave home.
                                endTime = CurrentTripChainTrips[0].TripStartTime;
                                CheckForCarAtHome(Time.StartOfDay, endTime, resourceAllocator, PassengerTripChainData, i, passengerNumber, passengerTripChainIndex, random, Household.Persons[i]);
                            }

                            if(CurrentTripChainTrips[CurrentTripChainTrips.Count - 1].ActivityStartTime < NextTripChainTrips[0].TripStartTime)
                            {
                                //check car availability and then assign trip chain
                                startTime = CurrentTripChainTrips[CurrentTripChainTrips.Count - 1].ActivityStartTime;
                                endTime = NextTripChainTrips[0].TripStartTime;
                                CheckForCarAtHome(startTime, endTime, resourceAllocator, PassengerTripChainData, i, passengerNumber, passengerTripChainIndex, random, Household.Persons[i]);
                            }
                        }
                    }
                }
            }
        }

        private void FindPotentialOnTourDriverForPassengerTrips(ModeChoiceTripChainData tripChainData, int passengerNumber, int passengerTripChainIndex, Random random)
        {
            // for each person, for each trip chain
            for(int i = 0; i < HouseholdData.PersonData.Length; i++)
            {
                // you can't passenger yourself
                if(passengerNumber != i)
                {
                    var personData = HouseholdData.PersonData[i];
                    var driverTripChainDataList = personData.TripChainData;
                    for(int j = 0; j < driverTripChainDataList.Length; j++)
                    {
                        // if it does not use a vehicle, then we can see if we can become a passenger
                        var driverTripChainData = driverTripChainDataList[j];
                        // ignore if they are on a joint trip and are not the representative
                        if(driverTripChainData.TripChain.JointTrip && !driverTripChainData.TripChain.JointTripRep)
                        {
                            continue;
                        }
                        // make sure that it requires a vehicle
                        var mode = driverTripChainData.TripChain.Trips[0].Mode;
                        if(mode.RequiresVehicle != null)
                        {
                            // if we are the representative for the trip (or no one is)
                            if(!driverTripChainData.TripChain.JointTrip || driverTripChainData.TripChain.JointTripRep)
                            {
                                CheckForPotentialPassengerTrips(driverTripChainData, tripChainData, j, passengerNumber, passengerTripChainIndex, random);
                            }
                        }
                    }
                }
            }
        }

        private float GenerateEpsilon(Random rand)
        {
            double u1 = rand.NextDouble();
            double u2 = rand.NextDouble();
            double r = Math.Sqrt(-2.0 * Math.Log(u1));
            double theta = 2.0 * Math.PI * u2;
            return (float)((r * Math.Sin(theta)) * PassengerMode.VarianceScale);
        }

        private List<ITrip> GetUniquePassengerTrips()
        {
            var unique = new List<ITrip>();
            var numberOfPotentialTrips = PotentialTrips.Count;
            for(int i = 0; i < numberOfPotentialTrips; i++)
            {
                if(!unique.Contains(PotentialTrips[i].PassengerDestinationTrip))
                {
                    unique.Add(PotentialTrips[i].PassengerDestinationTrip);
                }
            }
            return unique;
        }

        private float GetUtilityOfTrips(ModeChoiceTripChainData passengerTripChainData, int passengerTrip, ModeChoiceTripChainData driverTripChainData = default(ModeChoiceTripChainData), int driverTrip = -1)
        {
            float total = 0;
            if(driverTrip != -1)
            {
                var indexOfDriverMode = IndexOf(driverTripChainData.TripChain.Trips[driverTrip].Mode, Modes);
                // add in the utility of the driver's trip
                total += driverTripChainData.TripData[driverTrip].V[indexOfDriverMode]
                + driverTripChainData.TripData[driverTrip].Error[indexOfDriverMode];
            }
            var indexOfPassengersPreviousMode = IndexOf(passengerTripChainData.TripChain.Trips[passengerTrip].Mode, Modes);
            // add in the utility of the passenger's trip
            total += passengerTripChainData.TripData[passengerTrip].V[indexOfPassengersPreviousMode]
                + passengerTripChainData.TripData[passengerTrip].Error[indexOfPassengersPreviousMode];
            return total;
        }

        private void HardCase()
        {
            List<ITrip> uniquePassenger = GetUniquePassengerTrips();
            PotentialPassengerTrip[][] feasible = new PotentialPassengerTrip[uniquePassenger.Count][];
            SetupTable(HouseholdData.PersonData.Length, feasible, uniquePassenger);
            //var watch = new Stopwatch();
            //watch.Start();
            int[] bestAssignment = new int[uniquePassenger.Count];
            for(int i = 0; i < bestAssignment.Length; i++)
            {
                bestAssignment[i] = -1;
            }
            Solve(feasible, bestAssignment, HouseholdData.PersonData.Length);
            AssignPassengerTrips(feasible, uniquePassenger, bestAssignment);
        }

        private static int IndexOf<T>(T iVehicleType, List<T> vehicleTypes) where T : class
        {
            for(int i = 0; i < vehicleTypes.Count; i++)
            {
                if(iVehicleType == vehicleTypes[i])
                {
                    return i;
                }
            }
            return -1;
        }

        private static int IndexOf<T>(T elementToMatch, T[] arrayData) where T : class
        {
            for(int i = 0; i < arrayData.Length; i++)
            {
                if(elementToMatch == arrayData[i])
                {
                    return i;
                }
            }
            return -1;
        }

        private void SetupTable(int numberOfPeoples, PotentialPassengerTrip[][] feasible, List<ITrip> uniqueTrips)
        {
            for(int i = 0; i < uniqueTrips.Count; i++)
            {
                feasible[i] = new PotentialPassengerTrip[numberOfPeoples];
            }
            for(int i = 0; i < uniqueTrips.Count; i++)
            {
                for(int j = 0; j < PotentialTrips.Count; j++)
                {
                    if(PotentialTrips[j].PassengerDestinationTrip == uniqueTrips[i])
                    {
                        feasible[i][IndexOf(PotentialTrips[j].DriverDestinationTrip.TripChain.Person, Household.Persons)] = PotentialTrips[j];
                    }
                }
            }
        }

        private void Solve(PotentialPassengerTrip[][] feasible, int[] bestAssignment, int numberOfDrivers)
        {
            int numberOfConflicts;
            var conflicts = ConflictTable(feasible, numberOfDrivers, out numberOfConflicts);
            int[] currentAssignment = new int[bestAssignment.Length];
            bool[] alreadySolved = SolveSimpleCases(feasible, currentAssignment, bestAssignment, conflicts);
            int index = currentAssignment.Length;
            float bestU = float.NegativeInfinity;
            float currentU = 0;
            // clear out the memory to start with
            for(int i = 0; i < currentAssignment.Length; i++)
            {
                currentAssignment[i] = -1;
            }
            while(index >= 0)
            {
                if(index >= currentAssignment.Length)
                {
                    if(bestU < currentU)
                    {
                        for(int i = 0; i < currentAssignment.Length; i++)
                        {
                            bestAssignment[i] = currentAssignment[i];
                        }
                        bestU = currentU;
                    }
                    index--;
                    while(index >= 0)
                    {
                        if(alreadySolved[index])
                        {
                            index--;
                            continue;
                        }
                        break;
                    }
                    continue;
                }
                bool notFound = true;
                for(int i = currentAssignment[index] + 1; i < feasible[index].Length; i++)
                {
                    if(feasible[index][i] != null)
                    {
                        currentAssignment[index] = i;
                        if(!Feasible(conflicts, currentAssignment, index))
                        {
                            continue;
                        }
                        currentU += feasible[index][i].DeltaUtility;
                        index++;
                        while(index < feasible.Length)
                        {
                            if(alreadySolved[index])
                            {
                                index++;
                                continue;
                            }
                            break;
                        }
                        notFound = false;
                        break;
                    }
                }
                if(notFound)
                {
                    currentU -= feasible[index][currentAssignment[index]].DeltaUtility;
                    currentAssignment[index--] = -1;
                    while(index >= 0)
                    {
                        if(alreadySolved[index])
                        {
                            index--;
                            continue;
                        }
                        break;
                    }
                    continue;
                }
            }
        }

        private bool[] SolveSimpleCases(PotentialPassengerTrip[][] feasible, int[] currentAssignment, int[] bestAssignment, int[][] conflicts)
        {
            bool[] solved = new bool[currentAssignment.Length];
            for(int i = 0; i < conflicts.Length; i++)
            {
                int maxIndex = -1;
                float bestUtility = float.NegativeInfinity;
                for(int j = 0; j < conflicts[i].Length; j++)
                {
                    if(feasible[i][j] != null)
                    {
                        var util = feasible[i][j].DeltaUtility;
                        if(util > bestUtility)
                        {
                            bestUtility = util;
                            maxIndex = j;
                        }
                    }
                }
                if(maxIndex >= 0)
                {
                    if(conflicts[i][maxIndex] == 0)
                    {
                        // then we can just pick the best
                        currentAssignment[i] = bestAssignment[i] = maxIndex;
                        solved[i] = true;
                    }
                }
            }
            return solved;
        }
    }

    public sealed class PotentialPassengerTrip(ITrip driverDestinationTrip, ITrip passengerDestinationTrip, float deltaUtility,
        internal int DriverIndex, internal int PassengerIndex, internal int PassengerTripChainIndex, internal int PassengerTripIndex)
    {
        public float DeltaUtility { get; private set; } = deltaUtility;

        public ITrip DriverDestinationTrip { get; private set; } = driverDestinationTrip;

        public ITrip PassengerDestinationTrip { get; private set; } = passengerDestinationTrip;
    }
}