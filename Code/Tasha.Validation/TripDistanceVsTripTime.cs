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
        Description = "This module takes in the Household data (either real or TASHA scheduled) and then " +
                        "computes and records the average distance travelled for the different start times of " +
                        "all trips. It does not compute separately for each trip purpose, but rather agglomerates " +
                        "them and produces start time vs distance of ALL trips. \nNote: The distance is calculated " +
                        "as Manhattan distance in meters. "
        )]
    public class TripDistanceVsTripTime : IPostHousehold
    {
        [RunParameter("Output File Name", "TripDistanceVsTripStart", "The file that will contain the results (No Extension)")]
        public string OutputFile;

        [RunParameter("Real Data?", false, "Are you looking at real data?")]
        public bool RealData;

        [RootModule]
        public ITashaRuntime Root;

        [RunParameter("Trip Start Time", false, "Should we be using the trip start time or the activity start times?")]
        public bool TripStartTime;

        private Dictionary<int, List<float>> Results = new Dictionary<int, List<float>>();

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

        public void Execute(ITashaHousehold household, int iteration)
        {
            lock (this)
            {
                foreach (var person in household.Persons)
                {
                    foreach (var tripChain in person.TripChains)
                    {
                        foreach (var trip in tripChain.Trips)
                        {
                            float overallDistance;

                            if (trip.OriginalZone == trip.DestinationZone)
                            {
                                overallDistance = trip.OriginalZone.InternalDistance;
                            }
                            else
                            {
                                overallDistance = (Math.Abs(trip.OriginalZone.X - trip.DestinationZone.X) + Math.Abs(trip.OriginalZone.Y - trip.DestinationZone.Y));
                            }

                            if (Results.ContainsKey((TripStartTime ? trip.TripStartTime : trip.ActivityStartTime).Hours))
                            {
                                Results[(TripStartTime ? trip.TripStartTime : trip.ActivityStartTime).Hours].Add(overallDistance);
                            }
                            else
                            {
                                List<float> distance = new List<float>();
                                distance.Add(overallDistance);
                                Results.Add((TripStartTime ? trip.TripStartTime : trip.ActivityStartTime).Hours, distance);
                            }
                        }
                    }
                }
            }
        }

        public void IterationFinished(int iteration)
        {
            lock (this)
            {
                string fileName;
                if (RealData)
                {
                    fileName = OutputFile + "Data.csv";
                }
                else
                {
                    fileName = OutputFile + "Tasha.csv";
                }
                using (StreamWriter writer = new StreamWriter(fileName))
                {
                    writer.WriteLine("Start Time Hour, Average Distance, Number of Occurances");

                    foreach (var pair in Results)
                    {
                        var averageDistance = pair.Value.Average();
                        writer.WriteLine("{0}, {1}, {2}", pair.Key, averageDistance, pair.Value.Count);
                    }
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
        }

        public override string ToString()
        {
            return "Currently Validating Trip Distances!";
        }
    }
}