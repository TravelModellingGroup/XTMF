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
using System.Collections.Generic;
using Tasha.Common;
using XTMF;

namespace Tasha.ModeChoice;

public class PassengerAlgo
{
    private ITashaRuntime TashaRuntime;

    public PassengerAlgo(ITashaRuntime runtime)
    {
        TashaRuntime = runtime;
    }

    public void AssignPassengerTrips(ITashaHousehold household)
    {
        //finding all potential trips
        Dictionary<ITashaPerson, List<List<ITripChain>>> potentialModeChoices = FindAllPotentialModesForTrips(household);
        if (potentialModeChoices.Count == 0) return;
        //choosing the optimal modes for each person from all the potential trips
        Dictionary<ITashaPerson, List<ITripChain>> optimalSets = ChooseOptimalSetForEachPerson(potentialModeChoices);
        if (optimalSets != null)
        {
            //removing duplicate passenger trips (ie. Two people carry same passenger)
            RemoveDuplicates(optimalSets);
            //clearing Auxiliary Trips since they have been used as temporary variables to this point
            ClearPassengerTrips(household);
            //adding auxiliary passenger trips
            AddPassengerTrips(household, optimalSets);
            //combining connecting Auxiliary trips to tripchains
            FinalizeAuxTrips(household);
        }
    }

    public double CalculateU(ITripChain tripChain)
    {
        ITrip facilitatedTrip = tripChain["FacilitateTrip"] as ITrip;
        ISharedMode facilitatedTripMode = (ISharedMode)tripChain["SharedMode"];
        //the mode data for the facilitated trip
        ModeData facilitatedTripData = ModeData.Get(facilitatedTrip) ?? throw new XTMFRuntimeException(null, "There was no facilitated Trip Data!");
        if (TashaRuntime == null)
        {
            throw new XTMFRuntimeException(null, "Tasha runtime was null!");
        }
        double passengersU = facilitatedTripData.U(facilitatedTripMode.ModeChoiceArrIndex);
        double driversU = CalculateUofAuxTrip(tripChain);
        return passengersU + driversU;
    }

    /// <summary>
    /// Adds the auxiliary trip chain to the person if enough vehicles are available
    /// </summary>
    /// <param name="household"></param>
    /// <param name="optimalSets"></param>
    private void AddPassengerTrips(ITashaHousehold household, Dictionary<ITashaPerson, List<ITripChain>> optimalSets)
    {
        foreach (var optSet in optimalSets)
        {
            ITashaPerson person = optSet.Key;
            List<ITripChain> optimalSet = optSet.Value;
            if (person.AuxTripChains == null)
            {
                person.AuxTripChains = new List<ITripChain>(5);
            }
            else
            {
                person.AuxTripChains.Clear();
            }
            var length = optimalSet.Count;
            for (int i = 0; i < length; i++)
            {
                var vehicles = optimalSet[i].RequiresVehicle;
                var vehiclesLength = vehicles.Count;
                for (int j = 0; j < vehiclesLength; j++)
                {
                    if (household.NumberOfVehicleAvailable(new TashaTimeSpan(optimalSet[i].StartTime, optimalSet[i].EndTime), vehicles[j], true) > 0)
                    {
                        person.AuxTripChains.Add(optimalSet[i]);
                    }
                }
            }
        }
    }

    private double CalculateUofAuxTrip(ITripChain tripChain)
    {
        return (double)tripChain["U"];
    }

    private double CalculateUofTripChainSet(List<ITripChain> tripChains)
    {
        double utility = 0;
        var length = tripChains.Count;
        for (int i = 0; i < length; i++)
        {
            utility += CalculateU(tripChains[i]);
        }
        return utility;
    }

    private Dictionary<ITashaPerson, List<ITripChain>> ChooseOptimalSetForEachPerson(Dictionary<ITashaPerson, List<List<ITripChain>>> potentialModeChoices)
    {
        Dictionary<ITashaPerson, List<ITripChain>> optimalTripChainsForPerson = new(potentialModeChoices.Count);

        //For each person find their optimal set of Aux trip chains
        foreach (var personset in potentialModeChoices)
        {
            Dictionary<List<ITripChain>, double> tripChainsUtility = [];
            var personTripChains = personset.Value;
            var length = personTripChains.Count;
            for (int i = 0; i < length; i++)
            {
                tripChainsUtility.Add(personTripChains[i], CalculateUofTripChainSet(personTripChains[i]));
            }

            List<ITripChain> maxUtilitySet = GetMaxUtilityTripChainSet(tripChainsUtility);

            if (maxUtilitySet == null)
            {
                return null;
            }

            //personset.Key.Attach("OptimalAuxTripChainSet", MaxUtilitySet);

            //adding the max utility set
            optimalTripChainsForPerson.Add(personset.Key, maxUtilitySet);
        }

        return optimalTripChainsForPerson;
    }

    private void ClearPassengerTrips(ITashaHousehold household)
    {
        var persons = household.Persons;
        var personsLength = persons.Length;
        for (int i = 0; i < personsLength; i++)
        {
            var auxTripChains = persons[i].AuxTripChains;
            if (auxTripChains == null)
            {
                persons[i].AuxTripChains = new List<ITripChain>(5);
            }
            else
            {
                persons[i].AuxTripChains.Clear();
            }
        }
    }

    /// <summary>
    /// Copies a trip chain to a new trip chain
    /// </summary>
    /// <param name="tripchains">The trip chain to copy</param>
    /// <param name="newTripChain">The new trip chain</param>
    private void CopyChain(List<ITripChain> tripchains, out List<ITripChain> newTripChain)
    {
        var length = tripchains.Count;
        newTripChain = new List<ITripChain>(length);
        for (int i = 0; i < length; i++)
        {
            newTripChain.Add(tripchains[i]);
        }
    }

    private void FinalizeAuxTrips(ITashaHousehold household)
    {
        var persons = household.Persons;
        var pLength = persons.Length;
        for (int i = 0; i < pLength; i++)
        {
            var aux = persons[i].AuxTripChains;
            var auxLength = aux.Count;
            for (int j = 0; j < auxLength; j++)
            {
                ITrip facilitatedTrip = (ITrip)aux[j]["FacilitateTrip"];
                ISharedMode facilitatedTripMode = (ISharedMode)aux[j]["SharedMode"];
                facilitatedTrip.Mode = facilitatedTripMode;
                var trips = aux[j].Trips;
                var tripsLength = trips.Count;
                var associatedMode = facilitatedTripMode.AssociatedMode;
                for (int t = 0; t < tripsLength; t++)
                {
                    trips[t].Mode = associatedMode;
                }
            }
        }
    }

    private Dictionary<ITashaPerson, List<List<ITripChain>>> FindAllPotentialModesForTrips(ITashaHousehold household)
    {
        Dictionary<ITashaPerson, List<List<ITripChain>>> possibleChains = new(household.Persons.Length);
        var people = household.Persons;
        var peopleLength = people.Length;
        for (int i = 0; i < peopleLength; i++)
        {
            // check to see if there are no aux chain, if so just continue on
            if (people[i].AuxTripChains.Count == 0)
            {
                continue;
            }
            List<List<ITripChain>> potentialChains = [];
            //sorting trips by start time (prereq for getting conflicting chains)
            SortTrips(people[i].AuxTripChains);
            FindPotentialTripChainsRec(people[i].AuxTripChains, 0, potentialChains);
            possibleChains.Add(people[i], potentialChains);
        }
        return possibleChains;
    }

    private ITripChain FindHighestUtility(Dictionary<ITripChain, double> conflictingUtilities)
    {
        double max = double.MinValue;
        ITripChain best = null;
        foreach (var element in conflictingUtilities)
        {
            if (element.Value > max)
            {
                max = element.Value;
                best = element.Key;
            }
        }
        return best;
    }

    /// <summary>
    /// Recursive algorithm to find all potential non conflicting chains from the given set
    /// </summary>
    /// <param name="tripchains"></param>
    /// <param name="currentChain"></param>
    /// <param name="potentialChains"></param>
    private void FindPotentialTripChainsRec(List<ITripChain> tripchains,
        int currentChain, List<List<ITripChain>> potentialChains)
    {
        if (currentChain >= tripchains.Count)
        {
            potentialChains.Add(tripchains);
            return;
        }

        ITripChain currentTripChain = tripchains[currentChain];
        //gets all trip chains occuring at the same time as this one
        List<ITripChain> conflictingChains = GetConflictingChains(tripchains[currentChain], tripchains);
        //no conflicting chains so include it
        if (conflictingChains.Count == 0)
        {
            //no conflicting set so continue
            FindPotentialTripChainsRec(tripchains, ++currentChain, potentialChains);
        }
        else
        {
            //copy this trip chain to a new one
            CopyChain(tripchains, out List<ITripChain> tripChainRemovedConflicts);
            var length = conflictingChains.Count;
            for (int i = 0; i < length; i++)
            {
                tripChainRemovedConflicts.Remove(conflictingChains[i]);
            }
            //Find potential tripchains with this trip chain included
            FindPotentialTripChainsRec(tripChainRemovedConflicts, currentChain + 1, potentialChains);
            CopyChain(tripchains, out List<ITripChain> tripChainWithoutThisChain);
            tripChainWithoutThisChain.Remove(currentTripChain);
            //Find potential tripchain without this trip chain included
            FindPotentialTripChainsRec(tripChainWithoutThisChain, currentChain, potentialChains);
        }
    }

    private Time GetAuxTripChainEndTime(ITripChain auxTripChain)
    {
        if (auxTripChain["ConnectingChain"] == null)
        {
            return auxTripChain.EndTime;
        }
        ITrip connectingChain1 = (ITrip)auxTripChain["ConnectingChain"];
        Activity purpose = (Activity)auxTripChain["Purpose"];
        Time endTime;
        if (purpose == Activity.Dropoff)
        {
            endTime = connectingChain1.TripChain.EndTime;
        }
        else
        {
            endTime = auxTripChain.EndTime;
        }
        return endTime;
    }

    private Time GetAuxTripChainStartTime(ITripChain auxTripChain)
    {
        if (auxTripChain["ConnectingChain"] == null)
        {
            return auxTripChain.StartTime;
        }
        ITrip connectingChain1 = (ITrip)auxTripChain["ConnectingChain"];
        Activity purpose = (Activity)auxTripChain["Purpose"];
        Time startTime;
        if (purpose != Activity.Dropoff)
        {
            startTime = connectingChain1.TripChain.StartTime;
        }
        else
        {
            startTime = auxTripChain.StartTime;
        }
        return startTime;
    }

    /// <summary>
    /// Gets all the conflicting trip chains to a given trip chain
    /// </summary>
    /// <param name="tripchain">the given trip chain</param>
    /// <param name="tripchains">the trip chains to compare with</param>
    /// <returns></returns>
    private List<ITripChain> GetConflictingChains(ITripChain tripchain, List<ITripChain> tripchains)
    {
        List<ITripChain> conflictingChains = [];
        Time startTime = GetAuxTripChainStartTime(tripchain);
        Time endTime = GetAuxTripChainEndTime(tripchain);
        var length = tripchains.Count;
        for (int i = 0; i < length; i++)
        {
            if (tripchains[i] == tripchain)
            {
                continue;
            }
            //passed the trip chain no need to keep checking
            if (tripchains[i].StartTime > tripchain.EndTime)
            {
                return conflictingChains;
            }
            Time otherStartTime = GetAuxTripChainStartTime(tripchains[i]);
            Time otherEndTime = GetAuxTripChainEndTime(tripchains[i]);
            if (otherStartTime < endTime && otherEndTime > startTime)
            {
                conflictingChains.Add(tripchains[i]);
            }
        }
        return conflictingChains;
    }

    private List<ITripChain> GetMaxUtilityTripChainSet(Dictionary<List<ITripChain>, double> tripChainsUtility)
    {
        double max = double.MinValue;
        List<ITripChain> maxSet = null;
        foreach (var element in tripChainsUtility)
        {
            if (element.Value > max)
            {
                max = element.Value;
                maxSet = element.Key;
            }
        }
        return maxSet;
    }

    /// <summary>
    /// Removes Duplicate Passenger trips (ie. two ppl facilitating one passenger
    /// </summary>
    /// <param name="optimalSets"></param>
    /// <returns></returns>
    private void RemoveDuplicates(Dictionary<ITashaPerson, List<ITripChain>> optimalSets)
    {
        List<ITripChain> duplicates = [];
        //finding duplicates
        foreach (var s in optimalSets)
        {
            List<ITripChain> tripChains = s.Value;
            var tripChainsLength = tripChains.Count;
            for (int i = 0; i < tripChainsLength; i++)
            {
                Dictionary<ITripChain, double> conflictingUtilities = [];
                //the faciliated trip
                ITrip facilitatedTrip = tripChains[i]["FacilitateTrip"] as ITrip;
                double uOfAuxTrip = CalculateUofAuxTrip(tripChains[i]);
                conflictingUtilities.Add(tripChains[i], uOfAuxTrip);
                //finding tripchain and U of duplicate passenger trips
                foreach (var s2 in optimalSets)
                {
                    if (s.Key == s2.Key)
                    {
                        continue;
                    }
                    List<ITripChain> tripChains2 = s2.Value;
                    var tripChain2Length = tripChains2.Count;
                    for (int j = 0; j < tripChain2Length; j++)
                    {
                        ITrip facilitatedTrip2 = tripChains2[j]["FacilitateTrip"] as ITrip;

                        if (facilitatedTrip2 == facilitatedTrip)
                        {
                            conflictingUtilities.Add(tripChains2[j], CalculateUofAuxTrip(tripChains2[j]));
                        }
                    }
                }
                ITripChain best = FindHighestUtility(conflictingUtilities);
                conflictingUtilities.Remove(best);
                foreach (var element in conflictingUtilities)
                {
                    duplicates.Add(element.Key);
                }
            }
        }
        //removing duplicates
        foreach (var s in optimalSets)
        {
            List<ITripChain> tripChains = s.Value;
            var duplicatesLength = duplicates.Count;
            for (int i = 0; i < duplicatesLength; i++)
            {
                // Remove the duplicate if it exists
                tripChains.Remove(duplicates[i]);
            }
        }
    }

    /// <summary>
    /// Sorts the trip chains in the list by start time
    /// </summary>
    /// <param name="list"></param>
    private void SortTrips(List<ITripChain> list)
    {
        int length = list.Count;
        for (int i = 0; i < length; i++)
        {
            int min = i;
            for (int j = i + 1; j < length; j++)
            {
                if (list[min].StartTime > list[j].StartTime)
                {
                    min = j;
                }
            }
            if (min != i)
            {
                var temp = list[min];
                list[min] = list[i];
                list[i] = temp;
            }
        }
    }
}