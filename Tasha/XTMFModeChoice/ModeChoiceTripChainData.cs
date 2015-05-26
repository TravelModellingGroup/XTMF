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
using Tasha.Common;
using XTMF;

namespace Tasha.XTMFModeChoice
{
    public sealed class ModeChoiceTripChainData
    {
        /// <summary>
        /// The 0th entry is no vehicle, (1->N are Vehicle Type[i - 1])
        /// </summary>
        public PossibleTripChainSolution[] BestPossibleAssignmentForVehicleType;

        public ModeChoiceTripChainData(ITripChain tripChain, int numberOfModes, int numberOfVehicleTypes)
        {
            var trips = tripChain.Trips;
            TripChain = tripChain;
            BestPossibleAssignmentForVehicleType = new PossibleTripChainSolution[numberOfVehicleTypes];
            var tripData = new ModeChoiceTripData[trips.Count];
            PossibleAssignments = new List<PossibleTripChainSolution>((numberOfModes * trips.Count) >> 1);
            for(int i = 0; i < tripData.Length; i++)
            {
                tripData[i] = new ModeChoiceTripData(numberOfModes);
            }
            TripData = tripData;
        }

        public List<PossibleTripChainSolution> PossibleAssignments;

        public ITripChain TripChain;

        public ModeChoiceTripData[] TripData;

        public void GenerateRandomTerms(Random rand, float[] varianceScale)
        {
            var tripData = TripData;
            for(int i = 0; i < tripData.Length; i++)
            {
                var tce = tripData[i].Error;
                for(int j = 0; j < varianceScale.Length; j++)
                {
                    tce[j] = (float)GenerateRandomNumber(rand) * varianceScale[j];
                }
            }
            var possibleAssignments = PossibleAssignments;
            for(int i = 0; i < possibleAssignments.Count; i++)
            {
                possibleAssignments[i].RegenerateU();
            }
        }

        public bool Pass1(ITashaMode[] modes)
        {
            var trips = TripChain.Trips;
            var tripData = TripData;
            for(int i = 0; i < tripData.Length; i++)
            {
                bool anyModeFeasible = false;
                for(int j = 0; j < modes.Length; j++)
                {
                    // go through each non shared mode and if it is feasible get the V for that mode
                    if((tripData[i].Feasible[j] = modes[j].Feasible(trips[i])))
                    {
                        var value = (float)modes[j].CalculateV(trips[i]);
                        if(!(float.IsNaN(value) | float.IsInfinity(value)))
                        {
                            tripData[i].V[j] = value;
                            anyModeFeasible = true;
                        }
                        else
                        {
                            tripData[i].V[j] = float.NegativeInfinity;
                            tripData[i].Feasible[j] = false;
                        }
                    }
                    else
                    {
                        tripData[i].V[j] = float.NegativeInfinity;
                    }
                }
                if(!anyModeFeasible)
                {
                    return false;
                }
            }
            ComputePossibleAssignments(modes);
            return PossibleAssignments.Count > 0;
        }

        internal void Assign(int useVehicle, List<ITashaMode> modes)
        {
            var trips = TripChain.Trips;
            BestPossibleAssignmentForVehicleType[useVehicle].PickSolution(TripChain);
            for(int i = 0; i < TripData.Length; i++)
            {
                trips[i].Mode = modes[BestPossibleAssignmentForVehicleType[useVehicle].PickedModes[i]];
            }
        }

        internal void FinalAssignment(int householdIteration)
        {
            var trips = TripChain.Trips;
            if(TripChain.JointTrip && !TripChain.JointTripRep)
            {
                var otherTripChain = TripChain.GetRepTripChain.Trips;
                for(int i = 0; i < TripData.Length; i++)
                {
                    trips[i].Mode = otherTripChain[i].Mode;
                    trips[i].ModesChosen[householdIteration] = otherTripChain[i].Mode;
                }

            }
            else
            {
                for(int i = 0; i < TripData.Length; i++)
                {
                    trips[i].ModesChosen[householdIteration] = (trips[i].Mode);
                }
            }
        }

        internal void SelectBestPerVehicleType(List<ITashaMode> modes, List<IVehicleType> vehicleTypes)
        {
            for(int i = 0; i < BestPossibleAssignmentForVehicleType.Length; i++)
            {
                BestPossibleAssignmentForVehicleType[i] = null;
            }

            for(int i = 0; i < PossibleAssignments.Count; i++)
            {
                var assignment = PossibleAssignments[i];
                int vehicleType = IndexOf(modes[assignment.PickedModes[0]].RequiresVehicle, vehicleTypes);
                var otherU = BestPossibleAssignmentForVehicleType[vehicleType + 1] != null ? BestPossibleAssignmentForVehicleType[vehicleType + 1].U : float.NegativeInfinity;
                if(assignment.U > otherU)
                {
                    BestPossibleAssignmentForVehicleType[vehicleType + 1] = assignment;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        /// <summary>
        /// 
        /// Testing an open source method found at
        /// http://gsl.sourcearchive.com/documentation/1.14plus-pdfsg-1/randist_2gauss_8c-source.html
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        static double GenerateRandomNumber(Random r)
        {
            /* Ratio method (Kinderman-Monahan); see Knuth v2, 3rd ed, p130.
             * K+M, ACM Trans Math Software 3 (1977) 257-260.
             *
             * [Added by Charles Karney] This is an implementation of Leva's
             * modifications to the original K+M method; see:
             * J. L. Leva, ACM Trans Math Software 18 (1992) 449-453 and 454-455. */
            double u, v, x, y, Q;
            const double s = 0.449871;    /* Constants from Leva */
            const double t = -0.386595;
            const double a = 0.19600;
            const double b = 0.25472;
            const double r1 = 0.27597;
            const double r2 = 0.27846;

            do                            /* This loop is executed 1.369 times on average  */
            {
                /* Generate a point P = (u, v) uniform in a rectangle enclosing
                   the K+M region v^2 <= - 4 u^2 log(u). */

                /* u in (0, 1] to avoid singularity at u = 0 */
                u = 1 - r.NextDouble();

                /* v is in the asymmetric interval [-0.5, 0.5).  However v = -0.5
                   is rejected in the last part of the while clause.  The
                   resulting normal deviate is strictly symmetric about 0
                   (provided that v is symmetric once v = -0.5 is excluded). */
                v = r.NextDouble() - 0.5;

                /* Constant 1.7156 > sqrt(8/e) (for accuracy); but not by too
                   much (for efficiency). */
                v *= 1.7156;

                /* Compute Leva's quadratic form Q */
                x = u - s;
                y = Math.Abs(v) - t;
                Q = x * x + y * (a * y - b * x);

                /* Accept P if Q < r1 (Leva) */
                /* Reject P if Q > r2 (Leva) */
                /* Accept if v^2 <= -4 u^2 log(u) (K+M) */
                /* This final test is executed 0.012 times on average. */
            }
            while(Q >= r1 && (Q > r2 || v * v > -4 * u * u * Math.Log(u)));

            return (v / u);       /* Return slope */
        }

        [ThreadStatic]
        private static int[] PossibleSolution;

        [ThreadStatic]
        private static ITourDependentMode[] TourDependentModes;

        private void ComputePossibleAssignments(ITashaMode[] modes)
        {
            var possibleAssignments = PossibleAssignments;
            var topLevel = TripData.Length - 1;
            int level = 0;
            int mode = 0;
            var trips = TripChain.Trips;
            int chainLength = trips.Count;
            ITrip currentTrip = trips[0];
            int[] possibleSolution;
            ITourDependentMode[] tourDependentModes;
            if((possibleSolution = PossibleSolution) == null || possibleSolution.Length < chainLength)
            {
                PossibleSolution = possibleSolution = new int[chainLength];
            }
            if((tourDependentModes = TourDependentModes) == null)
            {
                tourDependentModes = TourDependentModes = new ITourDependentMode[modes.Length];
                for(int i = 0; i < tourDependentModes.Length; i++)
                {
                    tourDependentModes[i] = modes[i] as ITourDependentMode;
                }
            }
            while(level != -1)
            {
                for(; mode < modes.Length; mode++)
                {
                    // For each feasible mode
                    var currentData = TripData[level];
                    if(currentData.Feasible[mode])
                    {
                        // find the total utility
                        // store the mode into our set and chain
                        currentTrip.Mode = modes[mode];
                        possibleSolution[level] = mode;
                        // if we are at the end, store the set
                        if(level >= topLevel)
                        {
                            bool feasible = true;
                            TourData tourData = null;
                            for(int i = 0; i < chainLength; i++)
                            {
                                if(tourDependentModes[possibleSolution[i]] != null)
                                {
                                    float tourUtility;
                                    Action<ITripChain> onSelection;
                                    if(tourDependentModes[possibleSolution[i]].CalculateTourDependentUtility(TripChain, i, out tourUtility, out onSelection))
                                    {
                                        if(tourData == null)
                                        {
                                            tourData = new TourData(new float[chainLength], new Action<ITripChain>[chainLength]);
                                        }
                                        tourData.TourUtilityModifiers[i] = tourUtility;
                                        tourData.OnSolution[i] = onSelection;
                                    }
                                    else
                                    {
                                        feasible = false;
                                        break;
                                    }
                                }
                            }
                            // make sure this chain is allowed
                            for(int j = 0; j < modes.Length; j++)
                            {
                                // if this doesn't work don't save it
                                if(!modes[j].Feasible(TripChain))
                                {
                                    feasible = false;
                                    break;
                                }
                            }
                            if(feasible)
                            {
                                possibleAssignments.Add(new PossibleTripChainSolution(TripData, possibleSolution, tourData));
                            }
                        }
                        else
                        {
                            // otherwise go to the next trip
                            level++;
                            currentTrip = trips[level];
                            mode = -1;
                            continue;
                        }
                    }
                }
                level--;
                if(level >= 0)
                {
                    mode = possibleSolution[level] + 1;
                    currentTrip = trips[level];
                }
            }
        }

        private int IndexOf<T>(T iVehicleType, List<T> vehicleTypes) where T : class
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
    }
}