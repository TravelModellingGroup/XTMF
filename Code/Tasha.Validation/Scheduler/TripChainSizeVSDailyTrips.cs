/*
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
using Datastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tasha.Common;
using TMG.Input;
using XTMF;
namespace Tasha.Validation.Scheduler
{

    public class TripChainSizeVSDailyTrips : IPostHousehold
    {

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [RunParameter("Max Trips", 10, "The maximum number of trips, trip chains or daily trip values over this will be categorized in the maximum bin.")]
        public int MaxTrips;

        [SubModelInformation(Required = true, Description = "The location to save the results to.")]
        public FileLocation OutputLocation;

        /// <summary>
        /// Results[#OfTripsInChain][DailyTripCount]
        /// </summary>
        private float[][] Results;

        public void Execute(ITashaHousehold household, int iteration)
        {
            lock (Results)
            {
                foreach (var person in household.Persons)
                {
                    var expFactor = person.ExpansionFactor;
                    var dailyTripsByPerson = Math.Min(person.TripChains.Sum(tc => tc.Trips.Count), MaxTrips);
                    foreach (var tripChain in person.TripChains)
                    {
                        var tripChainLength = Math.Min(tripChain.Trips.Count, MaxTrips);
                        Results[tripChainLength][dailyTripsByPerson] += expFactor;
                    }
                }
            }
        }

        public void IterationFinished(int iteration)
        {
            using (StreamWriter writer = new StreamWriter(OutputLocation))
            {
                //write header
                writer.Write("TripChainSize\\DailyTripCount");
                for (int i = 0; i < Results.Length; i++)
                {
                    writer.Write(',');
                    writer.Write(i);
                }
                writer.WriteLine();
                // for each row
                for (int i = 0; i < Results.Length; i++)
                {
                    writer.Write(i);
                    for (int j = 0; j < Results[i].Length; j++)
                    {
                        writer.Write(',');
                        writer.Write(Results[i][j]);
                    }
                    writer.WriteLine();
                }
            }
        }

        public void IterationStarting(int iteration)
        {
            if (Results == null)
            {
                Results = new float[MaxTrips + 1][];
                for (int i = 0; i < Results.Length; i++)
                {
                    Results[i] = new float[Results.Length];
                }
            }
            else
            {
                for (int i = 0; i < Results.Length; i++)
                {
                    Array.Clear(Results[i], 0, Results[i].Length);
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
    }

}
