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
using System.Linq;
using System.Text;
using XTMF;
using Tasha.Common;
using TMG;
using TMG.Input;
using System.IO;
using TMG.Emme;
using TMG.Functions;
using Datastructure;
using System.Threading.Tasks;

namespace Tasha.StationAccess
{
    public class ComputeStationCapacityFactor : IPostIteration
    {

        [RootModule]
        public ITravelDemandModel Root;

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

        [RunParameter("Station Zones", "9000-9999", typeof(RangeSet), "The range of zones that represent station centroids.")]
        public RangeSet StationZoneRanges;

        public sealed class TimePeriod : IModule
        {
            [RootModule]
            public ITravelDemandModel Root;

            [ParentModel]
            public ComputeStationCapacityFactor Parent;

            [SubModelInformation(Required = true, Description = "The location of the demand matrix to process. (.mtx file format)")]
            public FileLocation DemandMatrix;

            [SubModelInformation(Required = true, Description = "The location to save the new Capacity Factors for each station.")]
            public FileLocation CapacityFactorOutput;

            private float[] CapacityFactors;

            internal void Execute(int iteration)
            {
                BinaryHelpers.ExecuteReader((reader) =>
                {
                    EmmeMatrix matrix = new EmmeMatrix(reader);
                    switch (matrix.Type)
                    {
                        case EmmeMatrix.DataType.Float:
                            ProcessData(matrix.FloatData, iteration);
                            break;
                        default:
                            throw new XTMFRuntimeException("In '" + Name + "' the data type for the file '" + DemandMatrix + "' was not float!");
                    }
                }, DemandMatrix);
            }

            private void ProcessData(float[] autoTripMatrix, int iteration)
            {
                var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
                int[] zoneIndexForStation = Parent.AccessZoneIndexes;
                float[] accessStationCounts = new float[zoneIndexForStation.Length];
                // a fast way of tallying the station counts
                Parallel.For(0, zones.Length,
                    () => { return new float[zoneIndexForStation.Length]; },
                    (int i, ParallelLoopState state, float[] threadLocalStationAccessCounts) =>
                    {
                        var iOffset = i * zones.Length;
                        for (int j = 0; j < zoneIndexForStation.Length; j++)
                        {
                            threadLocalStationAccessCounts[j] += autoTripMatrix[iOffset + zoneIndexForStation[j]];
                        }
                        return threadLocalStationAccessCounts;
                    },
                    (float[] threadLocalAccessStationCounts) =>
                    {
                        lock (accessStationCounts)
                        {
                            for (int i = 0; i < accessStationCounts.Length; i++)
                            {
                                accessStationCounts[i] += threadLocalAccessStationCounts[i];
                            }
                        }
                    });
                var capacity = Parent.Capacity.GetFlatData();
                if (CapacityFactors == null || iteration == 0)
                {
                    CapacityFactors = new float[accessStationCounts.Length];
                    for (int i = 0; i < CapacityFactors.Length; i++)
                    {
                        CapacityFactors[i] = 0.0f;
                    }
                }
                using (var writer = new StreamWriter(CapacityFactorOutput))
                {
                    writer.WriteLine("Zone,Factor,Demand,Capacity");
                    for (int i = 0; i < accessStationCounts.Length; i++)
                    {
                        float stationCapacity = capacity[zoneIndexForStation[i]];
                        float capacityFactor;
                        if (ComputeStationCapacityFactor(accessStationCounts[i], stationCapacity, CapacityFactors[i], out capacityFactor))
                        {
                            CapacityFactors[i] = capacityFactor;
                            writer.Write(zones[zoneIndexForStation[i]].ZoneNumber);
                            writer.Write(',');
                            writer.Write(capacityFactor);
                            writer.Write(',');
                            writer.Write(accessStationCounts[i]);
                            writer.Write(',');
                            writer.WriteLine(stationCapacity);
                        }
                        else
                        {
                            CapacityFactors[i] = 1.0f;
                        }
                    }
                }
            }

            public bool ComputeStationCapacityFactor(float demand, float capacity, float previousCapacityFactor, out float capacityFactor)
            {
                if (capacity <= 0)
                {
                    capacityFactor = float.NaN;
                    return false;
                }
                capacityFactor = demand / capacity;
                return true;
            }

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        private IZone[] AccessZones;
        private int[] AccessZoneIndexes;
        private SparseArray<float> Capacity;

        [SubModelInformation(Required = true, Description = "Describes the station data.(Origin = Station, Data = capacity)")]
        public IReadODData<float> StationCapacity;

        private void LoadStationCapacity()
        {
            SparseArray<float> capacity = Root.ZoneSystem.ZoneArray.CreateSimilarArray<float>();
            foreach (var point in StationCapacity.Read())
            {
                if (!capacity.ContainsIndex(point.O))
                {
                    throw new XTMFRuntimeException("In '" + Name + "' we found an invalid zone '" + point.O + "' while reading in the station capacities!");
                }
                // use the log of capacity
                capacity[point.O] = point.Data;
            }
            Capacity = capacity;
        }

        private void LoadAccessZones()
        {
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var indexes = GetStationZones(StationZoneRanges, Capacity.GetFlatData(), zones);
            AccessZones = new IZone[indexes.Length];
            for (int i = 0; i < indexes.Length; i++)
            {
                AccessZones[i] = zones[indexes[i]];
            }
            AccessZoneIndexes = indexes;
        }

        internal static int[] GetStationZones(RangeSet stationRanges, float[] capacity, IZone[] zones)
        {
            List<int> validStationIndexes = new List<int>();
            for (int i = 0; i < zones.Length; i++)
            {
                if (capacity[i] > 0 && stationRanges.Contains(zones[i].ZoneNumber))
                {
                    validStationIndexes.Add(i);
                }
            }
            return validStationIndexes.ToArray();
        }

        [SubModelInformation(Required = false, Description = "Used to process each time period.")]
        public TimePeriod[] TimePeriods;

        public void Execute(int iterationNumber, int totalIterations)
        {
            // if we are 
            if (iterationNumber == 0)
            {
                LoadStationCapacity();
                LoadAccessZones();
            }
            // compute everything in parallel
            Parallel.ForEach(TimePeriods, (period) =>
            {
                period.Execute(iterationNumber);
            });
        }

        public void Load(IConfiguration config, int totalIterations)
        {

        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
