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

namespace Tasha.XTMFModeChoice;

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
        PossibleAssignments = [];
        for (int i = 0; i < tripData.Length; i++)
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
        for (int i = 0; i < tripData.Length; i++)
        {
            var tce = tripData[i].Error;
            for (int j = 0; j < varianceScale.Length; j++)
            {
                tce[j] = (float)TMG.Functions.RandomNumberHelper.SampleNormalDistribution(rand) * varianceScale[j];
            }
        }
        var possibleAssignments = PossibleAssignments;
        for (int i = 0; i < possibleAssignments.Count; i++)
        {
            possibleAssignments[i].RegenerateU();
        }
    }

    public bool Pass1(ITashaMode[] modes)
    {
        var trips = TripChain.Trips;
        var tripData = TripData;
        for (int i = 0; i < tripData.Length; i++)
        {
            bool anyModeFeasible = false;
            ModeChoiceTripData currentTrip = tripData[i];
            for (int j = 0; j < modes.Length; j++)
            {
                // go through each non shared mode and if it is feasible get the V for that mode
                currentTrip.Feasible[j] = modes[j].Feasible(trips[i]);
                if (currentTrip.Feasible[j])
                {
                    var value = (float)modes[j].CalculateV(trips[i]);
                    if (!(float.IsNaN(value) | float.IsInfinity(value)))
                    {
                        currentTrip.V[j] = value;
                        anyModeFeasible = true;
                    }
                    else
                    {
                        currentTrip.V[j] = float.NegativeInfinity;
                        currentTrip.Feasible[j] = false;
                    }
                }
                else
                {
                    currentTrip.V[j] = float.NegativeInfinity;
                }
            }
            if (!anyModeFeasible)
            {
                return false;
            }
        }
        ComputePossibleAssignments(modes);
        return PossibleAssignments.Count > 0;
    }

    internal void Assign(Random random, int useVehicle, ITashaMode[] modes)
    {
        var trips = TripChain.Trips;
        BestPossibleAssignmentForVehicleType[useVehicle].PickSolution(random, TripChain);
        for (int i = 0; i < TripData.Length; i++)
        {
            trips[i].Mode = modes[BestPossibleAssignmentForVehicleType[useVehicle].PickedModes[i]];
        }
    }

    internal void FinalAssignment(int householdIteration, ITashaMode autoMode, ITashaMode rideshare, bool applyRideshareBug)
    {
        var trips = TripChain.Trips;
        if (TripChain.JointTrip && !TripChain.JointTripRep)
        {
            var repTripChain = TripChain.GetRepTripChain.Trips;
            for (int i = 0; i < TripData.Length; i++)
            {
                var repMode = repTripChain[i].Mode;
                var assignedMode = repMode == autoMode ? rideshare : repMode;
                trips[i].Mode = assignedMode;
                trips[i].ModesChosen[householdIteration] = assignedMode;
            }

        }
        else if (applyRideshareBug && TripChain.JointTrip)
        {
            for (int i = 0; i < TripData.Length; i++)
            {
                var mode = trips[i].Mode;
                mode = mode == autoMode ? rideshare : mode;
                trips[i].ModesChosen[householdIteration] = mode;
            }

        }
        else
        {
            for (int i = 0; i < TripData.Length; i++)
            {
                trips[i].ModesChosen[householdIteration] = (trips[i].Mode);
            }
        }
    }

    internal void SelectBestPerVehicleType(ITashaMode[] modes, IVehicleType[] vehicleTypes)
    {
        for (int i = 0; i < BestPossibleAssignmentForVehicleType.Length; i++)
        {
            BestPossibleAssignmentForVehicleType[i] = null;
        }
        if (vehicleTypes.Length == 1)
        {
            for (int i = 0; i < PossibleAssignments.Count; i++)
            {
                var assignment = PossibleAssignments[i];
                if (modes[assignment.PickedModes[0]].RequiresVehicle != null)
                {
                    var otherU = BestPossibleAssignmentForVehicleType[1] != null ? BestPossibleAssignmentForVehicleType[1].U : float.NegativeInfinity;
                    if (assignment.U > otherU)
                    {
                        BestPossibleAssignmentForVehicleType[1] = assignment;
                    }
                }
                else
                {
                    var otherU = BestPossibleAssignmentForVehicleType[0] != null ? BestPossibleAssignmentForVehicleType[0].U : float.NegativeInfinity;
                    if (assignment.U > otherU)
                    {
                        BestPossibleAssignmentForVehicleType[0] = assignment;
                    }
                }
            }
        }
        else
        {
            SelectBestPerVehicleTypeMultipleVehicles(modes, vehicleTypes);
        }
    }

    private void SelectBestPerVehicleTypeMultipleVehicles(ITashaMode[] modes, IVehicleType[] vehicleTypes)
    {
        for (int i = 0; i < PossibleAssignments.Count; i++)
        {
            var assignment = PossibleAssignments[i];
            int vehicleType = Array.IndexOf(vehicleTypes, modes[assignment.PickedModes[0]].RequiresVehicle);
            var otherU = BestPossibleAssignmentForVehicleType[vehicleType + 1] != null ? BestPossibleAssignmentForVehicleType[vehicleType + 1].U : float.NegativeInfinity;
            if (assignment.U > otherU)
            {
                BestPossibleAssignmentForVehicleType[vehicleType + 1] = assignment;
            }
        }
    }

    private void ComputePossibleAssignments(ITashaMode[] modes)
    {
        var possibleAssignments = PossibleAssignments;
        var topLevel = TripData.Length - 1;
        int level = 0;
        int mode = 0;
        var trips = TripChain.Trips;
        int chainLength = trips.Count;
        ITrip currentTrip = trips[0];
        byte[] possibleSolution = new byte[chainLength];
        ITourDependentMode[] tourDependentModes;
        tourDependentModes = new ITourDependentMode[modes.Length];
        PossibleAssignments.Capacity = ComputeEstimatedFeasibleCombinations();
        for (int i = 0; i < tourDependentModes.Length; i++)
        {
            tourDependentModes[i] = modes[i] as ITourDependentMode;
        }
        while (level != -1)
        {
            for (; mode < modes.Length; mode++)
            {
                // For each feasible mode
                var currentData = TripData[level];
                if (currentData.Feasible[mode])
                {
                    // find the total utility
                    // store the mode into our set and chain
                    currentTrip.Mode = modes[mode];
                    possibleSolution[level] = (byte)mode;
                    // if we are at the end, store the set
                    if (level >= topLevel)
                    {
                        bool feasible = true;
                        TourData tourData = null;
                        // make sure this chain is allowed
                        for (int j = 0; j < modes.Length; j++)
                        {
                            // if this doesn't work don't save it
                            if (!modes[j].Feasible(TripChain))
                            {
                                feasible = false;
                                break;
                            }
                        }
                        // if the modes think it is allowed calculate the tour level data
                        if (feasible)
                        {
                            for (int i = 0; i < chainLength; i++)
                            {
                                if (tourDependentModes[possibleSolution[i]] != null)
                                {
                                    if (tourDependentModes[possibleSolution[i]].CalculateTourDependentUtility(TripChain, i, out float tourUtility, out Action<Random, ITripChain> onSelection))
                                    {

                                        if (tourData == null)
                                        {
                                            tourData = new TourData(tourUtility, new Action<Random, ITripChain>[chainLength]);
                                        }
                                        else
                                        {
                                            tourData.TourUtilityModifiers += tourUtility;
                                        }
                                        tourData.OnSolution[i] = onSelection;
                                    }
                                    else
                                    {
                                        feasible = false;
                                        break;
                                    }
                                }
                            }
                        }
                        // if the tour level data thinks it is allowed, then it works and we can add it
                        if (feasible)
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
                    }
                }
            }
            level--;
            if (level >= 0)
            {
                mode = possibleSolution[level] + 1;
                currentTrip = trips[level];
            }
        }
    }

    private int ComputeEstimatedFeasibleCombinations()
    {
        return 100;
    }
}