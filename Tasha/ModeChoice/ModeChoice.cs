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
    public class ModeChoice : ITashaModeChoice
    {
        [RunParameter("Household Iterations", 1, "The number of iterations for a given household.")]
        public int HouseholdIterations;

        [RunParameter("Random Seed", 12345, "The random seed to use for creating a normal distribution")]
        public int RandomSeed;

        [RootModule]
        public ITashaRuntime TashaRuntime;

        /// <summary>
        /// A Random number generator
        /// for the given thread
        /// </summary>
        [ThreadStatic]
        private static Random Rand;

        [DoNotAutomate]
        public List<ITashaMode> AllModes
        {
            get { return TashaRuntime.AllModes; }
        }

        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// All of the modes we can chose from
        /// that are not shared
        /// </summary>
        [DoNotAutomate]
        public List<ITashaMode> NonSharedModes
        {
            get { return TashaRuntime.NonSharedModes; }
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

        /// <summary>
        /// A list of shared modes
        /// </summary>
        [DoNotAutomate]
        public List<ISharedMode> SharedModes
        {
            get { return TashaRuntime.SharedModes; }
        }

        [DoNotAutomate]
        public List<IVehicleType> VehicleTypes
        {
            get { return TashaRuntime.VehicleTypes; }
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
        /// Generates a standard normal variate (i.e. mean = 0; std dev = 1)
        /// Uses the property of the central limit theorem that the sum of (12) random numbers
        /// between 0 and 1 is normally distributed
        /// see reference: Naylor, Balintfy, Burdick and Chu, 1966.  "Computer Simulation Techniques", p. 95.
        /// </summary>
        /// <returns>-6 to 6, on a normal distrobution</returns>
        public double GetNormal()
        {
            double sum = 0;
            if ( Rand == null )
            {
                Rand = new Random( RandomSeed );
            }
            for ( int i = 0; i < 12; i++ )
            {
                // There is 1 Rand per thread
                sum += Rand.NextDouble();
            }
            sum = ( sum - 6 );
            return sum;
        }

        /// <summary>
        /// Loads the data required by Mode Choice
        /// </summary>
        public void LoadOneTimeLocalData()
        {
            var modes = AllModes;
            var modeLength = modes.Count;
            for ( int i = 0; i < modeLength; i++ )
            {
                var smode = modes[i] as ISharedMode;
                if ( smode != null )
                {
                    smode.ModeChoiceArrIndex = (byte)i;
                }
            }
            ModeChoiceHousehold.TashaRuntime = TashaRuntime;
            HouseholdExtender.TashaRuntime = TashaRuntime;
            ModeSet.ModeChoice = this;
        }

        /// <summary>
        /// Load the data you need to do after each iteration
        /// </summary>
        public void IterationStarting()
        {
            //loads the random numbers
            if ( Rand == null )
            {
                Rand = new Random( TashaRuntime.RandomSeed );
            }
        }

        /// <summary>
        /// Runs the ModeChoice model on the given household
        /// </summary>
        /// <param name="h">The household to run on</param>
        public bool Run(ITashaHousehold h)
        {
            // Compute the error terms and feasibility of non shared modes
            ComputeErrorTerms( h );
            //there are no "trip chains" so return true
            for ( int i = 0; i < HouseholdIterations; i++ )
            {
                // To start with Generate the LogLikelyhoods
                if ( i == 0 )
                {
                    foreach ( var person in h.Persons )
                    {
                        person.CalculateLoglikelihood();
                    }
                }
                else
                {
                    // on ever other one just update the utilities
                    h.UpdateUtilities();
                }

                ModeChoiceHousehold.ModeAssignmentHouseHold result = h.ResolveConflicts();

                //assign the best mode to each trip to prep for pass 3
                if ( result == ModeChoiceHousehold.ModeAssignmentHouseHold.ADVANCED_CASE )
                {
                    List<ITripChain> l = h.AllTripChains();
                    IVehicleType[] vt = ModeChoiceHousehold.FindBestPossibleAssignment( l, new List<IVehicle>( h.Vehicles ) );
                    if ( vt != null )
                    {
                        AssignBestMode( h, vt );
                    }
                    else
                    {
                        // TODO: should assign best possible vehicle even if
                        // someone cannot execute their tripchain since they can
                        // be passengers
                        return false;
                    }
                }
                else if ( result == ModeChoiceHousehold.ModeAssignmentHouseHold.SIMPLE_CASE )
                {
                    AssignBestMode( h );
                }
                else if ( result == ModeChoiceHousehold.ModeAssignmentHouseHold.NULL_SET )
                {
                    ReleaseModeSets( h );
                    return false;
                }

                //pass 3
                h.MultiPersonMode();

                //finally store it to the list of "generated" realities
                DoAssignment( h );
            }
            ReleaseModeSets( h );
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            // Update our helper classes with links back to ourself
            ModeChoiceTrip.TashaRuntime = TashaRuntime;
            ModeChoiceTripChain.TashaRuntime = TashaRuntime;
            ModeChoiceHousehold.ModeChoice = this;
            ModeData.TashaRuntime = TashaRuntime;
            return true;
        }

        /// <summary>
        /// Assigns the best mode to each trip in the trip chain for use in pass 3. (No Conflict Vers.)
        /// </summary>
        /// <param name="household"></param>
        private void AssignBestMode(ITashaHousehold household)
        {
            foreach ( var person in household.Persons )
            {
                foreach ( var tripChain in person.TripChains )
                {
                    ModeSet[] tripset = (ModeSet[])tripChain["BestForVehicle"];
                    ModeSet bestSet;
                    var trips = tripChain.Trips;
                    if ( ( bestSet = tripset[ModeChoiceHousehold.BestVehicle( tripset )] ) != null )
                    {
                        for ( int i = 0; i < bestSet.Length; i++ )
                        {
                            trips[i].Mode = bestSet.ChosenMode[i];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Assigns the best mode to each trip in the trip chain for use in pass 3. (Conflict Vers.)
        /// </summary>
        /// <param name="household">Household to work on</param>
        /// <param name="bestAssignment">The current (after pass 2) best vehicle assignment</param>
        private void AssignBestMode(ITashaHousehold household, IVehicleType[] bestAssignment)
        {
            int i = 0;
            foreach ( var person in household.Persons )
            {
                foreach ( var tripChain in person.TripChains )
                {
                    ModeSet[] sets = (ModeSet[])tripChain["BestForVehicle"];
                    ModeSet set = sets[VehicleTypes.IndexOf( bestAssignment[i++] ) + 1];
                    if ( set != null )
                    {
                        var numberOfTrips = tripChain.Trips.Count;
                        for ( int j = 0; j < numberOfTrips; j++ )
                        {
                            tripChain.Trips[j].Mode = set.ChosenMode[j];
                        }
                    }
                }
            }
        }

        ///  <summary>
        ///  </summary>
        ///  <param name="household"></param>
        private void DoAssignment(ITashaHousehold household)
        {
            foreach ( var person in household.Persons )
            {
                foreach ( var tripChain in person.TripChains )
                {
                    foreach ( var trip in tripChain.Trips )
                    {
                        var mode = trip.Mode;
                        var modesChosen = trip.ModesChosen;
                        for ( int i = 0; i < modesChosen.Length; i++ )
                        {
                            modesChosen[i] = mode;
                        }
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