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

namespace Tasha.ModeChoice
{
    /// <summary>
    /// This class augments ITripChain adding in some extra methods that TASHA's Mode choice can call
    /// </summary>
    public static class ModeChoiceTripChain
    {
        internal static ITashaRuntime TashaRuntime;

        /// <summary>
        /// Calculate the V Values for the chain
        /// </summary>
        /// <param name="chain">The chain to calculate for</param>
        public static bool CalculateV(this ITripChain chain)
        {
            // Figure out what we can do for each trip
            foreach ( var trip in chain.Trips )
            {
                if ( !trip.CalculateVTrip() ) return false;
            }
            return true;
        }

        /// <summary>
        /// Checks to see if this Trip Chain is feasible
        /// </summary>
        /// <param name="chain">The chain to check the feasibility of</param>
        /// <returns>If the trip chain is feasible</returns>
        public static bool Feasible(this ITripChain chain)
        {
            var modes = TashaRuntime.NonSharedModes;
            var modeLengths = modes.Count;
            // make sure the whole chain is allowed
            for ( int j = 0; j < modeLengths; j++ )
            {
                // if this doesn't work don't save it
                if ( !modes[j].Feasible( chain ) )
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Generates all feasible sets of modes for the trip chain
        /// </summary>
        /// <param name="chain">The chain to operate on</param>
        public static void GenerateModeSets(this ITripChain chain)
        {
            //initiates the mode set
            ModeSet.InitModeSets( chain );
            ModeData[] Data = new ModeData[chain.Trips.Count];
            // Generate the random terms
            var trips = chain.Trips;
            for ( int i = 0; i < Data.Length; i++ )
            {
                Data[i] = ModeData.Get( trips[i] );
                if ( Data[i] != null )
                {
                    Data[i].GenerateError();
                }
            }
            ModeSet set = ModeSet.Make( chain );
            // launch the recursive version to explore all sets
            GenerateModeSets( chain, Data, set );

            //clear temp var 'mode' that was used in generate mode set algo
            foreach ( var trip in chain.Trips )
            {
                trip.Mode = null;
            }
        }

        /// <summary>
        /// Calculates and stores the best trip chain for
        /// each type of vehicle (and NPV)
        /// </summary>
        /// <param name="chain">The chain to calculate</param>
        public static void SelectBestPerVehicleType(this ITripChain chain)
        {
            ModeSet[] best = chain["BestForVehicle"] as ModeSet[];
            var sets = ModeSet.GetModeSets( chain );
            if ( best == null )
            {
                best = new ModeSet[TashaRuntime.VehicleTypes.Count + 1];
                chain.Attach( "BestForVehicle", best );
            }
            for ( int i = 0; i < best.Length; i++ )
            {
                best[i] = null;
            }
            foreach ( var set in sets )
            {
                IVehicleType type = null;
                foreach ( var mode in set.ChosenMode )
                {
                    if ( mode.RequiresVehicle != null )
                    {
                        type = mode.RequiresVehicle;
                        break;
                    }
                }
                int index = TashaRuntime.VehicleTypes.IndexOf( type );
                best[index + 1] = ( best[index + 1] == null || best[index + 1].U < set.U ) ? set : best[index + 1];
            }
        }

        /// <summary>
        /// Generates all feasible sets of modes for the trip chain
        /// </summary>
        /// <param name="chain">The chain to operate on</param>
        /// <param name="Data">The ModeData for each trip</param>
        /// <param name="set">The mode set we are building</param>
        /// <param name="level">How deep in the recursion we are</param>
        /// <param name=Fo"U">What is the total Utility value calculated</param>
        private static void GenerateModeSets(ITripChain chain, ModeData[] Data, ModeSet set)
        {
            var modes = TashaRuntime.AllModes;
            var numberOfModes = modes.Count - TashaRuntime.SharedModes.Count;
            var topLevel = Data.Length - 1;
            int level = 0;
            double U = 0;
            int mode = 0;
            List<ModeSet> possibleTripChains = ModeSet.GetModeSets( chain ) as List<ModeSet>;
            Stack<int> previousMode = new Stack<int>( 10 );
            Stack<double> previousU = new Stack<double>( 10 );
            int chainLength = chain.Trips.Count;
            var trips = chain.Trips;
            ITrip currentTrip = trips[0];
            while ( level != -1 )
            {
                for ( ; mode < numberOfModes; mode++ )
                {
                    // For each feasible mode
                    var currentData = Data[level];
                    if ( currentData.Feasible[mode] )
                    {
                        // find the total utility
                        double newU = U + currentData.V[mode] + currentData.Error[mode];
                        // store the mode into our set and chain
                        set.ChosenMode[level] = currentTrip.Mode = modes[mode];
                        // if we are at the end, store the set
                        if ( level >= topLevel )
                        {
                            bool feasible = true;
                            // make sure this chain is allowed
                            for ( int j = 0; j < numberOfModes; j++ )
                            {
                                // if this doesn't work don't save it
                                if ( !modes[j].Feasible( chain ) )
                                {
                                    feasible = false;
                                    break;
                                }
                            }
                            if ( feasible )
                            {
                                possibleTripChains.Add( ModeSet.Make( set, newU ) );
                            }
                        }
                        else
                        {
                            // otherwise go to the next trip
                            level++;
                            previousU.Push( U );
                            U = newU;
                            currentTrip = trips[level];
                            previousMode.Push( mode );
                            mode = -1;
                            continue;
                        }
                    }
                }
                if ( previousMode.Count > 0 )
                {
                    mode = previousMode.Pop() + 1;
                    U = previousU.Pop();
                    currentTrip = trips[level - 1];
                }
                level--;
            }
        }
    }
}