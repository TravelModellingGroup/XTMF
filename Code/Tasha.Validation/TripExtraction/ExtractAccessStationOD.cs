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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tasha.ModeChoice;
using Tasha.Common;
using XTMF;
using TMG.Input;
using TMG;
using Datastructure;
using System.Runtime.CompilerServices;
using System.IO;

namespace Tasha.Validation.TripExtraction
{

    public class ExtractAccessStationOD : IPostHouseholdIteration
    {
        [RootModule]
        public ITashaRuntime Root;

        [RunParameter("Start Time", "6:00", typeof(Time), "The start time for this scenario")]
        public Time StartTime;

        [RunParameter("End Time", "9:00", typeof(Time), "The end time for this scenario")]
        public Time EndTime;

        [RunParameter("Minimum Age", 11, "The youngest a person can be and still be recorded.")]
        public int MinimumAge;

        [SubModelInformation(Required = true, Description = "Where do you want to save the binary matrix?")]
        public FileLocation SaveTo;

        [RunParameter("Attribute", "AccessStation", "The name of the attribute that contains the information of which access station was used.")]
        public string AttributeName;

        [SubModelInformation(Description = "The modes to check for.")]
        public EMME.CreateEmmeBinaryMatrixWithPassenger.ModeLink[] Modes;

        [RunParameter("Station Range", "{9000-9999}", typeof(RangeSet), "The range of zones that contain stations.")]
        public RangeSet StationRange;

        private ITashaMode[] _Modes;

        private SparseArray<IZone> Zones;

        private int[] StationIndex;

        public float[][][] Results;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public void HouseholdComplete(ITashaHousehold household, bool success)
        {
        }

        public enum ToRecord
        {
            Access,
            Egress,
            AccessAndEgress
        }

        [RunParameter("What To Record", "Access", typeof(ToRecord), "The type of information to record.")]
        public ToRecord WhatToRecord;

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {
            var att = AttributeName;
            var toRecord = WhatToRecord;
            foreach (var person in household.Persons)
            {
                var exp = person.ExpansionFactor;
                foreach (var tripChain in person.TripChains)
                {
                    var stationZone = tripChain[att] as IZone;
                    if (stationZone != null)
                    {
                        bool first = true;
                        foreach (var trip in tripChain.Trips)
                        {
                            if (Array.IndexOf(_Modes, trip.Mode) >= 0)
                            {
                                if (first)
                                {
                                    if (toRecord == ToRecord.Access || toRecord == ToRecord.AccessAndEgress)
                                    {
                                        AddToMatrix(trip.TripStartTime, exp, Zones.GetFlatIndex(trip.OriginalZone.ZoneNumber),
                                            Zones.GetFlatIndex(trip.DestinationZone.ZoneNumber),
                                            stationZone.ZoneNumber);
                                    }
                                    first = false;
                                }
                                else
                                {
                                    if (toRecord == ToRecord.Egress || toRecord == ToRecord.AccessAndEgress)
                                    {
                                        AddToMatrix(trip.TripStartTime, exp, Zones.GetFlatIndex(trip.OriginalZone.ZoneNumber),
                                            Zones.GetFlatIndex(trip.DestinationZone.ZoneNumber),
                                            stationZone.ZoneNumber);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddToMatrix(Time startTime, float expFactor, int originIndex, int destinationIndex, int StationZoneNumber)
        {
            if (startTime >= StartTime & startTime < EndTime)
            {
                var index = Array.IndexOf(StationIndex, StationZoneNumber);
                var result = Results[index];
                lock (result)
                {
                    var row = result[originIndex];
                    row[destinationIndex] += expFactor;
                }
            }
        }

        public void HouseholdStart(ITashaHousehold household, int householdIterations)
        {
        }

        public void IterationFinished(int iteration, int totalIterations)
        {
            var results = Results;
            var zoneNumbers = Zones.GetFlatData().Select(z => z.ZoneNumber.ToString()).ToArray();
            var stationIndexStr = StationIndex.Select(z => z.ToString()).ToArray();
            using (var writer = new StreamWriter(SaveTo))
            {
                writer.WriteLine("Station,Origin,Destination,Trips");
                for (int sIndex = 0; sIndex < results.Length; sIndex++)
                {
                    for (int o = 0; o < results[sIndex].Length; o++)
                    {
                        for (int d = 0; d < results[sIndex][o].Length; d++)
                        {
                            if (results[sIndex][o][d] > 0.0f)
                            {
                                writer.Write(stationIndexStr[sIndex]);
                                writer.Write(',');
                                writer.Write(zoneNumbers[o]);
                                writer.Write(',');
                                writer.Write(zoneNumbers[d]);
                                writer.Write(',');
                                writer.WriteLine(results[sIndex][o][d]);
                            }
                        }
                    }
                }
            }
        }

        public void IterationStarting(int iteration, int totalIterations)
        {
            _Modes = Modes.Select(m => m.Mode).ToArray();
            Zones = Root.ZoneSystem.ZoneArray;
            List<float[][]> results = new List<float[][]>();
            List<int> stationIndexes = new List<int>();
            foreach (var range in StationRange)
            {
                for (int i = range.Start; i < range.Stop; i++)
                {
                    if (Zones.GetFlatIndex(i) >= 0)
                    {
                        results.Add(CreateODMatrix());
                        stationIndexes.Add(i);
                    }
                }
            }
            StationIndex = stationIndexes.ToArray();
            Results = results.ToArray();
        }

        private float[][] CreateODMatrix()
        {
            float[][] ret = new float[Zones.Count][];
            for (int i = 0; i < Zones.Count; i++)
            {
                ret[i] = new float[Zones.Count];
            }
            return ret;
        }

        public bool RuntimeValidation(ref string error)
        {
            if (Modes.Length <= 0)
            {
                error = $"In {Name} no modes were selected for analysis.  At least one is required.";
                return false;
            }
            return true;
        }
    }

}
