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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datastructure;
using TMG.Modes;
using XTMF;

namespace TMG.GTAModel.Tally
{
    public class StationAccessTally : DirectModeAggregationTally
    {
        [RunParameter("Count From Origin", true, "Should we be tallying from the origin to the intermediate zone" +
            "\r\nor should we be counting from the intermediate zone to the destination?")]
        public bool CountFromOrigin;

        [RunParameter("Count Line Hull", false, "Should we be tallying the line haul (access station to egress station).\r\nThis option takes priority over Count From Origin.")]
        public bool LineHaull;

        [RunParameter("Simulation Time", "7:00AM", typeof(Time), "The time of day to use for the split.")]
        public Time Time;

        public override void IncludeTally(float[][] currentTally)
        {
            var purposes = Root.Purpose;
            var zoneArray = Root.ZoneSystem.ZoneArray;
            var zones = zoneArray.GetFlatData();
            for(int purp = 0; purp < PurposeIndexes.Length; purp++)
            {
                var purpose = purposes[purp];
                for(int m = 0; m < ModeIndexes.Length; m++)
                {
                    var data = GetResult(purpose.Flows, ModeIndexes[m]);
                    if(data == null) continue;
                    var mode = (IStationCollectionMode)GetMode(ModeIndexes[m]);
                    if(LineHaull)
                    {
                        ComputeLineHaull(currentTally, zoneArray, zones, m, data);
                    }
                    else
                    {
                        mode.Access = CountFromOrigin;
                        if(CountFromOrigin)
                        {
                            ComputeFromOrigin(currentTally, zoneArray, zones, m, data);
                        }
                        else
                        {
                            ComputeFromDestination(currentTally, zoneArray, zones, m, data);
                        }
                    }
                }
            }
        }

        private void ComputeFromDestination(float[][] currentTally, SparseArray<IZone> zoneArray, IZone[] zones, int m, float[][] data)
        {
            Parallel.For(0, data.Length, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                delegate (int j)
            {
                for(int i = 0; i < data.Length; i++)
                {
                    if(data[i] == null || data[i][j] <= 0f) continue;
                    var choices = GetStationChoiceSplit(m, zones[i], zones[j]);
                    if(choices == null) continue;
                    // check for egress stations first
                    var stationZones = choices.Item2;
                    if(stationZones == null)
                    {
                        // if there are no egress stations, use the access stations
                        stationZones = choices.Item1;
                    }
                    var splits = choices.Item3;
                    var totalTrips = data[i][j];
                    var totalSplits = 0f;
                    for(int z = 0; z < stationZones.Length; z++)
                    {
                        if(stationZones[z] != null)
                        {
                            totalSplits += splits[z];
                        }
                    }
                    for(int z = 0; z < stationZones.Length; z++)
                    {
                        if(stationZones[z] == null) break;
                        var flatZoneNumber = zoneArray.GetFlatIndex(stationZones[z].ZoneNumber);
                        if(currentTally[flatZoneNumber] == null)
                        {
                            lock (data)
                            {
                                Thread.MemoryBarrier();
                                if(currentTally[flatZoneNumber] == null)
                                {
                                    currentTally[flatZoneNumber] = new float[data[i].Length];
                                }
                            }
                        }
                        // no lock needed since we are doing it parallel in the i, so there will be no conflicts
                        currentTally[flatZoneNumber][j] += totalTrips * (splits[z] / totalSplits);
                    }
                }
            });
        }

        private void ComputeFromOrigin(float[][] currentTally, SparseArray<IZone> zoneArray, IZone[] zones, int m, float[][] data)
        {
            Parallel.For(0, data.Length, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                delegate (int i)
            {
                if(data[i] == null) return;
                var tallyRow = currentTally[i];
                for(int j = 0; j < data[i].Length; j++)
                {
                    var totalTrips = data[i][j];
                    if(totalTrips <= 0f) continue;
                    var choices = GetStationChoiceSplit(m, zones[i], zones[j]);
                    if(choices == null) continue;
                    var stationZones = choices.Item1;
                    var splits = choices.Item3;
                    var totalSplits = 0f;
                    for(int z = 0; z < stationZones.Length; z++)
                    {
                        if(stationZones[z] != null)
                        {
                            totalSplits += splits[z];
                        }
                    }
                    for(int z = 0; z < stationZones.Length; z++)
                    {
                        if(stationZones[z] == null) break;
                        var flatZoneNumber = zoneArray.GetFlatIndex(stationZones[z].ZoneNumber);
                        // no lock needed since we are doing it parallel in the i, so there will be no conflicts
                        tallyRow[flatZoneNumber] += totalTrips * (splits[z] / totalSplits);
                    }
                }
            });
        }

        public float Sum(float[][] data)
        {
            return data.Sum(row => row == null ? 0f : row.Sum());
        }

        private void ComputeLineHaull(float[][] currentTally, SparseArray<IZone> zoneArray, IZone[] zones, int m, float[][] data)
        {
            // this can't be in parallel since we are writing to the some access and egress data entries
            for(int i = 0; i < data.Length; i++)
            {
                if(data[i] == null) continue;
                for(int j = 0; j < data[i].Length; j++)
                {
                    var totalTrips = data[i][j];
                    if(totalTrips <= 0f)
                    {
                        continue;
                    }
                    var choices = GetStationChoiceSplit(m, zones[i], zones[j]);
                    if(choices == null)
                    {
                        continue;
                    }
                    var accessStations = choices.Item1;
                    var egressStations = choices.Item2;
                    if(egressStations == null)
                    {
                        continue;
                    }
                    var splits = choices.Item3;
                    var totalSplits = 0f;
                    for(int z = 0; z < accessStations.Length; z++)
                    {
                        if(accessStations[z] != null)
                        {
                            totalSplits += splits[z];
                        }
                    }
                    for(int z = 0; z < accessStations.Length; z++)
                    {
                        if(accessStations[z] == null | egressStations[z] == null) break;
                        var accessZoneNumber = zoneArray.GetFlatIndex(accessStations[z].ZoneNumber);
                        var egressZoneNumber = zoneArray.GetFlatIndex(egressStations[z].ZoneNumber);
                        // no lock needed since we are doing it parallel in the i, so there will be no conflicts
                        currentTally[accessZoneNumber][egressZoneNumber] += totalTrips * (splits[z] / totalSplits);
                    }
                }
            }
        }

        private Tuple<IZone[], IZone[], float[]> GetStationChoiceSplit(int m, IZone origin, IZone destination)
        {
            // this mode is our base
            var mode = GetMode(ModeIndexes[m]);
            var cat = mode as IStationCollectionMode;
            if(cat == null)
            {
                throw new XTMFRuntimeException("The mode '" + mode.ModeName
                    + "' is not an TMG.Modes.IStationCollectionMode and can not be used with a StationAccessTally!");
            }
            return cat.GetSubchoiceSplit(origin, destination, Time);
        }
    }
}