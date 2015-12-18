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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMG.Emme;
using XTMF;
using Tasha.Common;
using TMG;
using Datastructure;
using TMG.Input;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using TMG.Functions;
using System.Collections.Concurrent;
using System.IO;

namespace Tasha.Validation.ModeChoice
{
    [ModuleInformation(
        Description = "This module is designed to get the mode splits originating from a given area by personal attributes."
        )]
    public sealed class ModeSplitsByPersonAttributes : IPostHouseholdIteration
    {
        [RunParameter("Save Every Time", "false", typeof(bool), "Optionally save the data for each outer loop iteration of GTAModel.")]
        public bool SaveEveryTime;

        /// <summary>
        /// The age categories to summarize people into.
        /// </summary>
        [RunParameter("Age Ranges", "0-10,11-15,16-19,20-34,35-64,65-100", typeof(RangeSet), "The age categories to save for,")]
        public RangeSet AgeRanges;

        /// <summary>
        /// The listing of occupations that are being considered
        /// </summary>
        private Occupation[] Occupations = new Occupation[] { Occupation.Professional, Occupation.Office, Occupation.Retail, Occupation.Manufacturing, Occupation.NotEmployed };

        /// <summary>
        /// The listing of employment statuses that are being considered
        /// </summary>
        private TTSEmploymentStatus[] EmploymentStatuses = new TTSEmploymentStatus[] { TTSEmploymentStatus.FullTime, TTSEmploymentStatus.PartTime,
            TTSEmploymentStatus.WorkAtHome_FullTime, TTSEmploymentStatus.WorkAtHome_PartTime, TTSEmploymentStatus.NotEmployed };


        public sealed class CatchmentArea : XTMF.IModule
        {
            /// <summary>
            /// The catchment area for this analysis.
            /// </summary>
            [RunParameter("ContainedZones", "0-9999", typeof(RangeSet), "The zones within the catchment area.")]
            public RangeSet ContainedZones;

            [RunParameter("Start Time", "0:00", typeof(Time), "The minimum start time for a trip's start time in order to be recorded.")]
            public Time StartTime;

            [RunParameter("End Time", "30:00", typeof(Time), "The maximum start time for a trip's start time in order to be recorded.")]
            public Time EndTime;

            [RootModule]
            public ModeSplitsByPersonAttributes Root;

            [SubModelInformation(Required = true, Description = "The location to save the data to in csv format.")]
            public FileLocation SaveTo;

            /// <summary>
            /// The storage for the data in
            /// </summary>
            private float[] DataStorage;

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }

            private ConcurrentDictionary<ITashaPerson, float[]> OperatingOn = new ConcurrentDictionary<ITashaPerson, float[]>();

            private ConcurrentStack<float[]> ModePool = new ConcurrentStack<float[]>();
            private const int MaxModePoolSize = 100;

            public void HouseholdComplete(ITashaHousehold household, bool success)
            {

                var persons = household.Persons;
                foreach (var person in persons)
                {
                    var tripChains = person.TripChains;
                    if (tripChains.Count > 0)
                    {
                        float[] personalModeChoices;
                        // since this is the final phase, remove the household
                        if (OperatingOn.TryRemove(person, out personalModeChoices) && success)
                        {

                            RecordData(person, personalModeChoices);
                            // once we are done add the array back onto the pool as long as we are not going to overflow it
                            if (ModePool.Count < MaxModePoolSize)
                            {
                                Array.Clear(personalModeChoices, 0, personalModeChoices.Length);
                                ModePool.Push(personalModeChoices);
                            }
                        }

                    }
                }
            }

            private void RecordData(ITashaPerson person, float[] personalModeChoices)
            {
                // first build the persons offset
                int dataOffset = GetDataOffset(Root.AgeRanges.IndexOf(person.Age), person.Occupation, person.EmploymentStatus);
                // now we can store the results for the person
                lock (DataStorage)
                {
                    for (int i = 0; i < personalModeChoices.Length; i++)
                    {
                        DataStorage[dataOffset++] += personalModeChoices[i];
                    }
                }
            }

            private int GetDataOffset(int ageCategory, Occupation occupation, TTSEmploymentStatus employmentStatus)
            {
                var ageCat = ageCategory >= 0 ? ageCategory : 0;
                var occCat = Array.IndexOf(Root.Occupations, occupation);
                var empCat = Array.IndexOf(Root.EmploymentStatuses, employmentStatus);
                return ((ageCat * Root.Occupations.Length + occCat) * Root.EmploymentStatuses.Length + empCat) * NumberOfModes;
            }

            private int NumberOfModes;

            private void BuildDataStorage()
            {
                DataStorage = new float[Root.AgeRanges.Count * Root.Occupations.Length * Root.EmploymentStatuses.Length * Root.Modes.Count];
            }

            public void HouseholdStart(ITashaHousehold household)
            {
                var persons = household.Persons;
                foreach (var person in persons)
                {
                    if (person.TripChains.Count > 0)
                    {
                        float[] toAttach;
                        if (!ModePool.TryPop(out toAttach))
                        {
                            toAttach = new float[Root.Modes.Count];
                        }
                        OperatingOn[person] = toAttach;
                    }
                }
            }

            public void HouseholdIterationComplete(ITashaHousehold household)
            {
                var persons = household.Persons;
                foreach (var person in persons)
                {
                    var tripChains = person.TripChains;
                    if (tripChains.Count > 0)
                    {
                        var personalModeChoices = OperatingOn[person];
                        var expansionFactor = person.ExpansionFactor;
                        foreach (var tripChain in tripChains)
                        {
                            foreach (var trip in tripChain.Trips)
                            {
                                // if the trip is within the catchment area record it
                                if (ContainedZones.Contains(trip.OriginalZone.ZoneNumber))
                                {
                                    var startTime = trip.TripStartTime;
                                    if (startTime >= StartTime && startTime < EndTime)
                                    {
                                        personalModeChoices[Root.Modes[trip.Mode]] += expansionFactor;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            internal void IterationFinished()
            {
                // normalize the data by the number of household iterations
                VectorHelper.Multiply(DataStorage, 0, DataStorage, 0, (1.0f / Root.HouseholdIterations), DataStorage.Length);
                // now we can store the results
                using (var writer = new StreamWriter(SaveTo))
                {
                    writer.WriteLine("AgeCategory,Occupation,EmploymentStatus,Mode,ExpandedTrips");
                    var allModes = Root.Root.AllModes;
                    var ageString = Root.AgeRanges.Select(r => r.ToString()).ToArray();
                    var occString = Root.Occupations.Select(o => Enum.GetName(typeof(Occupation), o)).ToArray();
                    var empString = Root.EmploymentStatuses.Select(e => Enum.GetName(typeof(TTSEmploymentStatus), e)).ToArray();
                    var modeString = allModes.Select(m => m.ModeName).ToArray();
                    for (int ageCat = 0; ageCat < Root.AgeRanges.Count; ageCat++)
                    {
                        for (int occCat = 0; occCat < Root.Occupations.Length; occCat++)
                        {
                            for (int empCat = 0; empCat < Root.EmploymentStatuses.Length; empCat++)
                            {
                                int offset = GetDataOffset(ageCat, Root.Occupations[occCat], Root.EmploymentStatuses[empCat]);
                                for (int mode = 0; mode < allModes.Count; mode++)
                                {
                                    writer.Write(ageString[ageCat]);
                                    writer.Write(',');
                                    writer.Write(occString[occCat]);
                                    writer.Write(',');
                                    writer.Write(empString[empCat]);
                                    writer.Write(',');
                                    writer.Write(modeString[mode]);
                                    writer.Write(',');
                                    writer.WriteLine(DataStorage[offset + mode]);
                                }
                            }
                        }
                    }
                }
            }

            internal void IterationStarting()
            {
                NumberOfModes = Root.Modes.Count;
                ModePool.Clear();
                OperatingOn.Clear();
                BuildDataStorage();
            }
        }


        /// <summary>
        /// Use this to check to see if we are saving in this iteration
        /// </summary>
        private bool SaveThisIteration;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [RootModule]
        public ITashaRuntime Root;

        private Dictionary<ITashaMode, int> Modes;

        private int HouseholdIterations = 1;

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {
            if (SaveThisIteration)
            {
                foreach (var area in CatchmentAreas)
                {
                    area.HouseholdIterationComplete(household);
                }
            }
        }

        public void HouseholdStart(ITashaHousehold household, int householdIterations)
        {
            if (SaveThisIteration)
            {
                HouseholdIterations = householdIterations;
                foreach (var area in CatchmentAreas)
                {
                    area.HouseholdStart(household);
                }
            }
        }

        [SubModelInformation(Description = "The different catchment areas to record for.")]
        public CatchmentArea[] CatchmentAreas;

        public void IterationFinished(int iteration, int totalIterations)
        {
            if (SaveThisIteration)
            {
                foreach (var area in CatchmentAreas)
                {
                    area.IterationFinished();
                }
            }
        }

        public void IterationStarting(int iteration, int totalIterations)
        {
            SaveThisIteration = SaveEveryTime | (iteration == totalIterations - 1);
            if (SaveThisIteration)
            {
                Modes = Root.AllModes.Select((m, i) => new KeyValuePair<ITashaMode, int>(m, i)).ToDictionary(e => e.Key, e => e.Value);
                foreach (var area in CatchmentAreas)
                {
                    area.IterationStarting();
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void HouseholdComplete(ITashaHousehold household, bool success)
        {
            if (SaveThisIteration)
            {
                foreach (var area in CatchmentAreas)
                {
                    area.HouseholdComplete(household, success);
                }
            }
        }
    }

}
