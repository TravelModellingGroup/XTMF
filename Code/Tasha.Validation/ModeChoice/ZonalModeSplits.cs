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
using System.Text;
using System.Threading;
using Datastructure;
using Tasha.Common;
using TMG;
using TMG.Input;
using XTMF;

namespace Tasha.Validation.ModeChoice
{
    public class ZonalModeSplits : IPostHouseholdIteration
    {
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

        public void HouseholdComplete(ITashaHousehold household, bool success)
        {

        }

        [RunParameter("Start time", "6:00AM", typeof(Time), "The start time to capture (inclusive).")]
        public Time StartTime;

        [RunParameter("End time", "9:00AM", typeof(Time), "The end time (exclusive) to capture.")]
        public Time EndTime;

        [RunParameter("Minimum Age", 11, "The minimum age allowed for the person's trip to _Count.")]
        public int MinimumAge;

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {
            foreach(var person in household.Persons)
            {
                if(person.Age < MinimumAge)
                {
                    continue;
                }
                var expansionFactor = person.ExpansionFactor;
                foreach(var tripChain in person.TripChains)
                {
                    foreach(var trip in tripChain.Trips)
                    {
                        var tripTime = trip.TripStartTime;
                        if(tripTime >= StartTime && tripTime < EndTime)
                        {
                            var index = GetIndex(trip.Mode);
                            var oIndex = ZoneSystem.GetFlatIndex(trip.OriginalZone.ZoneNumber);
                            var dIndex = ZoneSystem.GetFlatIndex(trip.DestinationZone.ZoneNumber);
                            if(oIndex >= 0 & dIndex >= 0)
                            {
                                bool taken = false;
                                DataLock[index].Enter(ref taken);
                                if(taken)
                                {
                                    Data[index][oIndex][dIndex] += expansionFactor;
                                    DataLock[index].Exit(true);
                                }
                            }
                        }
                    }
                }
            }
        }

        private int GetIndex(ITashaMode mode)
        {
            for(int i = 0; i < Modes.Length; i++)
            {
                if(Modes[i] == mode)
                {
                    return i;
                }
            }
            throw new XTMFRuntimeException("In '" + Name + "' we were unable to find a mode called '" + mode == null ? "a null mode" : mode.ModeName + "'");
        }

        public void HouseholdStart(ITashaHousehold household, int householdIterations)
        {

        }

        [SubModelInformation(Required = true, Description = "The location to save the file.")]
        public FileLocation SaveLocation;

        public void IterationFinished(int iteration, int totalIterations)
        {
            var zones = ZoneSystem.ValidIndexArray();
            using (StreamWriter writer = new StreamWriter(SaveLocation))
            {
                writer.WriteLine("Mode,Origin,Destination,ExpandedTrips");
                for(int m = 0; m < Data.Length; m++)
                {
                    string modeName = Modes[m].ModeName + ",";
                    var oRow = Data[m];
                    for(int o = 0; o < oRow.Length; o++)
                    {
                        var dRow = oRow[o];
                        for(int d = 0; d < dRow.Length; d++)
                        {
                            if(dRow[d] > 0)
                            {
                                // this includes the comma already
                                writer.Write(modeName);
                                writer.Write(zones[o]);
                                writer.Write(',');
                                writer.Write(zones[d]);
                                writer.Write(',');
                                writer.WriteLine(dRow[d]);
                            }
                        }
                    }
                }
            }
            Data = null;
        }

        [RootModule]
        public ITashaRuntime Root;

        private SparseArray<IZone> ZoneSystem;
        private ITashaMode[] Modes;
        private float[][][] Data;
        private SpinLock[] DataLock;

        public void IterationStarting(int iteration, int totalIterations)
        {
            var zoneSystem = Root.ZoneSystem.ZoneArray;
            var zones = zoneSystem.GetFlatData();
            var modes = Root.AllModes.ToArray();
            // setup the data collection
            var data = new float[modes.Length][][];
            for(int i = 0; i < data.Length; i++)
            {
                var row = data[i] = new float[zones.Length][];
                for(int j = 0; j < row.Length; j++)
                {
                    row[j] = new float[zones.Length];
                }
            }
            // setup the instance variables
            DataLock = new SpinLock[modes.Length];
            for(int i = 0; i < DataLock.Length; i++)
            {
                DataLock[i] = new SpinLock(false);
            }
            Data = data;
            Modes = modes;
            ZoneSystem = zoneSystem;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
