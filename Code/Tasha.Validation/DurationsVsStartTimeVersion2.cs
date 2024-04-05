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
using Tasha.Common;
using XTMF;

namespace Tasha.Validation
{
    [ModuleInformation(
        Description = "<p>A validation module which gives us start time vs duration information " +
                        "for the different purposes. As an input, it takes in the newly scheduled " +
                        "households and then produces different files for the different purposes. " +
                        "For each purpose, the module records when the trips start (hour). It then " +
                        "computes and records the average duration for all trips that start at a common " +
                        "start hour. As an output, the module produces average durations for each start hour data " +
                        "for all observed trip purposes scheduled by TASHA. </p>" +

                        "Note: The module can also be used on real data instead of TASHA run data. This is important " +
                        "when one wants to validate the duration of trips that TASHA is scheduling."
        )]
    public class DurationsVsStartTimesVersion2 : IPostHousehold
    {
        public Dictionary<KeyValuePair<Activity, int>, List<int>> DurationsDict = [];

        [RunParameter("Output Directory", "OutputDirectory", "The directory that will contain the results")]
        public string OutputDirectory;

        [RunParameter("Real data?", false, "Are you running this on the real data?")]
        public bool RealData;

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
            get { return new Tuple<byte, byte, byte>(32, 76, 169); }
        }

        public void Execute(ITashaHousehold household, int iteration)
        {
            lock (this)
            {
                foreach (var person in household.Persons)
                {
                    foreach (var tripChain in person.TripChains)
                    {
                        var chain = tripChain.Trips;
                        for (int i = 0; i < (chain.Count - 1); i++)
                        {
                            var thisTrip = chain[i];
                            var nextTrip = chain[i + 1];
                            var currentMode = thisTrip.Mode;
                            thisTrip.Mode = Root.AutoMode;
                            var hours = thisTrip.ActivityStartTime.Hours;
                            var duration = (int)((nextTrip.TripStartTime - thisTrip.ActivityStartTime).ToMinutes());

                            KeyValuePair<Activity, int> bob = new(thisTrip.Purpose, hours);
                            if (DurationsDict.TryGetValue(bob, out List<int> ourList))
                            {
                                ourList.Add(duration);
                            }
                            else
                            {
                                ourList = [duration];
                                DurationsDict[bob] = ourList;
                            }

                            thisTrip.Mode = currentMode;
                        }
                    }
                }
            }
        }

        public void IterationFinished(int iteration)
        {
            Dictionary<Activity, StreamWriter> writerDict = [];
            try
            {
                if (!Directory.Exists(OutputDirectory))
                {
                    Directory.CreateDirectory(OutputDirectory);
                }
                lock (this)
                {
                    foreach (var pair in DurationsDict)
                    {
                        string fileName;
                        var purpose = pair.Key.Key;
                        var hour = pair.Key.Value;
                        var averageDur = pair.Value.Average();
                        var stdDev = GetStdDev(pair.Value, averageDur);
                        //standard deviation 
                        if (!writerDict.ContainsKey(purpose))
                        {
                            if (RealData)
                            {
                                fileName = Path.Combine(OutputDirectory, purpose + "DurationsData.csv");
                            }
                            else
                            {
                                fileName = Path.Combine(OutputDirectory, purpose + "DurationsTasha.csv");
                            }
                            writerDict[purpose] = new StreamWriter(fileName);
                            writerDict[purpose].WriteLine("Start Times,AverageDuration(Minutes),StdDev(Minutes)");
                        }
                        writerDict[purpose].WriteLine("{0},{1},{2}", hour, averageDur, stdDev);
                    }
                }
            }
            finally
            {
                foreach (var pair in writerDict)
                {
                    pair.Value.Close();
                }
            }
        }

        private double GetStdDev(List<int> value, double averageDur)
        {
            return value.Average(v => Math.Abs(averageDur - v));
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
        }
    }
}