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
using Datastructure;
using Tasha.Common;
using XTMF;

namespace Tasha.Modes
{
    [ModuleInformation(Name = "Generate Time Period Factor Matrices",
                        Description = "Generates 4x4 TPF matrices from TTS data. Any trips which fall outside the definition of the three time periods will be considered 'offpeak'")]
    public class GenerateTimePerioidMatrix : IPostHousehold
    {
        [RunParameter("Afternoon Period Definition", "1500-1829", typeof(RangeSet), "RANGE of afternnon peak period, in TTS-formatted hours (e.g. 400-2800).")]
        public RangeSet AfternoonTimePeriod;

        [RunParameter("Allowed Deviations", 0, "[Incomplete] The maximum number of acceptable non-primary activities which agents can insert into thei trip-chains.")]
        public int Degrees;

        [RunParameter("Home Anchor Override", "", "The name of the variable used to store an agent's initial activity. If blank, this will default to 'Home'")]
        public string HomeAnchorOverrideName;

        [RunParameter("Midday Period Definition", "900-1500", typeof(RangeSet), "RANGE of midday time period, in TTS-formatted hours (e.g. 400-2800).")]
        public RangeSet MiddayTimePeriod;

        [RunParameter("Morning Period Definition", "600-859", typeof(RangeSet), "RANGE of morning peak period, in TTS-formatted hours (e.g. 400-2800).")]
        public RangeSet MorningTimePeriod;

        [RunParameter("Results File", "tpfResults.txt", "The file to save result matrices into.")]
        public string ResultsFile;

        [RootModule]
        public ITashaRuntime Root;

        private static Tuple<byte, byte, byte> _ProgressColour = new(100, 100, 150);
        private Dictionary<string, float[,]> WorkMatrices;

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
            get { return _ProgressColour; }
        }

        public void Execute(ITashaHousehold household, int iteration)
        {
            lock (this)
            {
                foreach (var p in household.Persons)
                {
                    if (p.TripChains.Count < 1)
                        continue; //Skip people with no trips

                    string key = p.Occupation + "," + p.EmploymentStatus + "," + p.StudentStatus; //The key to determine which table the person's trips belong to.

                    string prevAct;
                    if (string.IsNullOrEmpty(HomeAnchorOverrideName))
                    {
                        prevAct = Activity.Home.ToString();
                    }
                    else
                    {
                        var x = p.TripChains[0].GetVariable(HomeAnchorOverrideName);
                        if (x != null) prevAct = x.ToString();
                        else prevAct = Activity.Home.ToString();
                    }

                    var trips = p.TripChains[0].Trips; //Assumes that each person has at most one trip chain.
                    Time outgoingTripTime = trips[0].TripStartTime;

                    int workActCounter = 0;
                    int schoolActCounter = 0;

                    foreach (var trip in p.TripChains[0].Trips)
                    {
                        var nextAct = trip.Purpose;

                        if (prevAct == Activity.Home.ToString()) //Starting from home
                        {
                            if (nextAct == Activity.PrimaryWork || nextAct == Activity.SecondaryWork || nextAct == Activity.WorkBasedBusiness)
                            {
                                //Outgoing work trip
                                outgoingTripTime = trip.TripStartTime;
                                workActCounter++;
                            }
                            else if (nextAct == Activity.School)
                            {
                                //Outgoing school trip
                                outgoingTripTime = trip.TripStartTime;
                                schoolActCounter++;
                            }
                        }
                        else if (nextAct == Activity.Home && (workActCounter > 0 || schoolActCounter > 0)) //Ending at home
                        {
                            Time incomingTripTime = trip.TripStartTime;

                            //Save tour in matrix
                            if ((workActCounter <= (1 + Degrees)) && (schoolActCounter <= (Degrees + 1))) //Only if there were fewer deviations than allowed.
                            {
                                float[,] matrix;
                                if (!WorkMatrices.ContainsKey(key)) //Check if this specific key has already been mapped.
                                {
                                    matrix = new float[4, 4];
                                    matrix[GetTimePeriod(outgoingTripTime), GetTimePeriod(incomingTripTime)] = household.ExpansionFactor;
                                    WorkMatrices.Add(key, matrix);
                                }
                                else
                                {
                                    if (WorkMatrices.TryGetValue(key, out matrix))
                                    {
                                        matrix[GetTimePeriod(outgoingTripTime), GetTimePeriod(incomingTripTime)] +=
                                            household.ExpansionFactor;
                                    }
                                }
                            }

                            //Reset counters
                            workActCounter = 0;
                            schoolActCounter = 0;
                        }
                        else //Otherwise
                        {
                            workActCounter += (workActCounter > 0) ? 1 : 0;
                            schoolActCounter += (schoolActCounter > 0) ? 1 : 0;
                        }

                        prevAct = nextAct.ToString();
                    }
                }
            }
        }

        public void IterationFinished(int iteration)
        {
            var path = ResultsFile;

            using StreamWriter sw = new(path);
            sw.WriteLine("Time Period Matrices");
            sw.WriteLine();
            sw.WriteLine("Morning Period [0]: " + MorningTimePeriod);
            sw.WriteLine("Midday Period [1]: " + MiddayTimePeriod);
            sw.WriteLine("Afternoon Period [2]: " + AfternoonTimePeriod);
            sw.WriteLine("Offpeak [3]");
            sw.WriteLine();
            sw.WriteLine("Table Names = [Occupation], [Employment Status], [Student Status]");

            foreach (var e in WorkMatrices)
            {
                sw.WriteLine();
                var table = e.Value;
                sw.WriteLine("Table: '" + e.Key + "':");
                for (int i = 0; i < 4; i++)
                {
                    string s = "";
                    for (int j = 0; j < 4; j++)
                    {
                        s += "\t" + table[i, j];
                    }
                    sw.WriteLine(s);
                }
            }
        }

        public void Load(int maxIterations)
        {
            WorkMatrices = [];
        }

        public bool RuntimeValidation(ref string error)
        {
            if (MorningTimePeriod.Overlaps(MiddayTimePeriod))
            {
                error = "Morning period overlaps midday period!";
                return false;
            }
            if (MorningTimePeriod.Overlaps(AfternoonTimePeriod))
            {
                error = "Morning period overlaps afternoon period!";
                return false;
            }
            if (MiddayTimePeriod.Overlaps(AfternoonTimePeriod))
            {
                error = "Midday period overlaps afteroon period!";
                return false;
            }

            return true;
        }

        public void IterationStarting(int iteration)
        {
            //throw new NotImplementedException();
        }

        private int GetTimePeriod(Time tTime)
        {
            int iTime = tTime.Hours * 100 + tTime.Minutes;

            if (iTime > 2800)
                throw new XTMFRuntimeException(this, "Cannot have a time of more than 28:00!");

            if (MorningTimePeriod.Contains(iTime))
                return 0;
            if (MiddayTimePeriod.Contains(iTime))
                return 1;
            if (AfternoonTimePeriod.Contains(iTime))
                return 2;
            return 3;
        }
    }
}