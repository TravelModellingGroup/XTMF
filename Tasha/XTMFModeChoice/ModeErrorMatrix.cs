/*
    Copyright 2014-2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Tasha.Common;
using TMG.Input;
using XTMF;

namespace Tasha.XTMFModeChoice;

public class ModeErrorMatrix : IPostHousehold
{
    [RunParameter("Compute Fitness", true, "Should we compute the fitness variable as well?")]
    public bool ComputeFitness;

    [RunParameter("FileName", "PredictionTable.csv", "The name of the file to store the prediction matrix in.")]
    public string FileName;

    [RunParameter("Household Iterations", 100, "The number of household iterations that we are expecting.")]
    public int HouseholdIterations;

    SpinLock ObservationsLock = new(false);
    public float[][] Observations;

    [RunParameter("ObservedMode", "ObservedMode", "The name of the observed mode's attribute.")]
    public string ObservedMode;

    [RunParameter("Report Expansion Factors", false, "When exporting the trips use expansion factors?")]
    public bool ReportExpansionFactors;

    [RunParameter("StartTime", "0:00", typeof(Time), "The early bound for when to collect data from.")]
    public Time StartTime;

    [RunParameter("EndTime", "30:00", typeof(Time), "The late bound for when to collect data from.")]
    public Time EndTime;

    [RootModule]
    public ITashaRuntime TashaRuntime;

    [RunParameter("Exlude Intrazonal", false, "Should intrazonal trips be excluded from the confusion matrix?")]
    public bool ExcludeIntrazonal;

    private int[] BadTrips;
    private ConcurrentQueue<BadTripEntry> BadTripsQueue;
    private float Fitness;
    private int MissingTrips;

    [DoNotAutomate]
    private List<ITashaMode> Modes;

    private float ZeroParamFitness;

    private SpinLock FitnessUpdateLock = new(false);

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

    public void Execute(ITashaHousehold household, int iteration)
    {
        var numberOfModes = Modes.Count;
        var numberOfSharedModes = TashaRuntime.SharedModes.Count;
        var householdData = (ModeChoiceHouseholdData) household["ModeChoiceData"];
        double householdFitness = 0.0;
        double zeroFitness = 0.0;
        // Make sure there is data for this household.  This might happen if there is no trips.
        if(householdData == null)
        {
            return;
        }
        for(int personIndex = 0; personIndex < householdData.PersonData.Length; personIndex++)
        {
            var personData = householdData.PersonData[personIndex];
            var expFactor = ReportExpansionFactors ? household.Persons[personIndex].ExpansionFactor : 1.0f;
            for(int tripChainIndex = 0; tripChainIndex < personData.TripChainData.Length; tripChainIndex++)
            {
                var tripChainData = personData.TripChainData[tripChainIndex];
                for(int tripIndex = 0; tripIndex < tripChainData.TripData.Length; tripIndex++)
                {
                    var trip = tripChainData.TripChain.Trips[tripIndex];
                    // Check to see if we should exclude this trip
                    if ((ExcludeIntrazonal && trip.OriginalZone == trip.DestinationZone) || !IsInTimeBound(trip))
                    {
                        continue;
                    }
                    
                    var tripData = tripChainData.TripData[tripIndex];
                    
                    int correct = 0;
                    if(trip.ModesChosen == null)
                    {
                        Interlocked.Add(ref MissingTrips, HouseholdIterations);
                        break;
                    }
                    var hhldIterations = trip.ModesChosen.Length;
                    if(hhldIterations != HouseholdIterations)
                    {
                        Interlocked.Add(ref MissingTrips, HouseholdIterations - hhldIterations);
                    }
                    if(hhldIterations == 0)
                    {
                        break;
                    }

                    var obs = trip[ObservedMode];
                    if(obs != null)
                    {
                        if (obs is ITashaMode obsMode)
                        {
                            // find index
                            var realIndex = Modes.IndexOf(obsMode);
                            if (realIndex >= 0)
                            {
                                var chosenModes = trip.ModesChosen;
                                for (int k = 0; k < chosenModes.Length; k++)
                                {
                                    var predMode = Modes.IndexOf(chosenModes[k]);
                                    if (predMode >= 0)
                                    {
                                        bool taken = false;
                                        ObservationsLock.Enter(ref taken);
                                        Thread.MemoryBarrier();
                                        Observations[realIndex][predMode] += expFactor;
                                        if (taken) ObservationsLock.Exit(true);
                                    }
                                    if (realIndex == predMode)
                                    {
                                        correct++;
                                    }
                                }
                                if (realIndex < numberOfModes - numberOfSharedModes)
                                {
                                    if (!tripData.Feasible[realIndex] & (!tripChainData.TripChain.JointTrip | tripChainData.TripChain.JointTripRep))
                                    {
                                        Interlocked.Increment(ref BadTrips[realIndex]);
                                        BadTripsQueue.Enqueue(new BadTripEntry()
                                        {
                                            HHLD = household.HouseholdId,
                                            PersonID = household.Persons[personIndex].Id,
                                            TripID = trip.TripNumber,
                                            Mode = obsMode.ModeName,
                                            Distance = Math.Abs(trip.OriginalZone.X - trip.DestinationZone.X) + Math.Abs(trip.OriginalZone.Y - trip.DestinationZone.Y),
                                            HasTravelTime = obsMode.TravelTime(trip.OriginalZone, trip.DestinationZone, trip.TripStartTime) > Time.Zero,
                                            OrginZone = trip.OriginalZone.ZoneNumber,
                                            DestZone = trip.DestinationZone.ZoneNumber
                                        });
                                    }
                                }
                            }
                            if (ComputeFitness)
                            {
                                householdFitness += (float)Math.Log((correct + 1f) / (hhldIterations + 1f));
                                int feasibleModes = 1;
                                for (int i = 0; i < tripData.Feasible.Length; i++)
                                {
                                    if (tripData.Feasible[i])
                                    {
                                        feasibleModes++;
                                    }
                                }
                                // now for the shared modes...
                                if (household.Vehicles.Length > 0)
                                {
                                    feasibleModes++;
                                }
                                if (feasibleModes <= 0)
                                {
                                    feasibleModes = numberOfModes - 1;
                                }
                                zeroFitness += (float)Math.Log((((hhldIterations / (float)feasibleModes)) + 1f) / (hhldIterations + 1.0f));
                            }
                        }
                    }
                    else
                    {
                        Interlocked.Add(ref MissingTrips, HouseholdIterations);
                    }
                }
            }
        }
        bool entered = false;
        FitnessUpdateLock.Enter(ref entered);
        Thread.MemoryBarrier();
        Fitness += (float)householdFitness;
        ZeroParamFitness += (float)zeroFitness;
        Thread.MemoryBarrier();
        if(entered) FitnessUpdateLock.Exit(true);
    }

    private bool IsInTimeBound(ITrip trip)
    {
        var activity = trip.Purpose;
        /*
         We need to actually compute this to make sure that the changing of the active mode does not change when this trip
         started compared to the base data.  Home trips are going to change the activity start time, other trips will change
         the trip start time depending on the currently selected mode.  To keep things consistent we will use their activity
         start time.
        */
        var activityTime = activity == Activity.Home ? trip.TripStartTime : trip.ActivityStartTime;
        return StartTime <= activityTime && activityTime < EndTime;
    }

    public void IterationFinished(int iteration)
    {
        var numModes = Modes.Count;
        var correctTotal = 0.0f;
        var columnTotals = new float[numModes];
        var total = 0.0f;
        using(StreamWriter writer = new(FileName))
        {
            // print the header
            writer.Write("Pred\\Real");
            for(int i = 0; i < numModes; i++)
            {
                writer.Write(',');
                writer.Write(Modes[i].ModeName);
            }
            writer.WriteLine(",Row Total");
            // for each row
            for(int j = 0; j < numModes; j++)
            {
                float rowTotal = 0.0f;
                writer.Write(Modes[j].ModeName);
                for(int i = 0; i < numModes; i++)
                {
                    var val = Observations[i][j];
                    writer.Write(',');
                    writer.Write(val);
                    columnTotals[i] += val;
                    rowTotal += val;
                    if(i == j)
                    {
                        correctTotal += val;
                    }
                    total += val;
                }
                writer.Write(',');
                writer.WriteLine(rowTotal);
            }
            writer.Write("Column Total,");
            for(int i = 0; i < numModes; i++)
            {
                writer.Write(columnTotals[i]);
                writer.Write(',');
            }
            writer.WriteLine(correctTotal);

            // NOW COMPUTE THE %
            writer.Write("Pred\\Real%");
            for(int i = 0; i < numModes; i++)
            {
                writer.Write(',');
                writer.Write(Modes[i].ModeName);
            }
            writer.WriteLine(",Row Total");
            // for each row
            for(int j = 0; j < numModes; j++)
            {
                float rowTotal = 0;
                writer.Write(Modes[j].ModeName);
                for(int i = 0; i < numModes; i++)
                {
                    writer.Write(',');
                    writer.Write("{0:0.##}%", 100 * ((Observations[i][j]) / total));
                    rowTotal += Observations[i][j];
                }
                writer.WriteLine(",{0:0.##}%", 100 * (rowTotal / total));
            }
            writer.Write("Column Total,");
            for(int i = 0; i < numModes; i++)
            {
                writer.Write("{0:0.##}%", 100 * (columnTotals[i] / total));
                writer.Write(',');
            }
            writer.WriteLine("{0:0.##}%", 100 * (correctTotal / total));

            if(ComputeFitness)
            {
                writer.Write("Value,");
                writer.WriteLine(Fitness);
                writer.Write("ZeroParam,");
                writer.WriteLine(ZeroParamFitness);
                writer.Write("Rho^2,");
                writer.WriteLine(1 - (Fitness / ZeroParamFitness));
                // 2 lines of blank
                var numberOfModes = Modes.Count;
                writer.WriteLine("\r\n");
                writer.WriteLine("Number of Non-Feasible Trips");
                for(int i = 0; i < numberOfModes; i++)
                {
                    writer.Write(Modes[i].ModeName);
                    writer.Write(',');
                    writer.WriteLine(BadTrips[i]);
                }
                writer.WriteLine("Missing Trips");
                writer.WriteLine(MissingTrips);
                writer.WriteLine("Invaid Trips");
                writer.WriteLine("HHLD,Person,Trip#,Mode,Distance,HasTravelTime,OriginZone,DestZone");
                while(BadTripsQueue.TryDequeue(out BadTripEntry t))
                {
                    writer.Write(t.HHLD);
                    writer.Write(',');
                    writer.Write(t.PersonID);
                    writer.Write(',');
                    writer.Write(t.TripID);
                    writer.Write(',');
                    writer.Write(t.Mode);
                    writer.Write(',');
                    writer.Write(t.Distance);
                    writer.Write(',');
                    writer.Write(t.HasTravelTime);
                    writer.Write(',');
                    writer.Write(t.OrginZone);
                    writer.Write(',');
                    writer.WriteLine(t.DestZone);
                }
            }
        }
        // after we output reset the fitness
        Fitness = 0;
        ZeroParamFitness = 0;
        MissingTrips = 0;
        for(int i = 0; i < BadTrips.Length; i++)
        {
            BadTrips[i] = 0;
        }
        Thread.MemoryBarrier();
    }

    public void Load(int maxIterations)
    {
        // Create the table
        var allModes = Modes = TashaRuntime.AllModes;
        Observations = new float[allModes.Count][];
        for(int i = 0; i < Observations.Length; i++)
        {
            Observations[i] = new float[allModes.Count];
        }
        BadTrips = new int[allModes.Count];
        BadTripsQueue = new ConcurrentQueue<BadTripEntry>();
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void IterationStarting(int iteration)
    {
        ClearTrips();
        Fitness = 0;
        ZeroParamFitness = 0;
        for(int i = 0; i < BadTrips.Length; i++)
        {
            BadTrips[i] = 0;
        }
    }

    private void ClearTrips()
    {
        while (BadTripsQueue.TryDequeue(out BadTripEntry t))
        {
        }
    }

    private struct BadTripEntry
    {
        internal int DestZone;
        internal float Distance;
        internal bool HasTravelTime;
        internal int HHLD;
        internal string Mode;
        internal int OrginZone;
        internal int PersonID;
        internal int TripID;
    }
}