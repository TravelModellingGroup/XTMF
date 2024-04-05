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
using System.Linq;
using System.IO;
using Tasha.Common;
using TMG.Input;
using XTMF;


namespace Tasha.Validation.PerformanceMeasures;

public class TripLengthMeasure : IPostHousehold
{        

    [RunParameter("Min Age", 11, "The minimum age to record the results for.")]
    public int MinAge;

    [RunParameter("Max Distance", 40, "The maximum distance (km) to analyze (anything over this distance will be aggregated under the same bin)")]
    public int MaxDistanceInKm;

    [SubModelInformation(Required = true, Description = "Where do you want to save the Purpose Results. Must be in .CSV format.")]
    public FileLocation TripLengthResults;

    private ConcurrentDictionary<Activity, float[]> ResultsDictionary = new();

    [RootModule]
    public ITashaRuntime Root;


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
        var distances = Root.ZoneSystem.Distances;
        float expFactor;
        if (iteration == Root.TotalIterations - 1)
        {
            foreach (var person in household.Persons)
            {
                expFactor = person.ExpansionFactor;                    
                if (person.Age >= MinAge)
                {
                    foreach (var tripChain in person.TripChains)
                    {
                        foreach (var trip in tripChain.Trips)
                        {                                     
                            if (trip.Mode != null)
                            {
                                lock (this)
                                {
                                    var tripDistance = distances[trip.OriginalZone.ZoneNumber, trip.DestinationZone.ZoneNumber];
                                    AddToResults(trip.Purpose, tripDistance * 0.001f, expFactor);
                                }
                            }
                        }
                    }
                }
            }
        }            
    }

    public void AddToResults(Activity purpose, float tripDistance, float expFactor)
    {
        int distanceBin;
        if(tripDistance > MaxDistanceInKm)
        {
            distanceBin = MaxDistanceInKm;
        }
        else
        {
            distanceBin = (int)Math.Round(tripDistance, 0);
        }
        if (!ResultsDictionary.TryGetValue(purpose, out float[] data))
        {
            lock (ResultsDictionary)
            {
                if (!ResultsDictionary.TryGetValue(purpose, out data))
                {
                    data = new float[MaxDistanceInKm + 1];
                    ResultsDictionary[purpose] = data;
                }
            }
        }
        // we need to lock here in order to make sure we don't have a race condition between the read and write
        lock (this)
        {
            data[distanceBin] = data[distanceBin] + expFactor;
        }
    }

    public void IterationFinished(int iteration)
    {
        using (StreamWriter writer = new(TripLengthResults))
        {
            writer.WriteLine("Trip Purpose vs. Trip Distance (km) & Cells = Number Of Occurrences (expanded or not-expanded)");

            writer.Write(" ,");
            for (int i = 0; i < MaxDistanceInKm + 1; i++)
            {
                writer.Write("{0},", i);
            }
            writer.WriteLine();
            foreach (var pair in ResultsDictionary.OrderBy(k=>k.Key))
            {
                writer.Write("{0}, ", pair.Key);
                for (int j = 0; j < ResultsDictionary[pair.Key].Length; j++)
                {
                    writer.Write("{0}, ", ResultsDictionary[pair.Key][j]);
                }
                writer.WriteLine();
            }
        }
        ResultsDictionary.Clear();
    }

    public void IterationStarting(int iteration)
    {
    }

    public void Load(int maxIterations)
    {            
    }

    public bool RuntimeValidation(ref string error)
    {

        return true;
    }
}
