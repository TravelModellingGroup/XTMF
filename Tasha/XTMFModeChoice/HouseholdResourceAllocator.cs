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
    public struct Conflict
    {
        public List<ITripChain> TripChains;

        public Time EndTime
        {
            get
            {
                var endTime = TripChains[0].EndTime;
                for(int i = 1; i < TripChains.Count; i++)
                {
                    if(TripChains[i].EndTime > endTime)
                    {
                        endTime = TripChains[i].EndTime;
                    }
                }
                return endTime;
            }
        }

        public Time StartTime
        {
            get
            {
                var startTime = TripChains[0].StartTime;
                for(int i = 1; i < TripChains.Count; i++)
                {
                    if(TripChains[i].StartTime < startTime)
                    {
                        startTime = TripChains[i].StartTime;
                    }
                }
                return startTime;
            }
        }
    }

    public struct VehicleAllocationWindow
    {
        public int AvailableCars;
        public TashaTimeSpan TimeSpan;
    }

    public sealed class HouseholdResourceAllocator
    {
        /// <summary>
        /// The list of conflicts for this household iteration
        /// </summary>
        public List<Conflict> Conflicts;

        public List<VehicleAllocationWindow> VehicleAvailability;
        private float BestConflictUtility = float.NegativeInfinity;
        private ITashaHousehold household;
        private ITashaMode[] Modes;

        /// <summary>
        /// A working set for the resolution of the household working set
        /// </summary>
        private int[][] ResolutionWorkingSet;

        private CurrentPosition[] Scan;

        public HouseholdResourceAllocator(ITashaHousehold household, ITashaMode[] modes)
        {
            this.household = household;
            var persons = household.Persons;
            // allocate all of the memory required for resource allocation
            Resolution = new int[persons.Length][];
            Modes = modes;
            for(int i = 0; i < persons.Length; i++)
            {
                Resolution[i] = new int[persons[i].TripChains.Count];
            }
        }

        /// <summary>
        /// The Resolution for the household conflicts
        /// </summary>
        public int[][] Resolution { get; private set; }

        public void BuildVehicleAvailabilities(ModeChoiceHouseholdData householdData, IVehicle[] vehicles)
        {
            int numberOfVehicles = vehicles.Length;
            var numberOfPeople = Resolution.Length;
            if(numberOfVehicles == 0) return;
            VehicleAvailability = new List<VehicleAllocationWindow>();
            if(Scan == null)
            {
                Scan = new CurrentPosition[numberOfPeople];
            }
            for(int i = 0; i < numberOfPeople; i++)
            {
                Scan[i] = new CurrentPosition() { Position = 0, TripChains = household.Persons[i].TripChains};
            }
            List<ITripChain> activeTours = new List<ITripChain>(numberOfVehicles);
            var endOfDay = new Time() { Hours = 28 };
            Time previousAllocationTime = Time.StartOfDay;
            while(true)
            {
                int nextPerson = -1;
                Time nextTime = endOfDay;
                for(int i = 0; i < numberOfPeople; i++)
                {
                    if(Scan[i].Position < Scan[i].TripChains.Count)
                    {
                        var personNextStartTime = Scan[i].TripChains[Scan[i].Position].StartTime;
                        if(personNextStartTime < nextTime)
                        {
                            nextTime = personNextStartTime;
                            nextPerson = i;
                        }
                    }
                }
                if(nextPerson == -1)
                {
                    break;
                }

                while(true)
                {
                    Time earliestEnd = nextTime;
                    int earliestIndex = -1;
                    var length = activeTours.Count;
                    for(int i = 0; i < length; i++)
                    {
                        if(activeTours[i].EndTime <= earliestEnd)
                        {
                            earliestIndex = i;
                            earliestEnd = activeTours[i].EndTime;
                        }
                    }
                    if(earliestIndex >= 0)
                    {
                        VehicleAvailability.Add(new VehicleAllocationWindow()
                        {
                            TimeSpan = new TashaTimeSpan(previousAllocationTime, earliestEnd),
                            AvailableCars = numberOfVehicles - activeTours.Count
                        });
                        previousAllocationTime = earliestEnd;
                        activeTours.RemoveAt(earliestIndex);
                        continue;
                    }
                    break;
                }

                var nextTourData = householdData.PersonData[nextPerson].TripChainData[Scan[nextPerson].Position];
                // if it isn't a joint trip not made by the representative
                if(!(nextTourData.TripChain.JointTrip && !nextTourData.TripChain.JointTripRep))
                {
                    if(Resolution[nextPerson][Scan[nextPerson].Position] > 0)
                    {
                        var endTime = nextTourData.TripChain.StartTime;
                        VehicleAvailability.Add(new VehicleAllocationWindow()
                        {
                            TimeSpan = new TashaTimeSpan(previousAllocationTime, endTime),
                            AvailableCars = numberOfVehicles - activeTours.Count
                        });
                        previousAllocationTime = endTime;
                        activeTours.Add(nextTourData.TripChain);
                    }
                }
                Scan[nextPerson].Position++;
                // now check to see if the next trip wants to be
                // at the end of this step fail if there are more active users than vehicles
            }
            while(true)
            {
                Time earliestEnd = Time.EndOfDay;
                int earliestIndex = -1;
                var length = activeTours.Count;
                for(int i = 0; i < length; i++)
                {
                    if(activeTours[i].EndTime <= earliestEnd)
                    {
                        earliestIndex = i;
                        earliestEnd = activeTours[i].EndTime;
                    }
                }
                if(earliestIndex >= 0)
                {
                    VehicleAvailability.Add(new VehicleAllocationWindow()
                    {
                        TimeSpan = new TashaTimeSpan(previousAllocationTime, earliestEnd),
                        AvailableCars = numberOfVehicles - activeTours.Count
                    });
                    previousAllocationTime = earliestEnd;
                    activeTours.RemoveAt(earliestIndex);
                    continue;
                }
                break;
            }
            VehicleAvailability.Add(new VehicleAllocationWindow()
            {
                TimeSpan = new TashaTimeSpan(previousAllocationTime, Time.EndOfDay),
                AvailableCars = numberOfVehicles // activeTours.Count is 0
            });
        }

        internal int[][] Resolve(ModeChoiceHouseholdData householdData, IVehicleType[] vehicleTypes, int hhldIterations)
        {
            // check if there are no vehicles
            var vehicles = household.Vehicles;
            if(vehicles.Length == 0)
            {
                return Resolution;
            }
            // if we actually have to compute something go and do it
            if(hhldIterations > 0)
            {
                ClearData();
            }
            // check to see if we have enough resources for everyone
            if(vehicleTypes.Length == 1)
            {
                if(!ProcessSingleVehicleType(householdData, vehicleTypes, vehicles))
                {
                    return null;
                }
            }
            else
            {
                throw new NotImplementedException("We will get to more than one vehicle type later!");
            }
            return Resolution;
        }

        private void AssignBest(ModeChoiceHouseholdData householdData, ITripChain[][] personConflicts, bool[][] bestAssignment)
        {
            for(int i = 0; i < householdData.PersonData.Length; i++)
            {
                var person = householdData.PersonData[i];
                for(int j = 0; j < person.TripChainData.Length; j++)
                {
                    var tc = person.TripChainData[j];
                    var bestU = float.NegativeInfinity;
                    int index;
                    if((index = IndexOf(tc.TripChain, personConflicts[i])) != -1)
                    {
                        index = bestAssignment[i][index] ? 1 : 0;
                    }
                    else
                    {
                        index = -1;
                        for(int k = 0; k < tc.BestPossibleAssignmentForVehicleType.Length; k++)
                        {
                            var assignment = tc.BestPossibleAssignmentForVehicleType[k];
                            if(assignment != null)
                            {
                                if(assignment.U >= bestU)
                                {
                                    index = k;
                                    bestU = assignment.U;
                                }
                            }
                        }
                    }
                    Resolution[i][j] = index;
                }
            }
        }

        private void AssignBestToAll(ModeChoiceHouseholdData householdData)
        {
            for(int i = 0; i < householdData.PersonData.Length; i++)
            {
                var person = householdData.PersonData[i];
                for(int j = 0; j < person.TripChainData.Length; j++)
                {
                    var tc = person.TripChainData[j];
                    var bestU = float.NegativeInfinity;
                    var index = -1;
                    for(int k = 0; k < tc.BestPossibleAssignmentForVehicleType.Length; k++)
                    {
                        var assignment = tc.BestPossibleAssignmentForVehicleType[k];
                        if(assignment != null)
                        {
                            if(assignment.U >= bestU)
                            {
                                index = k;
                                bestU = assignment.U;
                            }
                        }
                    }
                    Resolution[i][j] = index;
                }
            }
        }

        private void AssignToBest(bool[][] assignResource, bool[][] bestAssignment)
        {
            for(int i = 0; i < assignResource.Length; i++)
            {
                Array.Copy(assignResource[i], bestAssignment[i], assignResource[i].Length);
            }
        }

        private bool CheckPossibleUsers(IVehicleType[] vehicleTypes, IVehicle[] vehicles)
        {
            int numberOfUsers = 0;
            var typeZero = vehicleTypes[0];
            for(int i = 0; i < Resolution.Length; i++)
            {
                if(typeZero.CanUse(household.Persons[i]))
                {
                    numberOfUsers++;
                }
            }
            return vehicles.Length >= numberOfUsers;
        }

        private bool CheckPossibleUsersAtTimeOfDay(ModeChoiceHouseholdData householdData, IVehicle[] vehicles)
        {
            if(Conflicts == null)
            {
                Conflicts = new List<Conflict>();
            }
            else
            {
                Conflicts.Clear();
            }
            if(Scan == null)
            {
                Scan = new CurrentPosition[Resolution.Length];
            }
            for(int i = 0; i < Scan.Length; i++)
            {
                Scan[i] = new CurrentPosition() { Position = 0, TripChains = household.Persons[i].TripChains};
            }
            List<ITripChain> activeTours = new List<ITripChain>(vehicles.Length);
            var endOfDay = new Time() { Hours = 28 };
            while(true)
            {
                int nextPerson = -1;
                Time nextTime = endOfDay;
                for(int i = 0; i < Scan.Length; i++)
                {
                    if(Scan[i].Position < Scan[i].TripChains.Count)
                    {
                        var personNextStartTime = Scan[i].TripChains[Scan[i].Position].StartTime;
                        if(personNextStartTime < nextTime)
                        {
                            nextTime = personNextStartTime;
                            nextPerson = i;
                        }
                    }
                }
                if(nextPerson == -1)
                {
                    break;
                }
                for(int i = 0; i < activeTours.Count; i++)
                {
                    if(activeTours[i].EndTime <= nextTime)
                    {
                        activeTours.RemoveAt(i);
                        i--;
                    }
                }
                var nextTourData = householdData.PersonData[nextPerson].TripChainData[Scan[nextPerson].Position];
                // if it isn't a joint trip not made by the representative
                if(!(nextTourData.TripChain.JointTrip && !nextTourData.TripChain.JointTripRep))
                {
                    // and they want a car
                    if(nextTourData.BestPossibleAssignmentForVehicleType[1] != null
                        && nextTourData.BestPossibleAssignmentForVehicleType[1].U >= (nextTourData.BestPossibleAssignmentForVehicleType[0] == null ? float.NegativeInfinity : nextTourData.BestPossibleAssignmentForVehicleType[0].U))
                    {
                        var tc = Scan[nextPerson].TripChains[Scan[nextPerson].Position];
                        for(int j = 0; j < tc.Trips.Count; j++)
                        {
                            tc.Trips[j].Mode = Modes[nextTourData.BestPossibleAssignmentForVehicleType[1].PickedModes[j]];
                        }
                        activeTours.Add(tc);
                        // There could be a conflict for this vehicle's allocation
                        if(activeTours.Count > vehicles.Length)
                        {
                            Conflicts.Add(new Conflict() { TripChains = Clone(activeTours) });
                        }
                    }
                }
                Scan[nextPerson].Position++;
            }
            return Conflicts.Count == 0;
        }

        private void ClearData()
        {
            // clear out the old results
            var resolution = Resolution;
            for(int i = 0; i < resolution.Length; i++)
            {
                var row = resolution[i];
                for(int j = 0; j < row.Length; j++)
                {
                    row[j] = 0;
                }
            }
        }

        private List<ITripChain> Clone(List<ITripChain> activeTours)
        {
            List<ITripChain> problems = new List<ITripChain>(activeTours.Count);
            for(int i = 0; i < activeTours.Count; i++)
            {
                problems.Add(activeTours[i]);
            }
            return problems;
        }

        private bool ComputeUtilityOfAssignment(ModeChoiceHouseholdData householdData, bool[][] assignment, ITripChain[][] conflictTripChains, out float utility)
        {
            var sum = 0f;
            for(int i = 0; i < assignment.Length; i++)
            {
                var row = assignment[i];
                for(int j = 0; j < row.Length; j++)
                {
                    var u = householdData.PersonData[i].TripChainData[IndexOf(conflictTripChains[i][j], household.Persons[i].TripChains)]
                        .BestPossibleAssignmentForVehicleType[row[j] ? 1 : 0];
                    if(u == null)
                    {
                        utility = float.NegativeInfinity;
                        return false;
                    }
                    sum += u.U;
                }
            }
            utility = sum;
            return true;
        }

        private int IndexOf<T>(T iVehicleType, IList<T> vehicleTypes) where T : class
        {
            var length = vehicleTypes.Count;
            for(int i = 0; i < length; i++)
            {
                if(iVehicleType == vehicleTypes[i])
                {
                    return i;
                }
            }
            return -1;
        }

        private void InitializeProcessHardSingleVehicleCaseData(ITripChain[][] personConflicts, bool[][] assignResource, bool[][] bestAssignment)
        {
            var persons = household.Persons;
            var numberOfPeople = persons.Length;
            var numberOfConflicts = Conflicts.Count;
            List<ITripChain>[] newPersonConflicts = new List<ITripChain>[numberOfPeople];
            List<bool>[] newAssignResource = new List<bool>[numberOfPeople];
            // initialize our temp data
            for(int i = 0; i < numberOfPeople; i++)
            {
                newPersonConflicts[i] = new List<ITripChain>();
                newAssignResource[i] = new List<bool>();
            }
            if(ResolutionWorkingSet == null)
            {
                ResolutionWorkingSet = new int[numberOfPeople][];
                for(int i = 0; i < numberOfPeople; i++)
                {
                    ResolutionWorkingSet[i] = new int[Resolution[i].Length];
                }
            }
            // Load in the data with unique trip chains
            LoadUniqueTripChainConflicts(persons, numberOfConflicts, newPersonConflicts, newAssignResource);
            for(int i = 0; i < numberOfPeople; i++)
            {
                personConflicts[i] = newPersonConflicts[i].ToArray();
                assignResource[i] = newAssignResource[i].ToArray();
                bestAssignment[i] = newAssignResource[i].ToArray();
            }
        }

        private bool IsValidAssignment(int numberOfResource, bool[][] assignment, ITripChain[][] conflictTripChains, ModeChoiceHouseholdData householdData, out float utility)
        {
            var persons = household.Persons;
            var conflicts = Conflicts;
            var numberOfConflicts = conflicts.Count;
            utility = float.NegativeInfinity;
            for(int i = 0; i < numberOfConflicts; i++)
            {
                var chainsInConflict = conflicts[i].TripChains;
                var numberOfChainsInConflict = chainsInConflict.Count;
                var assignedCount = 0;
                for(int j = 0; j < numberOfChainsInConflict; j++)
                {
                    var personIndex = IndexOf(chainsInConflict[j].Person, persons);
                    var chainIndex = IndexOf(chainsInConflict[j], conflictTripChains[personIndex]);
                    if(assignment[personIndex][chainIndex])
                    {
                        assignedCount++;
                    }
                }
                if(assignedCount > numberOfResource)
                {
                    return false;
                }
            }
            // Since this is a valid assignment compute the utility of the assignment
            return ComputeUtilityOfAssignment(householdData, assignment, conflictTripChains, out utility);
        }

        private void LoadUniqueTripChainConflicts(ITashaPerson[] persons, int numberOfConflicts, List<ITripChain>[] personConflicts, List<bool>[] assignResource)
        {
            // Gather the unique trip chains in the conflicts and assign them to people
            for(int i = 0; i < numberOfConflicts; i++)
            {
                var tripChains = Conflicts[i].TripChains;
                var numberOfTripChains = tripChains.Count;
                for(int j = 0; j < numberOfTripChains; j++)
                {
                    int personIndex = IndexOf(persons, tripChains[j].Person);
                    var tc = tripChains[j];
                    if(!personConflicts[personIndex].Contains(tc))
                    {
                        personConflicts[personIndex].Add(tc);
                        assignResource[personIndex].Add(false);
                    }
                }
            }
        }

        private static int IndexOf<T>(T[] data, T toLookFor) where T : class
        {
            for(int i = 0; i < data.Length; i++)
            {
                if(data[i] == toLookFor)
                {
                    return i;
                }
            }
            return -1;
        }

        private bool ProcessHardSingleVehicleCase(ModeChoiceHouseholdData householdData, IVehicle[] vehicles)
        {
            var persons = household.Persons;
            var numberOfPeople = persons.Length;
            ITripChain[][] personConflicts = new ITripChain[numberOfPeople][];
            bool[][] assignResource = new bool[numberOfPeople][];
            bool[][] bestAssignment = new bool[numberOfPeople][];
            // Initialize our data with the unique trip chains in conflict
            InitializeProcessHardSingleVehicleCaseData(personConflicts, assignResource, bestAssignment);
            BestConflictUtility = float.NegativeInfinity;
            if(RecursiveExplore(householdData, vehicles.Length, assignResource, bestAssignment, personConflicts, 0, 0))
            {
                AssignBest(householdData, personConflicts, bestAssignment);
                return true;
            }
            return false;
        }

        private bool ProcessSingleVehicleType(ModeChoiceHouseholdData householdData, IVehicleType[] vehicleTypes, IVehicle[] vehicles)
        {
            // check to see if there are as many vehicles as number of people
            int activeVehicles = vehicles.Length;
            var numberOfPeople = Resolution.Length;

            // See if we have at least the same number of the vehicle type than people that can use it
            if(activeVehicles >= numberOfPeople || CheckPossibleUsers(vehicleTypes, vehicles))
            {
                // Assign to anyone who wants one
                AssignBestToAll(householdData);
                return true;
            }

            //Check users at time of day
            if(CheckPossibleUsersAtTimeOfDay(householdData, vehicles))
            {
                AssignBestToAll(householdData);
                return true;
            }

            if(ProcessHardSingleVehicleCase(householdData, vehicles))
            {
                return true;
            }
            return false;
            //throw new XTMFRuntimeException( "Failed to assign vehicles for household #" + this.household.HouseholdId + "!" );
        }

        private bool RecursiveExplore(ModeChoiceHouseholdData householdData, int numberOfResource, bool[][] assignResource, bool[][] bestAssignment, ITripChain[][] personConflicts, int person, int tIndex)
        {
            var any = false;
            if(tIndex < assignResource[person].Length)
            {
                assignResource[person][tIndex] = false;
                if(RecursiveExplore(householdData, numberOfResource, assignResource, bestAssignment, personConflicts, person, tIndex + 1))
                {
                    any = true;
                }
                assignResource[person][tIndex] = true;
                if(RecursiveExplore(householdData, numberOfResource, assignResource, bestAssignment, personConflicts, person, tIndex + 1))
                {
                    any = true;
                }
                return any;
            }
            if(assignResource.Length == person + 1)
            {
                if (IsValidAssignment(numberOfResource, assignResource, personConflicts, householdData, out float utility))
                {
                    if (utility > BestConflictUtility)
                    {
                        BestConflictUtility = utility;
                        AssignToBest(assignResource, bestAssignment);
                    }
                    return true;
                }
                return false;
            }
            return RecursiveExplore(householdData, numberOfResource, assignResource, bestAssignment, personConflicts, person + 1, 0);
        }

        private struct CurrentPosition
        {
            internal int Position;
            internal List<ITripChain> TripChains;
        }
    }
}