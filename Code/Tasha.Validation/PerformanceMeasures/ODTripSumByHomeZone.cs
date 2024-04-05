﻿/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.IO;
using TMG.Input;
using Tasha.Common;
using XTMF;
using Tasha.XTMFModeChoice;


namespace Tasha.Validation.PerformanceMeasures;

[ModuleInformation(Description = "This module is designed to extract out vehicle kilometer's traveled (VKTs) by home zone for the given modes.")]
public sealed class ODTripSumByHomeZone : IPostHousehold
{
    [SubModelInformation(Required = true, Description = "Where do you want to save the Purpose Results. Must be in .CSV format.")]
    // ReSharper disable once InconsistentNaming
    public FileLocation VKT_Output;

    [SubModelInformation(Required = true, Description = "Which modes do you want to _Count the VKTs for?")]
    public EMME.CreateEmmeBinaryMatrix.ModeLink[] AnalyzeModes;

    [RunParameter("Start Time", "6:00", typeof(Time), "The start time for this scenario")]
    public Time StartTime;
    [RunParameter("End Time", "9:00", typeof(Time), "The end time for this scenario")]
    public Time EndTime;

    List<string> ValidModeNames = [];

    [RootModule]
    public ITashaRuntime Root;


    private ConcurrentDictionary<int, ConcurrentQueue<ODData<float>>> RecordedTrips = new();

    
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
        get { return new Tuple<byte, byte, byte>(120, 25, 100); }
    }

    public void Execute(ITashaHousehold household, int iteration)
    {            
        var houseData = household["ModeChoiceData"] as ModeChoiceHouseholdData;            

        if (iteration == Root.TotalIterations - 1 && houseData != null)
        {
            lock (this)
            {
                var homeZone = household.HomeZone.ZoneNumber;

                if (household.Vehicles.Length > 0)
                {
                    for (int i = 0; i < household.Persons.Length; i++)
                    {
                        for (int j = 0; j < household.Persons[i].TripChains.Count; j++)
                        {
                            var personalExp = household.Persons[i].ExpansionFactor;
                            for (int k = 0; k < household.Persons[i].TripChains[j].Trips.Count; k++)
                            {
                                var trip = household.Persons[i].TripChains[j].Trips[k];
                                var tripMode = trip.Mode;

                                if (trip.ActivityStartTime >= StartTime && trip.ActivityStartTime < EndTime)
                                {
                                    if (ValidModeNames.Contains(tripMode.ModeName))
                                    {
                                        AddData(homeZone, trip.OriginalZone.ZoneNumber, trip.DestinationZone.ZoneNumber, personalExp);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public void AddData(int householdZone, int origin, int destination, float expFactor)
    {
        if (!RecordedTrips.TryGetValue(householdZone, out ConcurrentQueue<ODData<float>> queue))
        {
            lock (RecordedTrips)
            {
                if (!RecordedTrips.TryGetValue(householdZone, out queue))
                {
                    RecordedTrips.TryAdd(householdZone, (queue = new ConcurrentQueue<ODData<float>>()));
                }
            }
        }
        queue.Enqueue(new ODData<float>() { O = origin, D = destination, Data = expFactor });
    }

    public void IterationFinished(int iteration)
    {
        if (iteration == Root.TotalIterations - 1)
        {
            lock (this)
            {
                var writeHeader = !File.Exists(VKT_Output);
                using StreamWriter writer = new(VKT_Output, true);
                if (writeHeader)
                {
                    writer.WriteLine("Home Zone,Origin,Destination,Trips");
                }

                foreach (var homeZone in from key in RecordedTrips.Keys
                                         orderby key
                                         select key)
                {
                    var zonalData = from data in RecordedTrips[homeZone]
                                    orderby data.O, data.D
                                    group data by new { data.O, data.D } into gd
                                    select new
                                    {
                                        gd.Key.O,
                                        gd.Key.D,
                                        SumOfExpandedTrips = gd.Sum(element => element.Data)
                                    };

                    foreach (var zone in zonalData)
                    {
                        writer.WriteLine("{0},{1},{2},{3}", homeZone, zone.O, zone.D, zone.SumOfExpandedTrips);
                    }
                }
            }
            RecordedTrips.Clear();
        }
    }

    public void IterationStarting(int iteration)
    {
        RecordedTrips.Clear();
        for (int i = 0; i < AnalyzeModes.Length; i++)
        {
            ValidModeNames.Add(AnalyzeModes[i].ModeName);
        }            
    }

    public void Load(int maxIterations)
    {

    }

    public bool RuntimeValidation(ref string error)
    {            
        return true;
    }
}
