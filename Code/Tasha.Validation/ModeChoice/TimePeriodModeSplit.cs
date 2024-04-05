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
using System.Linq;
using System.Threading;
using Tasha.Common;
using TMG.Input;
using XTMF;
namespace Tasha.Validation.ModeChoice;

public class TimePeriodModeSplit : IPostHousehold
{
    [RootModule]
    public ITashaRuntime Root;

    [SubModelInformation(Required = true, Description = "The location to save the mode splits to.")]
    public FileLocation OutputFileLocation;

    public string Name { get; set; }

    public float Progress
    {
        get
        {
            return 0f;
        }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get
        {
            return null;
        }
    }

    public class TimePeriod : IModule
    {
        [ParentModel]
        public TimePeriodModeSplit Parent;

        [RunParameter("Start Time", "4:00AM", typeof(Time), "The start time for the time period.")]
        public Time StartTime;

        [RunParameter("End Time", "4:00AM", typeof(Time), "The end time for the time period, exclusive.")]
        public Time EndTime;

        internal Dictionary<Activity, float[]> Counts = [];
        SpinLock WriteLock = new(false);

        public bool Execute(Time tripStart, int modeIndex, float expansionFactor, Activity activity)
        {
            if(StartTime <= tripStart && tripStart < EndTime)
            {
                bool taken = false;
                WriteLock.Enter(ref taken);
                GetPurposeCount(activity)[modeIndex] += expansionFactor;
                if(taken) WriteLock.Exit(true);
                return true;
            }
            return false;
        }

        private float[] GetPurposeCount(Activity purpose)
        {
            if (!Counts.TryGetValue(purpose, out float[] value))
            {
                Counts.Add(purpose, value = new float[Parent.Modes.Length]);
            }
            return value;
        }

        public void FinishIteration(int iteration)
        {
            Counts.Clear();
        }

        public void StartIteration(int iteration)
        {

        }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool RuntimeValidation(ref string error)
        {
            if(StartTime > EndTime)
            {
                error = "In '" + Name + "' the start time is later than the end time!";
                return false;
            }
            return true;
        }
    }

    public TimePeriod[] TimePeriods;


    ITashaMode[] Modes;

    [RunParameter("Minimum Age", 11, "The minimum age for the trips to be recorded in the total.")]
    public int MinimumAge;

    public void Execute(ITashaHousehold household, int iteration)
    {
        var persons = household.Persons;
        for(int i = 0; i < persons.Length; i++)
        {
            if(persons[i].Age >= MinimumAge)
            {
                var expansionFactor = persons[i].ExpansionFactor;
                var tripChains = persons[i].TripChains;
                for(int j = 0; j < tripChains.Count; j++)
                {
                    var tripChain = tripChains[j].Trips;
                    for(int k = 0; k < tripChain.Count; k++)
                    {
                        var tripStart = tripChain[k].TripStartTime;
                        var mode = tripChain[k].Mode;
                        int modeIndex = -1;
                        for(int l = 0; l < Modes.Length; l++)
                        {
                            if(Modes[l] == mode)
                            {
                                modeIndex = l;
                                break;
                            }
                        }
                        var activity = tripChain[k].Purpose;
                        if(modeIndex >= 0)
                        {
                            for(int l = 0; l < TimePeriods.Length; l++)
                            {
                                if(TimePeriods[l].Execute(tripStart, modeIndex, expansionFactor, activity))
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    List<float[]> IterationTotals = [];

    public void IterationFinished(int iteration)
    {
        float[] counts = new float[Modes.Length];
        for(int i = 0; i < counts.Length; i++)
        {
            // get the sum of each time period for each purpose for the ith mode
            counts[i] = TimePeriods.Sum(period => period.Counts.Sum((type) => type.Value[i]));
        }
        IterationTotals.Add(counts);
        using(var writer = new StreamWriter(OutputFileLocation, true))
        {
            writer.Write("Iteration: ");
            writer.WriteLine(iteration);
            writer.Write("Mode");
            for(int i = 0; i < TimePeriods.Length; i++)
            {
                foreach(var key in TimePeriods[i].Counts.Keys)
                {
                    writer.Write(',');
                    writer.Write(TimePeriods[i].Name);
                    writer.Write(':');
                    writer.Write(Enum.GetName(typeof(Activity), key));
                }
            }
            writer.WriteLine(",Total");
            for(int i = 0; i < Modes.Length; i++)
            {
                writer.Write(Modes[i].ModeName);
                for(int j = 0; j < TimePeriods.Length; j++)
                {
                    foreach(var key in TimePeriods[j].Counts.Keys)
                    {
                        writer.Write(',');
                        writer.Write(TimePeriods[j].Counts[key][i]);
                    }
                }
                writer.Write(',');
                writer.WriteLine(counts[i]);
            }
            if(iteration >= Root.TotalIterations - 1)
            {
                writer.WriteLine();
                writer.Write("Iterations");
                for(int i = 0; i < Modes.Length; i++)
                {
                    writer.Write(',');
                    writer.Write(Modes[i].ModeName);
                }
                writer.WriteLine(",,Total");

                for(int it = 0; it < IterationTotals.Count; it++)
                {
                    var row = IterationTotals[it];
                    writer.Write((it + 1));
                    for(int i = 0; i < row.Length; i++)
                    {
                        writer.Write(',');
                        writer.Write(row[i]);
                    }
                    writer.Write(',');
                    writer.Write(',');
                    writer.WriteLine(row.Sum());
                }
            }
        }
        for(int i = 0; i < TimePeriods.Length; i++)
        {
            TimePeriods[i].FinishIteration(iteration);
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
        Modes = Root.AllModes.ToArray();
        for(int i = 0; i < TimePeriods.Length; i++)
        {
            TimePeriods[i].StartIteration(iteration);
        }
    }
}
