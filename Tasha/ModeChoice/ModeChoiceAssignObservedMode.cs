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

namespace Tasha.ModeChoice
{
    /// <summary>
    /// This is used for running the mode choice model for Tasha#
    /// </summary>
    public class ModeChoiceAssignObservedMode : ITashaModeChoice
    {
        [RunParameter( "Observed Mode", "ObservedMode", "The name of the attribute for the observed mode attached to a trip." )]
        public string ObservedMode;

        [RootModule]
        public ITashaRuntime Root;

        private PassengerAlgo PassAlgo;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public void AssignJointTours(ITashaHousehold household)
        {
            ITashaMode rideShare = null;
            int rideShareIndex = -1;
            var allModes = Root.AllModes;
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
            foreach ( var element in household.JointTours )
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
                 * Now we assign the rideshare mode and if the total utility of everyone using rideshare is less than that
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
        ///
        /// </summary>
        /// <param name="h"></param>
        /// <returns></returns>
        public bool ComputeErrorTerms(ITashaHousehold h)
        {
            bool worked = true;
            foreach ( var person in h.Persons )
            {
                foreach ( var chain in person.TripChains )
                {
                    if ( !chain.CalculateV() )
                    {
                        worked = false;
                    }
                }
            }
            return worked;
        }

        /// <summary>
        /// Loads the data required by Mode Choice
        /// </summary>
        public void LoadOneTimeLocalData()
        {
        }

        /// <summary>
        /// Load the data you need to do after each iteration
        /// </summary>
        public void IterationStarting()
        {
        }

        /// <summary>
        /// Determines the occurance of a rideshares within a household
        /// ie: drop off and pick up (passengers).
        /// </summary>
        /// <param name="household"></param>
        public void MultiPersonMode(ITashaHousehold household)
        {
            //Assign Joint Tours for rideshare
            AssignJointTours( household );
            //AssignFeasibility(household);
            //passenger algorithm traverses through all possible auxiliary trips and assigns the most desirable one
            InitializePassengerAlgo();
            PassAlgo.AssignPassengerTrips( household );
        }

        /// <summary>
        /// Runs the ModeChoice model on the given household
        /// </summary>
        /// <param name="h">The household to run on</param>
        public bool Run(ITashaHousehold h)
        {
            ComputeErrorTerms( h );
            AssignObservedMode( h );

            //pass 3
            MultiPersonMode( h );

            //finally store it to the list of "generated" realities
            DoAssignment( h );

            ReleaseModeSets( h );

            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            ModeChoiceHousehold.TashaRuntime = Root;
            ModeChoiceTripChain.TashaRuntime = Root;
            ModeData.TashaRuntime = Root;
            ModeChoiceTrip.TashaRuntime = Root;
            HouseholdExtender.TashaRuntime = Root;
            return true;
        }

        /// <summary>
        /// Finds the best non-personal vehicle mode set for a tour, returns false otherwise
        /// </summary>
        /// <param name="tour"></param>
        /// <param name="bestSet"></param>
        /// <param name="utility"></param>
        /// <returns></returns>
        private static bool BestNonVehicleModeSetForTour(List<ITripChain> tour, out IList<ITashaMode> bestSet, out double utility)
        {
            bestSet = null;
            utility = Double.MinValue;
            List<List<ModeSet>> modeSets = [];
            foreach ( var chain in tour )
            {
                modeSets.Add( new List<ModeSet>( ModeSet.GetModeSets( chain ) ) );
            }
            Dictionary<ModeSet, double> setAndU = [];
            foreach ( var set in modeSets[0] )
            {
                double curU = set.U;
                bool existsInAllSets = true;
                for ( int i = 1; i < modeSets.Count; i++ )
                {
                    bool exists = false;
                    foreach ( var nextSet in modeSets[i] )
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

                if ( u > utility )
                {
                    bestSet = chosen;
                    utility = u;
                }
            }
            return true;
        }

        private static bool SameChosenModes(ModeSet a, ModeSet b)
        {
            var aLength = a.ChosenMode.Length;
            if ( aLength != b.ChosenMode.Length )
            {
                return false;
            }
            for ( int i = 0; i < aLength; i++ )
            {
                if ( a.ChosenMode[i].NonPersonalVehicle == false )
                    return false;

                if ( a.ChosenMode[i] != b.ChosenMode[i] )
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
        /// Assigns the best mode to each trip in the trip chain for use in pass 3. (No Conflict Vers.)
        /// </summary>
        /// <param name="household"></param>
        private void AssignObservedMode(ITashaHousehold household)
        {
            foreach ( var person in household.Persons )
            {
                foreach ( var tripChain in person.TripChains )
                {
                    var numberOfTrips = tripChain.Trips.Count;
                    for ( int i = 0; i < numberOfTrips; i++ )
                    {
                        tripChain.Trips[i].Mode = tripChain.Trips[i][ObservedMode] as ITashaMode;
                    }
                }
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="household"></param>
        private void DoAssignment(ITashaHousehold household)
        {
            foreach ( var person in household.Persons )
            {
                foreach (var tripChain in person.TripChains)
                {
                    foreach (var trip in tripChain.Trips)
                    {
                        var mode = trip.Mode;
                        var modesChosen = trip.ModesChosen;
                        for (int i = 0; i < modesChosen.Length; i++ )
                        {
                            modesChosen[i] = mode;
                        }
                    }
                }
            }
        }

        private void InitializePassengerAlgo()
        {
            if ( PassAlgo == null )
            {
                lock ( this )
                {
                    System.Threading.Thread.MemoryBarrier();
                    if ( PassAlgo == null )
                    {
                        PassAlgo = new PassengerAlgo( Root );
                        System.Threading.Thread.MemoryBarrier();
                    }
                }
            }
        }

        private void ReleaseModeSets(ITashaHousehold h)
        {
            foreach ( var person in h.Persons )
            {
                foreach ( var tripChain in person.TripChains )
                {
                    ModeSet.ReleaseModeSets( tripChain );
                }
            }
        }

        public void IterationStarted(int iteration, int totalIterations)
        {
            
        }

        public void IterationFinished(int iteration, int totalIterations)
        {
            
        }
    }
}