/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Tasha.Common;
using XTMF;

namespace Tasha.EMME
{
    [ModuleInformation(
        Description = "Allows the collection of a peak hour factor for a given time period.  This can then be set to a resource for later use."
        )]
    public class ComputePeakHourFactor : IPostHouseholdIteration
    {
        [RunParameter("Start Time", "6:00AM", typeof(Time), "The time period starts at.")]
        public Time StartTime;

        [RunParameter("End Time", "9:00AM", typeof(Time), "The time period ends at.")]
        public Time EndTime;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private float[] TripBins = new float[0];

        [SubModelInformation(Required = false, Description = "")]
        public ISetableDataSource<float> StoreResultTo;

        [SubModelInformation(Required = false, Description = "")]
        public ISetableDataSource<Time> PeakHourStart;

        [SubModelInformation(Required = false, Description = "")]
        public ISetableDataSource<Time> PeakHourEnd;

        public void HouseholdComplete(ITashaHousehold household, bool success)
        {
        }

        private int GetTimeBin(Time time)
        {
            var t = (int)((time - StartTime).ToMinutes() / 15.0f);
            return t < 0 || t >= TripBins.Length ? -1 : t;
        }

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {
            var persons = household.Persons;
            lock (this)
            {
                foreach (var person in persons)
                {
                    var expFactor = person.ExpansionFactor;
                    foreach (var tripChain in person.TripChains)
                    {
                        foreach (var trip in tripChain.Trips)
                        {
                            if (UsesModeToCheck(trip.Mode))
                            {
                                var startIndex = GetTimeBin(trip.TripStartTime);
                                if (startIndex >= 0)
                                {
                                    TripBins[startIndex] += expFactor;
                                }
                            }
                        }
                    }
                }
            }
        }

        public sealed class ModeLink : IModule
        {
            [RootModule]
            public ITashaRuntime Root;

            [RunParameter("Mode Name", "Auto", "The name of the mode")]
            public string ModeName;

            internal ITashaMode Mode;

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public bool RuntimeValidation(ref string error)
            {
                foreach (var mode in Root.AllModes)
                {
                    if (mode.ModeName == ModeName)
                    {
                        Mode = mode;
                        return true;
                    }
                }
                error = "In '" + Name + "' we were unable to find a mode called '" + ModeName + "'";
                return false;
            }
        }

        public ModeLink[] ModesToCheck;

        private bool UsesModeToCheck(ITashaMode mode)
        {
            var modes = ModesToCheck;
            for (int i = 0; i < modes.Length; i++)
            {
                if (modes[i].Mode == mode)
                {
                    return true;
                }
            }
            return false;
        }

        public void HouseholdStart(ITashaHousehold household, int householdIterations)
        {
        }

        private static bool SetIfExists<T>(ISetableDataSource<T> dataSource, T value)
        {
            if (dataSource != null)
            {
                if (!dataSource.Loaded)
                {
                    dataSource.LoadData();
                }
                dataSource.SetData(value);
                return true;
            }
            return false;
        }

        public void IterationFinished(int iteration, int totalIterations)
        {
            int hourwindow;
            var result = ComputeFactor(out hourwindow);
            if (!(SetIfExists(PeakHourStart, Time.FromMinutes((hourwindow + 4) * 15))
                || SetIfExists(PeakHourEnd, Time.FromMinutes((hourwindow + 5) * 15))
                || SetIfExists(StoreResultTo, result)))
            {
                Console.WriteLine(Name + "' Peak Hour Factor = " + result);
            }
        }

        private float ComputeFactor(out int startOfHourWindow)
        {
            // first find the max hour window
            var bins = TripBins;
            float window = bins[0] + bins[1] + bins[2] + bins[3];
            // now that the window is loaded find the max index
            float maxValue = window;
            float sum = window;
            int bestIndex = 0;
            for (int i = 1; i < bins.Length - 4; i++)
            {
                sum += bins[i + 4];
                window = window - bins[i - 1] + bins[i];
                if (window > maxValue)
                {
                    bestIndex = i;
                    maxValue = window;
                }
            }
            startOfHourWindow = bestIndex;
            return (bins[bestIndex] + bins[bestIndex + 1] + bins[bestIndex + 2] + bins[bestIndex + 3]) / sum;
        }

        public void IterationStarting(int iteration, int totalIterations)
        {
            // compute the number of bins that we are going to need
            var minutes = (EndTime - StartTime).ToMinutes();
            // ReSharper disable once InconsistentlySynchronizedField
            TripBins = new float[(int)Math.Ceiling(minutes / 15.0f)];
        }

        public bool RuntimeValidation(ref string error)
        {
            var minutes = (EndTime - StartTime).ToMinutes();
            if (minutes < 0)
            {
                error = "In '" + Name + "' End time occurs before start time!";
                return false;
            }
            if (minutes < 60)
            {
                error = "In '" + Name + "' the time slice needs to be over an hour!";
                return false;
            }
            return true;
        }
    }
}