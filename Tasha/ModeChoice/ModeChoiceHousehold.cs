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
using Datastructure;
using Tasha.Common;
using XTMF;
// ReSharper disable InconsistentNaming

namespace Tasha.ModeChoice
{
    internal struct UnAssignedModeSet
    {
        public ITripChain Chain;
        public ModeSet Set;
        public IVehicleType VehicleType;

        public UnAssignedModeSet(ITripChain chain, ModeSet mSet, IVehicleType vehicleType)
            : this()
        {
            Chain = chain;
            Set = mSet;
            VehicleType = vehicleType;
        }

        public ITripChain GetChainWithAssignedModes()
        {
            int x = 0;
            foreach ( var trip in Chain.Trips )
            {
                trip.Mode = Set.ChosenMode[x];
                x++;
            }
            return Chain;
        }

        public void ResetChain()
        {
            foreach ( var trip in Chain.Trips )
            {
                trip.Mode = null;
            }
        }
    }

    internal static class ModeChoiceHousehold
    {
        public static ITashaRuntime TashaRuntime;
        internal static ModeChoice ModeChoice;
        private static PassengerAlgo PassAlgo;

        public enum ModeAssignmentHouseHold
        {
            NULL_SET, SIMPLE_CASE, ADVANCED_CASE
        }

        public static void AssignJointTours(ITashaHousehold household)
        {
            Dictionary<int, List<ITripChain>> JointTours = household.JointTours;
            ITashaMode rideShare = null;
            int rideShareIndex = -1;
            var allModes = TashaRuntime.AllModes;
            var numberOfModes = allModes.Count;
            for ( int i = 0; i < numberOfModes; i++ )
            {
                if ( ( allModes[i] is ISharedMode ) && allModes[i].ModeName == "RideShare" )
                {
                    rideShare = allModes[i];
                    rideShareIndex = i;
                }
            }
            //no rideshare mode?
            if ( rideShare == null )
            {
                return;
            }
            //go through each joint tour and assign identical modes for each tripchain in a tour if possible
            foreach ( var element in JointTours )
            {
                List<ITripChain> tripChains = element.Value;
                //does a non vehicle tour exist
                bool nonVehicleTour = BestNonVehicleModeSetForTour(tripChains, out IList<ITashaMode> nonVehicleModesChosen, out double nonVehicleU);
                //the potential driver in this tour
                ITashaPerson potentialDriver = null;
                //finding potential driver who already has the car
                foreach ( var tripChain in tripChains )
                {
                    if ( tripChain.RequiresVehicle.Contains( rideShare.RequiresVehicle ) )
                    {
                        potentialDriver = tripChain.Person;
                        break;
                    }
                }
                //if no one has the car check if one is available
                if ( potentialDriver == null )
                {
                    if ( household.NumberOfVehicleAvailable(
                        new TashaTimeSpan( tripChains[0].StartTime, tripChains[0].EndTime ), rideShare.RequiresVehicle, false ) > 0 )
                    {
                        foreach ( var tc in tripChains )
                        {
                            if ( rideShare.RequiresVehicle.CanUse( tc.Person ) )
                            {
                                potentialDriver = tc.Person;
                                break;
                            }
                        }
                    }
                }
                //No potential driver and no nonVehicle tour means that ppl in this tour have to take different modes which shouldnt happen
                if ( ( potentialDriver == null ) & ( !nonVehicleTour ) )
                {
                    continue;
                }
                double oldU = Double.MinValue;
                if ( nonVehicleTour )
                {
                    oldU = nonVehicleU;
                }
                //no driver, go to next tour
                if ( potentialDriver == null )
                {
                    continue;
                }
                double newU = 0.0;
                bool success = true;
                /*
                 * Now we assign the rideshare mode and if the total U of everyone using rideshare is less than that
                 * of a non personal vehicle mode, everyone uses rideshare
                 *
                 */
                foreach ( var tripChain in tripChains )
                {
                    foreach ( var trip in tripChain.Trips )
                    {
                        ModeData md = (ModeData)trip.GetVariable( "MD" );
                        trip.Mode = rideShare;
                        trip.CalculateVTrip();
                        newU += md.U( rideShareIndex );
                    }
                    if ( !tripChain.Feasible() )
                    {
                        success = false;
                        break;
                    }
                }
                //reset modes
                if ( ( !success || newU <= oldU ) & ( nonVehicleTour ) )
                {
                    SetModes( tripChains, nonVehicleModesChosen );
                    //go to next joint trip
                }
            }
        }

        /// <summary>
        /// Gets the index of the best vehicle for this mode set
        /// 0 being transit and 1 .... n being the vehicles
        /// </summary>
        /// <param name="set"></param>
        /// <returns></returns>
        public static int BestVehicle(ModeSet[] set)
        {
            int bestIndex = 0;
            double bestU = double.MinValue;
            int i = 0;
            if ( set == null ) return -1;
            var length = set.Length;
            for ( int j = 0; j < length; j++ )
            {
                if ( ( set[j] != null ) && ( set[j].U > bestU ) )
                {
                    bestU = set[j].U;
                    bestIndex = i;
                }
                i++;
            }
            return bestIndex;
        }

        /// <summary>
        /// Finds the best possible assignment of vehicles to trip chain
        ///
        /// - Using a brute force algorithm
        /// </summary>
        /// <param name="tripChains">The trip chains</param>
        /// <param name="vehicles">The vehicles</param>
        /// <returns>The best possible assignment</returns>
        public static IVehicleType[] FindBestPossibleAssignment(List<ITripChain> tripChains, List<IVehicle> vehicles)
        {
            List<Pair<IVehicleType[], double>> AllAssignments = new List<Pair<IVehicleType[], double>>();
            //finds all the possible assignments of vehicles to trips
            FindPossibleAssignments( AllAssignments, tripChains, vehicles, 0, new List<UnAssignedModeSet>(), new IVehicleType[tripChains.Count], 0 );
            double maxU = double.MinValue;
            IVehicleType[] bestAssignment = null;
            foreach ( Pair<IVehicleType[], double> assignment in AllAssignments )
            {
                if ( assignment.Second > maxU )
                {
                    maxU = assignment.Second;
                    bestAssignment = assignment.First;
                }
            }
            return bestAssignment;
        }

        public static void FindPossibleAssignments(List<Pair<IVehicleType[], double>> possibleAssignments,
                                                            List<ITripChain> chains,
                                                            List<IVehicle> vehicles,
                                                            int position,
                                                            List<UnAssignedModeSet> currentChains,
                                                            IVehicleType[] currentAssignment,
                                                            double currentU)
        {
            ModeSet[] sets = (ModeSet[])chains[position]["BestForVehicle"];
            if ( sets == null )
            {
                return;
            }
            var numberOfVehicleTypes = ModeChoice.VehicleTypes.Count + 1;
            for ( int j = 0; j < numberOfVehicleTypes; j++ )//take into account NPV
            {
                ModeSet set = sets[j];
                //if this vehicle cannot be used for this set skip it since we are already considering NPV
                if ( ( set != null ) && ( set.ChosenMode != null ) )
                {
                    //a Personal vehicle mode
                    if ( j > 0 )
                    {
                        UnAssignedModeSet newUms = new UnAssignedModeSet( chains[position], set, set.ChosenMode[0].RequiresVehicle );
                        List<UnAssignedModeSet> allChainsWithSameVehicle = currentChains.FindAll( n => n.VehicleType == newUms.VehicleType );
                        List<ITripChain> sameVehicleChains = new List<ITripChain>();
                        foreach ( var ums in allChainsWithSameVehicle )
                        {
                            sameVehicleChains.Add( ums.GetChainWithAssignedModes() );
                        }
                        int count = vehicles.Count( n => n.VehicleType.Equals( newUms.VehicleType ) );
                        var availabilities = HouseholdExtender.FindVehicleAvailabilites( sameVehicleChains, count );
                        foreach ( var ums in allChainsWithSameVehicle )
                        {
                            ums.ResetChain();
                        }
                        ITripChain curChain = newUms.GetChainWithAssignedModes();
                        var span = new TashaTimeSpan( curChain.StartTime, curChain.EndTime );
                        newUms.ResetChain();
                        if ( HouseholdExtender.VehicleAvailableInTimeSpan( availabilities, span, count ) )
                        {
                            currentAssignment[position] = newUms.VehicleType;
                            currentChains.Add( newUms );
                        }
                        else
                        {
                            continue;
                        }
                    }
                    double newU = currentU + set.U;
                    if ( position + 1 == chains.Count )
                    {   //end of the line
                        IVehicleType[] nextAssignment = new IVehicleType[chains.Count];
                        currentAssignment.CopyTo( nextAssignment, 0 );
                        possibleAssignments.Add( new Pair<IVehicleType[], double>( nextAssignment, newU ) );
                    }
                    else
                    {
                        IVehicleType[] nextAssignment = new IVehicleType[chains.Count];
                        currentAssignment.CopyTo( nextAssignment, 0 );
                        FindPossibleAssignments( possibleAssignments, chains, vehicles, position + 1, new List<UnAssignedModeSet>( currentChains ), nextAssignment, newU );
                    }
                }
            }
        }

        /// <summary>
        /// Determines the occurance of a rideshares within a household
        /// ie: drop off and pick up (passengers).
        /// </summary>
        /// <param name="household"></param>
        public static void MultiPersonMode(this ITashaHousehold household)
        {
            //Assign Joint Tours for rideshare
            AssignJointTours( household );
            AssignFeasibility( household );
            //passenger algorithm traverses through all possible auxiliary trips and assigns the most desirable one
            InitializePassengerAlgo();
            PassAlgo.AssignPassengerTrips( household );
        }

        /// <summary>
        /// Resolve the scheduling conflicts for the household
        /// </summary>
        /// <param name="household">The household to resolve</param>
        public static ModeAssignmentHouseHold ResolveConflicts(this ITashaHousehold household)
        {
            if ( !TestForNullModeSet( household ) )
            {
                return ModeAssignmentHouseHold.NULL_SET;
            }
            if ( TestForSimpleCase( household ) )//more vehicles than people
            {
                // do what we need to do to assign vehicles to everyone
                //DoAssignment(household);
                return ModeAssignmentHouseHold.SIMPLE_CASE;
            }
            if ( TestForAdvancedCase( household ) ) //more vehicles than overlaps
            {
                //DoAssignment(household);
                return ModeAssignmentHouseHold.SIMPLE_CASE;
            }
            // resort to the complex case
            IVehicleType[] bestAssignment = FindBestPossibleAssignment( household.AllTripChains(), new List<IVehicle>( household.Vehicles ) );
            if ( bestAssignment == null )
            {
                return ModeAssignmentHouseHold.NULL_SET;
            }
            return ModeAssignmentHouseHold.ADVANCED_CASE;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="household"></param>
        /// <returns></returns>
        public static bool TestForNullModeSet(ITashaHousehold household)
        {
            var numberOfVehicleTypesPlusNon = ModeChoice.VehicleTypes.Count + 1;
            foreach ( var person in household.Persons )
            {
                foreach ( var tripchain in person.TripChains )
                {
                    ModeSet[] sets = (ModeSet[])tripchain["BestForVehicle"];
                    if ( sets.Count( n => ( n == null ) ) == numberOfVehicleTypesPlusNon )
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Goes through the data and regenerates the random components for utility
        /// </summary>
        /// <param name="household"></param>
        public static void UpdateUtilities(this ITashaHousehold household)
        {
            foreach ( var p in household.Persons )
            {
                foreach ( var chain in p.TripChains )
                {
                    foreach ( var trip in chain.Trips )
                    {
                        ModeData.Get( trip ).GenerateError();
                    }
                    foreach ( var modeSet in ModeSet.GetModeSets( chain ) )
                    {
                        modeSet.RecalculateU();
                    }
                    chain.SelectBestPerVehicleType();
                }
            }
        }

        private static void AssignFeasibility(ITashaHousehold household)
        {
            //loop through each trip in the household and assign all possible auxiliary trips
            var modes = TashaRuntime.SharedModes;
            var nonSharedModes = TashaRuntime.NonSharedModes.Count;
            var modesLength = modes.Count;
            // clear out all of the aux trip chains to begin with
            var persons = household.Persons;
            for ( int i = 0; i < persons.Length; i++ )
            {
                persons[i].AuxTripChains.Clear();
            }
            for ( int i = 0; i < persons.Length; i++ )
            {
                var tripChains = persons[i].TripChains;
                for ( int j = 0; j < tripChains.Count; j++ )
                {
                    var trips = tripChains[j].Trips;
                    for ( int k = 0; k < trips.Count; k++ )
                    {
                        ModeData md = ModeData.Get( trips[k] ); //get the mode data saved on the object
                        for ( int l = 0; l < modesLength; l++ )
                        {
                            if ( !( md.Feasible[l + nonSharedModes] = modes[l].Feasible( trips[k] ) ) )
                            {
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Finds the best non-personal vehicle mode set for a tour, returns false otherwise
        /// </summary>
        /// <param name="tour"></param>
        /// <param name="bestSet"></param>
        /// <param name="U"></param>
        /// <returns></returns>
        private static bool BestNonVehicleModeSetForTour(List<ITripChain> tour, out IList<ITashaMode> bestSet, out double U)
        {
            bestSet = null;
            U = Double.MinValue;
            List<List<ModeSet>> ModeSets = new List<List<ModeSet>>();
            foreach ( var chain in tour )
            {
                ModeSets.Add( new List<ModeSet>( ModeSet.GetModeSets( chain ) ) );
            }
            Dictionary<ModeSet, double> setAndU = new Dictionary<ModeSet, double>();
            foreach ( var set in ModeSets[0] )
            {
                double curU = set.U;
                bool existsInAllSets = true;
                for ( int i = 1; i < ModeSets.Count; i++ )
                {
                    bool exists = false;
                    foreach ( var nextSet in ModeSets[i] )
                    {
                        if ( SameChosenModes( set, nextSet ) )
                        {
                            exists = true;
                            curU += nextSet.U;
                            break;
                        }
                    }
                    if ( !exists )
                    {
                        existsInAllSets = false;
                        break;
                    }
                }
                if ( existsInAllSets )
                {
                    setAndU.Add( set, curU );
                }
            }
            if ( setAndU.Count == 0 )
            {
                return false;
            }
            foreach ( var element in setAndU )
            {
                IList<ITashaMode> chosen = element.Key.ChosenMode;
                double u = element.Value;

                if ( u > U )
                {
                    bestSet = chosen;
                    U = u;
                }
            }
            return true;
        }

        ///  <summary>
        ///  Are their less vehicles than the amount of vehicles needed by the trips in the household at one point
        ///  in the day?
        /// 
        ///  Using Marzullo's algorithm
        /// 
        ///  </summary>
        ///  <param name="tripChains">The trip chains of the household</param>
        ///  <param name="numVehicles">The number of this vehicle type the household has available</param>
        /// <param name="bestForVehicle"></param>
        /// <returns></returns>
        private static bool Conflict(List<ITripChain> tripChains, int numVehicles, int bestForVehicle)
        {
            List<Pair<Time, int>> tripIntervals = new List<Pair<Time, int>>( tripChains.Count() * 2 );
            foreach ( ITripChain tripChain in tripChains )
            {
                //add start time to list
                ModeSet[] sets = (ModeSet[])tripChain["BestForVehicle"];
                ModeSet set = sets[bestForVehicle];
                Time travelTime = set.ChosenMode[0].TravelTime( tripChain.Trips[0].OriginalZone, tripChain.Trips[0].DestinationZone, tripChain.Trips[0].ActivityStartTime );
                tripIntervals.Add(new Pair<Time, int>(tripChain.Trips[0].ActivityStartTime - travelTime, 1));
                //add end time to list
                tripIntervals.Add(new Pair<Time, int>(tripChain.EndTime, -1));
            }
            //sort based on times
            tripIntervals.Sort( delegate(Pair<Time, int> p1, Pair<Time, int> p2)
            {
                var first = p1.First;
                var second = p2.First;
                if ( first < second )
                {
                    return -1;
                }
                /*else if(first > second)
                {
                    return 1;
                }*/
                return 1;//0;
            } );
            return MaxConflicts( tripIntervals ) > numVehicles;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Reliability", "CA2002:DoNotLockOnObjectsWithWeakIdentity" )]
        private static void InitializePassengerAlgo()
        {
            if ( PassAlgo == null )
            {
                lock ( typeof( ModeChoiceHousehold ) )
                {
                    System.Threading.Thread.MemoryBarrier();
                    if ( PassAlgo == null )
                    {
                        PassAlgo = new PassengerAlgo( TashaRuntime );
                        System.Threading.Thread.MemoryBarrier();
                    }
                }
            }
        }

        /// <summary>
        ///
        /// Find out what is the maximum amount of vehicles needed at one time
        ///
        /// The trips have been sorted based on time
        /// The end point of a trip is marked as -1
        /// The start point of a trip is marked as 1
        /// Simply go through the sorted list add 1 when a start is encounter and -1 when a finish time
        /// is encountered.
        /// </summary>
        /// <param name="trips">a list of all the trips start and end times</param>
        /// <returns>the maximum number of conflicts</returns>
        private static int MaxConflicts(List<Pair<Time, int>> trips)
        {
            int MaxConflicts = 0;
            int curConflicts = 0;
            foreach ( Pair<Time, int> trip in trips )
            {
                curConflicts += trip.Second;
                if ( curConflicts > MaxConflicts )
                {
                    MaxConflicts = curConflicts;
                }
            }
            return MaxConflicts;
        }

        private static bool SameChosenModes(ModeSet A, ModeSet B)
        {
            var aLength = A.ChosenMode.Length;
            if ( aLength != B.ChosenMode.Length )
            {
                return false;
            }
            for ( int i = 0; i < aLength; i++ )
            {
                if ( A.ChosenMode[i].NonPersonalVehicle == false )
                    return false;

                if ( A.ChosenMode[i] != B.ChosenMode[i] )
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Sets he given modes to each chain in the provided list
        ///
        /// Note: all trip chains must be the same size as the modes
        /// </summary>
        /// <param name="chains"></param>
        /// <param name="modes"></param>
        private static void SetModes(IList<ITripChain> chains, IList<ITashaMode> modes)
        {
            foreach ( var tripChain in chains )
            {
                for ( int i = 0; i < tripChain.Trips.Count; i++ )
                {
                    tripChain.Trips[i].Mode = modes[i];
                }
            }
        }

        /// <summary>
        /// Handle case where we check if there are more conflicting schedules than vehicles for all the trips
        /// in the household
        /// </summary>
        /// <param name="household">The household</param>`
        private static bool TestForAdvancedCase(ITashaHousehold household)
        {
            //a list of tripchains for each vehicle type
            List<ITripChain>[] optimalSets = TripChainsForVehicle( household.AllTripChains() );
            var vehicles = household.Vehicles;
            // if there are no vehicles then we don't need to test for the case
            if (vehicles.Length == 0 )
            {
                return false;
            }
            var optimalSetsLength = optimalSets.Length;
            for ( int i = 0; i < optimalSetsLength; i++ )
            {
                //check if there is a conflict st the vehicle is required at more than one trip chain
                //at the same point in the day
                if ( !ModeChoice.VehicleTypes[i].Finite )
                {
                    continue;
                }
                var ammount = 0;
                for ( int j = 0; j < vehicles.Length; j++ )
                {
                    if ( vehicles[j].VehicleType == ModeChoice.VehicleTypes[i] )
                    {
                        ammount++;
                    }
                }
                if ( Conflict( optimalSets[i], ammount, i + 1 ) )
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Tests to see if there are enough vehicles for everyone
        /// No matter who choses what.
        /// </summary>
        /// <param name="household">The household to test</param>
        /// <returns>If the assignment is trivial</returns>
        private static bool TestForSimpleCase(ITashaHousehold household)
        {
            var vehicles = household.Vehicles;
            var persons = household.Persons;
            var vehicleTypes = ModeChoice.VehicleTypes;
            for ( int j = 0; j < vehicleTypes.Count; j++ )
            {
                var v = vehicleTypes[j];
                if ( !v.Finite )
                {
                    continue;
                }
                int available = 0;
                for ( int i = 0; i < vehicles.Length; i++ )
                {
                    if ( vehicles[i].VehicleType == v )
                    {
                        available++;
                    }
                }

                var users = 0;
                for ( int i = 0; i < persons.Length; i++ )
                {
                    if ( v.CanUse( persons[i] ) )
                    {
                        users++;
                    }
                }
                // if we have more possible users than this resource type then we need to schedule them properly
                if ( available < users )
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// - Goes through all the tripchains in household.
        /// - Finds the optimal mode of transportation for that tripchain.
        /// - if it requires a vehicle, add it to that vehicles trip chain list
        /// </summary>
        /// <param name="TripChains"></param>
        /// <returns>a tripchain list for each vehicle</returns>
        private static List<ITripChain>[] TripChainsForVehicle(List<ITripChain> TripChains)
        {
            //best mode of choice for each trip in set
            List<ITripChain>[] optimalSets = new List<ITripChain>[ModeChoice.VehicleTypes.Count];
            for (int i = 0; i < optimalSets.Length; i++)
            {
                optimalSets[i] = new List<ITripChain>();
            }
            foreach ( ITripChain tripchain in TripChains )
            {
                int BestForVehicle;
                //if it does require a vehicle
                if ( ( BestForVehicle = BestVehicle( (ModeSet[])tripchain["BestForVehicle"] ) ) > 0 )
                {
                    //add this trip chain to its associated vehicle list
                    optimalSets[--BestForVehicle].Add( tripchain );
                }
            }
            return optimalSets;
        }
    }
}