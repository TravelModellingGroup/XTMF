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
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Tasha.Common;
using TMG;
using TMG.Functions;
using XTMF;

namespace Tasha.XTMFModeChoice;

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
        PotentialTrips = [];
        Conflicts = [];
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

    private void AssignPassengerTrips(PotentialPassengerTrip?[][] feasible, int[] bestAssignment)
    {
        for(int i = 0; i < feasible.Length; i++)
        {
            var best = Get(bestAssignment, i);
            if (best >= 0)
            {
                var assign = Get(feasible, i, best).Value;
                var tripData = HouseholdData.PersonData[assign.PassengerIndex].TripChainData[assign.PassengerTripChainIndex].TripData[assign.PassengerTripIndex];
                var otherModeIndex = IndexOf(assign.PassengerDestinationTrip.Mode, Modes);
                // add the change of utility to the old data to compute our utility
                tripData.V[IndexOfPassenger] = (tripData.V[otherModeIndex] + tripData.Error[otherModeIndex]) + assign.DeltaUtility;
                assign.PassengerDestinationTrip.Mode = PassengerMode;
                assign.PassengerDestinationTrip.Attach("Driver", assign.DriverDestinationTrip);
            }
        }
    }

    private void CheckForCarAtHome(Time startTime, Time endTime, HouseholdResourceAllocator resourceAllocator,
        ModeChoiceTripChainData passengerTripChainData, int driverIndex, int passengerIndex, int passengerTripChainIndex, Random random,
        ITashaPerson driver)
    {
        int totalVehicles = Household.Vehicles.Length;
        if(totalVehicles == 0)
        {
            return;
        }
        int allDrivers = 0;
        PurePassengerTripChain driverTripChain;
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
            driverTripChain = CreateDriverTripChain(startTime, endTime, Household.HomeZone, driver);
            CheckForPurePassengerTrips(driverTripChain, passengerTripChainData, driverIndex, passengerIndex, passengerTripChainIndex, random);
        }
        else
        {
            var timeSlots = resourceAllocator.VehicleAvailability;
            for(int i = 0; i < timeSlots.Count; i++)
            {
                if(timeSlots[i].AvailableCars > 0)
                {
                    if (Time.Intersection(startTime, endTime, timeSlots[i].TimeSpan.Start, timeSlots[i].TimeSpan.End, out Time intersectionStart, out Time intersectionEnd))
                    {
                        driverTripChain = CreateDriverTripChain(intersectionStart, intersectionEnd, Household.HomeZone, driver);
                        CheckForPurePassengerTrips(driverTripChain, passengerTripChainData, driverIndex, passengerIndex, passengerTripChainIndex, random);
                    }
                }
            }
        }
    }

    private void CheckForPotentialPassengerTrips(ModeChoiceTripChainData driverTripChainData, ModeChoiceTripChainData passengerTripChainData,
        int driverIndex, int passengerIndex, int passengerTripChainIndex, Random random)
    {
        var driverTripChainTrips = driverTripChainData.TripChain.Trips;
        for(int j = 0; j < passengerTripChainData.TripData.Length; j++)
        {
            float passengerEpsilon = float.NegativeInfinity;
            for(int i = 0; i < driverTripChainData.TripData.Length; i++)
            {
                ITrip passengerTrip;
                if (driverTripChainTrips[i].Mode == PassengerMode.AssociatedMode &&
                    PassengerMode.CalculateV(driverTripChainTrips[i], (passengerTrip = passengerTripChainData.TripChain.Trips[j]), out float v))
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
                        PotentialTrips.Add(new PotentialPassengerTrip(driverTripChainTrips[i],
                            passengerTrip, deltaU, driverIndex, passengerIndex, passengerTripChainIndex, j));
                    }
                }
            }
        }
    }

    private void CheckForPurePassengerTrips(PurePassengerTripChain driverTripChain, ModeChoiceTripChainData passengerTripChainData, int driverIndex, int passengerIndex, int passengerTripChainIndex, Random random)
    {
        for(int j = 0; j < passengerTripChainData.TripData.Length; j++)
        {
            float passengerEpsilon = float.NegativeInfinity;
            if (passengerTripChainData.TripChain.Trips[j].Mode.RequiresVehicle != null)
            {
                continue;
            }
            if (PassengerMode.CalculateV(driverTripChain.Trips[0], passengerTripChainData.TripChain.Trips[j], out float v))
            {
                if(passengerEpsilon <= float.NegativeInfinity)
                {
                    passengerEpsilon = GenerateEpsilon(random);
                }
                var deltaU = v + passengerEpsilon + GenerateEpsilon(random) - GetUtilityOfTrips(passengerTripChainData, j);
                if(deltaU > 0)
                {
                    PotentialTrips.Add(new PotentialPassengerTrip(driverTripChain.Trips[0],
                        passengerTripChainData.TripChain.Trips[j], deltaU, driverIndex, passengerIndex, passengerTripChainIndex, j));
                }
            }
        }
    }

    private int[][] ConflictTable(PotentialPassengerTrip?[][] feasible, int numberOfDrivers, out int numberOfConflicts)
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
                var jDriverRecord = Get(feasible, j, driver);
                if (!jDriverRecord.HasValue)
                {
                    continue;
                }
                var jDriver = jDriverRecord.Value;
                for (int k = j + 1; k < feasible.Length; k++)
                {
                    var kDriverRecord = Get(feasible, k, driver);
                    if (!kDriverRecord.HasValue)
                    {
                        continue;
                    }                   
                    var kDriver = kDriverRecord.Value;
                    if (Time.Intersection(jDriver.DriverDestinationTrip.TripStartTime,
                            jDriver.PassengerDestinationTrip.ActivityStartTime,
                            kDriver.DriverDestinationTrip.TripStartTime,
                            kDriver.PassengerDestinationTrip.ActivityStartTime))
                    {
                        if(ret[j][driver] == 0)
                        {
                            Get(ret, j, driver) = numberOfConflicts;
                            Get(ret, k, driver) = numberOfConflicts;
                            numberOfConflicts++;
                        }
                        else
                        {
                            Get(ret, k, driver) = Get(ret, j, driver);
                        }
                    }
                }
            }
        }
        numberOfConflicts--;
        return ret;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static ref T Get<T>(T[][] matrix, int row, int column)
    {
        // This is a helper method to get a value from a matrix without bounds checking.
        ref var arrayRow = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(matrix), row);
        ref var cell = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(arrayRow), column);
        return ref cell;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static ref T Get<T>(T[] vector, int column)
    {
        // This is a helper method to get a value from a matrix without bounds checking.
        ref var cell = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(vector), column);
        return ref cell;
    }

    private PurePassengerTripChain CreateDriverTripChain(Time start, Time end, IZone homeZone, ITashaPerson driver)
    {
        var mode = PassengerMode.AssociatedMode;
        var driverTrip = PurePassengerTrip.MakeDriverTrip(homeZone, mode, start, end);
        var driverTripChain = new PurePassengerTripChain();
        driverTrip.TripChain = driverTripChain;
        driverTripChain.Trips.Add(driverTrip);
        driverTripChain.Person = driver;
        return driverTripChain;
    }

    private bool Feasible(int[][] conflicts, int[] currentAssignment, int index)
    {
        int highestConflictIndex;
        if(currentAssignment[index] == -1 || (highestConflictIndex = Get(conflicts, index, Get(currentAssignment, index))) == 0)
        {
            return true;
        }
        // if we do want a driver though, check to make sure no one else is using them at the same time
        for(int j = index - 1; j >= 0 ; j--)
        {
            if(currentAssignment[j] >= 0)
            {
                // check to see if they are using the same driver
                if(highestConflictIndex == Get(conflicts, j, Get(currentAssignment, j)))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private void FindPotentialAtHomeDriverForPassengerTrips(ModeChoiceTripChainData passengerTripChainData, int passengerNumber, 
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
                CheckForCarAtHome(Time.StartOfDay, Time.EndOfDay, resourceAllocator, passengerTripChainData, i, passengerNumber, passengerTripChainIndex, random, Household.Persons[i]);
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
                        CheckForCarAtHome(startTime, Time.EndOfDay, resourceAllocator, passengerTripChainData, i, passengerNumber, passengerTripChainIndex, random, Household.Persons[i]);
                    }
                    else
                    {
                        var currentTripChainTrips = personData.TripChainData[j].TripChain.Trips;
                        var nextTripChainTrips = personData.TripChainData[j + 1].TripChain.Trips;

                        //First trip chain of the day
                        if(j == 0)
                        {
                            // that means that you can have them transport a passenger before they leave home.
                            endTime = currentTripChainTrips[0].TripStartTime;
                            CheckForCarAtHome(Time.StartOfDay, endTime, resourceAllocator, passengerTripChainData, i, passengerNumber, passengerTripChainIndex, random, Household.Persons[i]);
                        }

                        if(currentTripChainTrips[currentTripChainTrips.Count - 1].ActivityStartTime < nextTripChainTrips[0].TripStartTime)
                        {
                            //check car availability and then assign trip chain
                            startTime = currentTripChainTrips[currentTripChainTrips.Count - 1].ActivityStartTime;
                            endTime = nextTripChainTrips[0].TripStartTime;
                            CheckForCarAtHome(startTime, endTime, resourceAllocator, passengerTripChainData, i, passengerNumber, passengerTripChainIndex, random, Household.Persons[i]);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float GenerateEpsilon(Random rand)
    {
        return (float)(RandomNumberHelper.SampleNormalDistribution(rand) * PassengerMode.VarianceScale);
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
        if(driverTrip != -1 && driverTripChainData != null)
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
        PotentialPassengerTrip?[][] feasible = new PotentialPassengerTrip?[uniquePassenger.Count][];
        SetupTable(HouseholdData.PersonData.Length, feasible, uniquePassenger);
        //var watch = new Stopwatch();
        //watch.Start();
        int[] bestAssignment = new int[uniquePassenger.Count];
        for(int i = 0; i < bestAssignment.Length; i++)
        {
            bestAssignment[i] = -1;
        }
        Solve(feasible, bestAssignment, HouseholdData.PersonData.Length);
        AssignPassengerTrips(feasible, bestAssignment);
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

    private void SetupTable(int numberOfPeoples, PotentialPassengerTrip?[][] feasible, List<ITrip> uniqueTrips)
    {
        for(int i = 0; i < uniqueTrips.Count; i++)
        {
            feasible[i] = new PotentialPassengerTrip?[numberOfPeoples];
        }
        for(int i = 0; i < uniqueTrips.Count; i++)
        {
            for(int j = 0; j < PotentialTrips.Count; j++)
            {
                if(PotentialTrips[j].PassengerDestinationTrip == uniqueTrips[i])
                {
                    Get(feasible, i, IndexOf(PotentialTrips[j].DriverDestinationTrip.TripChain.Person, Household.Persons)) = PotentialTrips[j];
                }
            }
        }
    }

    private void Solve(PotentialPassengerTrip?[][] feasible, int[] bestAssignment, int numberOfDrivers)
    {
        var conflicts = ConflictTable(feasible, numberOfDrivers, out int numberOfConflicts);
        int[] currentAssignment = new int[bestAssignment.Length];
        // clear out the memory to start with
        for(int i = 0; i < currentAssignment.Length; i++)
        {
            currentAssignment[i] = -1;
        }
        bool[] alreadySolved = SolveSimpleCases(feasible, currentAssignment, bestAssignment, conflicts);
        int index = currentAssignment.Length;
        float bestU = float.NegativeInfinity;
        float currentU = 0;
        while(index >= 0)
        {
            if(index >= currentAssignment.Length)
            {
                if(bestU < currentU)
                {
                    for(int i = 0; i < currentAssignment.Length; i++)
                    {
                        Get(bestAssignment, i) = currentAssignment[i];
                    }
                    bestU = currentU;
                }
                index--;
                while(index >= 0)
                {
                    if(Get(alreadySolved, index))
                    {
                        index--;
                        continue;
                    }
                    break;
                }
                continue;
            }
            bool notFound = true;
            for(int i = Get(currentAssignment, index) + 1; i < Get(feasible, index).Length; i++)
            {
                if(Get(feasible, index, i) != null)
                {
                    Get(currentAssignment, index) = i;
                    if(!Feasible(conflicts, currentAssignment, index))
                    {
                        continue;
                    }
                    currentU += Get(feasible, index, i).Value.DeltaUtility;
                    index++;
                    while(index < feasible.Length)
                    {
                        if(Get(alreadySolved, index))
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
                currentU -= Get(feasible, index, Get(currentAssignment, index)).Value.DeltaUtility;
                Get(currentAssignment, index--) = -1;
                while(index >= 0)
                {
                    if(Get(alreadySolved , index))
                    {
                        index--;
                        continue;
                    }
                    break;
                }
            }
        }
    }

    private bool[] SolveSimpleCases(PotentialPassengerTrip?[][] feasible, int[] currentAssignment, int[] bestAssignment, int[][] conflicts)
    {
        bool[] solved = new bool[currentAssignment.Length];
        for(int i = 0; i < conflicts.Length; i++)
        {
            int maxIndex = -1;
            float bestUtility = float.NegativeInfinity;
            for(int j = 0; j < conflicts[i].Length; j++)
            {
                var feasibilityRecord = Get(feasible, i, j);
                if (feasibilityRecord.HasValue)
                {
                    var util = feasibilityRecord.Value.DeltaUtility;
                    if(util > bestUtility)
                    {
                        bestUtility = util;
                        maxIndex = j;
                    }
                }
            }
            if(maxIndex >= 0)
            {
                if(Get(conflicts, i, maxIndex) == 0)
                {
                    // then we can just pick the best
                    Get(currentAssignment, i) = Get(bestAssignment, i) = maxIndex;
                    Get(solved, i) = true;
                }
            }
        }
        return solved;
    }
}

public struct PotentialPassengerTrip
{
    internal int DriverIndex;
    internal int PassengerIndex;
    internal int PassengerTripChainIndex;
    internal int PassengerTripIndex;

    public PotentialPassengerTrip(ITrip driverDestinationTrip, ITrip passengerDestinationTrip, float deltaUtility,
     int driverIndex,  int passengerIndex,  int passengerTripChainIndex, int passengerTripIndex)
    {
        DriverDestinationTrip = driverDestinationTrip;
        PassengerDestinationTrip = passengerDestinationTrip;
        DeltaUtility = deltaUtility;
        DriverIndex = driverIndex;
        PassengerIndex = passengerIndex;
        PassengerTripChainIndex = passengerTripChainIndex;
        PassengerTripIndex = passengerTripIndex;
    }
    public float DeltaUtility { get; private set; }

    public ITrip DriverDestinationTrip { get; private set; }

    public ITrip PassengerDestinationTrip { get; private set; }
}