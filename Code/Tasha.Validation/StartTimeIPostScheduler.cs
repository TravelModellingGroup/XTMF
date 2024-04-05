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
using System.IO;
using Tasha.Common;
using TMG.Input;
using XTMF;

namespace Tasha.Validation;

[ModuleInformation(
    Description = "This module is used for validation purposes. It computes and records " +
                    "the start times of activities immediately after the scheduler is finished. " +
                    "This is important as it allows for the analysis of the planned schedule before mode choice occurs."

    )]
public class StartTimeIPostScheduler : IPostScheduler
{
    [SubModelInformation(Required = true, Description = "The location to store the data.")]
    public FileLocation OutputFile;

    [RootModule]
    public ITashaRuntime Root;

    private Dictionary<Activity, Dictionary<int, float>> ActivityStartTimeDictionaries = [];

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
        get { return new Tuple<byte, byte, byte>(100, 100, 100); }
    }

    public void Execute(ITashaHousehold household)
    {
        lock (this)
        {
            foreach(var person in household.Persons)
            {
                var expFactor = person.ExpansionFactor;
                foreach(var tripChain in person.TripChains)
                {
                    foreach(var trip in tripChain.Trips)
                    {
                        Dictionary<int, float> activityDictionary = GetDictionary(trip.Purpose);
                        int hour = trip.ActivityStartTime.Hours;
                        if(activityDictionary.ContainsKey(hour))
                        {
                            activityDictionary[hour] += expFactor;
                        }
                        else
                        {
                            activityDictionary.Add(hour, expFactor);
                        }
                    }
                }
            }
        }
    }

    private Dictionary<int, float> GetDictionary(Activity purpose)
    {
        if (!ActivityStartTimeDictionaries.TryGetValue(purpose, out Dictionary<int, float> ret))
        {
            ret = [];
            ActivityStartTimeDictionaries[purpose] = ret;
        }
        return ret;
    }

    public void IterationFinished(int iterationNumber)
    {
        using StreamWriter writer = new(OutputFile);
        foreach (var activityDictionary in ActivityStartTimeDictionaries)
        {
            var activityStr = activityDictionary.Key.ToString();
            foreach (var e in activityDictionary.Value)
            {
                writer.WriteLine("{2},{0},{1}", e.Key, e.Value, activityStr);
            }
        }
    }

    public void Load(int maxIterations)
    {
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void IterationStarting(int iteration)
    {
        ActivityStartTimeDictionaries.Clear();
    }
}