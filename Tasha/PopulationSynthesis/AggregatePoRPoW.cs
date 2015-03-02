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
using TMG;
using Datastructure;
using System.Threading.Tasks;
using TMG.Input;
using TMG.Functions;
using TMG.Functions.VectorHelper;

namespace Tasha.PopulationSynthesis
{
    public class AggregatePoRPoW : IDataSource<SparseTriIndex<float>>
    {
        [RootModule]
        public ITravelDemandModel Root;

        public bool Loaded
        {
            get; set;
        }

        public string Name
        {
            get; set;
        }

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

        public SparseTriIndex<float> GiveData()
        {
            return Data;
        }

        [SubModelInformation(Required = true, Description = "A SparseArray<float> for each zone of people in this category whom work.")]
        public IResource EmployedPopulationResidenceByZone;

        [SubModelInformation(Required = true, Description = "A SparseArray<float> for each zone of jobs in this category.")]
        public IResource JobsByZone;

        [SubModelInformation(Required = false, Description = "The k-factors to apply.")]
        public IResource KFactors;


        [RunParameter("Auto Network", "Auto", "The name of the auto network.")]
        public string AutoNetworkName;

        [RunParameter("Transit Network Name", "Transit", "The name of the transit network.")]
        public string TransitNetworkName;

        INetworkData AutoNetwork;

        INetworkData TransitNetwork;

        public class Segment : IModule
        {
            [RunParameter("Auto Time", 0.0f, "The weight of the auto travel time between zones.")]
            public float AutoTime;

            [RunParameter("Transit Time", 0.0f, "The total travel time by transit's weight.")]
            public float TransitTime;

            [RunParameter("Distance", 0.0f, "The weight applied to the distance in km.")]
            public float Distance;

            [RunParameter("MaxDistance", 10.0f, "The maximum distance to apply the distance factor.")]
            public float MaxDistance;

            [RunParameter("Transit Constant", 0.0f, "The constant applied for transit.")]
            public float TransitConstant;

            [RunParameter("Distance Constant", 0.0f, "The constant applied for distance.")]
            public float DistanceConstant;

            [RunParameter("Intrazonal", 0.0f, "A constant applied to intrazonals.")]
            public float IntrazonalConstant;

            [RunParameter("IntraPD", 0.0f, "A constant applied to intra-Planning-District linkages.")]
            public float IntraPDConstant;

            [RunParameter("Origin PDs", "1-48", "The planning districts contained in this segment.")]
            public RangeSet OriginPDs;

            [RunParameter("Destination PDs", "1-48", "The planning districts contained in this segment.")]
            public RangeSet DestinationPDs;

            [RunParameter("Saturated Household", 0.0f, "The weight added for worker category 2's auto utility.")]
            public float SaturatedVehicles;

            public string Name { get; set; }

            public float Progress { get { return 0f; } }

            public Tuple<byte, byte, byte> ProgressColour { get { return null; } }

            public bool RuntimeValidation(ref string error) { return true; }
        }

        public Segment[] Segments;

        [RunParameter("Time of Day", "7:00AM", "The time we will use to calculate the utilities.")]
        public Time TimeOfDay;

        /// <summary>
        /// Calculate the utility between two zones
        /// </summary>
        /// <param name="zoneO">The flat origin index</param>
        /// <param name="zoneJ">The flat destination index</param>
        /// <returns>The utility between the two zones.</returns>
        public float CalculateUtilityToE(int pdO, int pdD, int zoneO, int zoneD, int workerIndex, float distance)
        {
            var segment = GetSegment(pdO, pdD);
            if(segment == null) return 0;
            double utility = 0.0;
            // Worker Categories:
            // 0 = No Car / No License
            // 1 = Less cars than people with licenses
            // 2 = More or equal cars to persons with licenses
            float time;
            // auto (only apply auto for people who have cars)
            time = AutoNetwork.TravelTime(zoneO, zoneD, TimeOfDay).ToMinutes();
            if(workerIndex > 0)
            {
                utility = Math.Exp(segment.AutoTime * time +
                    (workerIndex == 2 ? segment.SaturatedVehicles : 0));
            }
            // transit
            time = TransitNetwork.TravelTime(zoneO, zoneD, TimeOfDay).ToMinutes();
            if(time > 0)
            {
                utility += Math.Exp(segment.TransitTime * time + segment.TransitConstant);
            }
            // distance
            if(distance < segment.MaxDistance)
            {
                utility += Math.Exp(segment.Distance * distance + segment.DistanceConstant);
            }
            var constants = 0.0;
            if(zoneO == zoneD) constants += segment.IntrazonalConstant;
            if(pdO == pdD) constants += segment.IntraPDConstant;
            if(constants > 0.0f)
            {
                return (float)(utility * Math.Exp(constants));
            }
            return (float)utility;
        }

        public int[][] HighPerformanceMap;


        private Segment GetSegment(int pdO, int pdD)
        {
            var segments = Segments;
            if(HighPerformanceMap != null)
            {
                var index = HighPerformanceMap[pdO][pdD];
                return index >= 0 ? segments[index] : null;
            }
            else
            {
                for(int i = 0; i < segments.Length; i++)
                {
                    if(segments[i].OriginPDs.Contains(pdO)
                        && segments[i].DestinationPDs.Contains(pdD))
                    {
                        return segments[i];
                    }
                }
            }
            return null;
        }

        private SparseTriIndex<float> Data;

        [RunParameter("Epsilon", 0.01f, "The maximum amount of error before stopping")]
        public float Epsilon;

        [RunParameter("Max Iterations", 100, "The maximum number of iterations before we stop.")]
        public int MaxIterations;

        private float[] LocalData;

        private int[] PlanningDistricts;

        [RunParameter("Keep Local Data", false, "Turn this on to trade slight computational speed for additional memory usage, this is only advised for doing estimation.")]
        public bool KeepLocalData;

        public void LoadData()
        {
            var zoneArray = Root.ZoneSystem.ZoneArray;
            var zones = zoneArray.GetFlatData();
            var data = LocalData;
            if(data == null)
            {
                LocalData = data = new float[zones.Length * zones.Length * NumberOfWorkerCategories];
            }
            var distances = Root.ZoneSystem.Distances.GetFlatData();
            var pds = PlanningDistricts;
            if(pds == null)
            {
                PlanningDistricts = pds = zones.Select((zone) => zone.PlanningDistrict).ToArray();
            }
            if(KeepLocalData)
            {
                CreateHighPerformanceLookup(zoneArray);
            }
            float[] workerSplits = LoadWorkerCategories(zones, zoneArray);
            SparseTwinIndex<float> kFactors = null;
            if(KFactors != null)
            {
                kFactors = KFactors.AquireResource<SparseTwinIndex<float>>();
                Parallel.For(0, zones.Length, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, (int i) =>
                {
                    var distanceRow = distances[i];
                    var iPD = pds[i];
                    for(int k = 0; k < NumberOfWorkerCategories; k++)
                    {
                        int offset = k * zones.Length * zones.Length + i * zones.Length;
                        for(int j = 0; j < zones.Length; j++)
                        {
                            // use distance in km
                            data[offset + j] = kFactors[iPD, pds[j]] * CalculateUtilityToE(iPD, pds[j], i, j, k, distanceRow[j] * 0.001f);
                        }
                    }
                });
                KFactors.ReleaseResource();
            }
            else
            {
                Parallel.For(0, zones.Length, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, (int i) =>
                {
                    var distanceRow = distances[i];
                    var iPD = pds[i];
                    for(int k = 0; k < NumberOfWorkerCategories; k++)
                    {
                        int offset = k * zones.Length * zones.Length + i * zones.Length;
                        for(int j = 0; j < zones.Length; j++)
                        {
                            // use distance in km
                            data[offset + j] = CalculateUtilityToE(iPD, pds[j], i, j, k, distanceRow[j] * 0.001f);
                        }
                    }
                });
            }

            SparseArray<float> employmentSeekers = EmployedPopulationResidenceByZone.AquireResource<SparseArray<float>>();
            var jobs = CreateNormalizedJobs(employmentSeekers, JobsByZone.AquireResource<SparseArray<float>>().GetFlatData());
            var results = TMG.Functions.GravityModel3D.ProduceFlows(MaxIterations, Epsilon,
                                CreateWorkersByCategory(employmentSeekers, workerSplits),
                                jobs, data,
                                NumberOfWorkerCategories, zones.Length);
            Data = ConvertResults(results, zoneArray);
            if(!KeepLocalData)
            {
                LocalData = null;
                PlanningDistricts = null;
                WorkerCategories = null;
            }
            Loaded = true;
        }

        private void CreateHighPerformanceLookup(SparseArray<IZone> zoneArray)
        {
            if(HighPerformanceMap == null)
            {
                var pds = TMG.Functions.ZoneSystemHelper.CreatePDArray<int>(zoneArray);
                var pdIndexes = pds.ValidIndexArray();
                HighPerformanceMap = new int[pdIndexes.Max() + 1][];
                Parallel.For(0, HighPerformanceMap.Length, (int i) =>
                {
                    var row = HighPerformanceMap[i] = new int[HighPerformanceMap.Length];
                    for(int j = 0; j < row.Length; j++)
                    {
                        int index = -1;
                        for(int k = 0; k < Segments.Length; k++)
                        {
                            if(Segments[k].OriginPDs.Contains(i) && Segments[k].DestinationPDs.Contains(j))
                            {
                                index = k;
                                break;
                            }
                        }
                        row[j] = index;
                    }
                });
            }
        }

        private float[] CreateNormalizedJobs(SparseArray<float> employmentSeekers, float[] employment)
        {
            var pop = employmentSeekers.GetFlatData();
            var totalPop = pop.Sum();
            var totalEmployment = employment.Sum();
            var balanceFactor = totalPop / totalEmployment;
            var ret = new float[employment.Length];
            if(IsHardwareAccelerated)
            {
                VectorMultiply(ret, 0, employment, 0, balanceFactor, ret.Length);
            }
            else
            {
                for(int i = 0; i < employment.Length; i++)
                {
                    ret[i] = employment[i] * balanceFactor;
                }
            }
            return ret;
        }

        private float[] LocalWorkerCategories;

        private float[] CreateWorkersByCategory(SparseArray<float> occPopByZone, float[] workerSplits)
        {
            if(KeepLocalData && LocalWorkerCategories != null)
            {
                return LocalWorkerCategories;
            }
            var pop = occPopByZone.GetFlatData();
            var ret = new float[NumberOfWorkerCategories * pop.Length];
            for(int workerCategory = 0; workerCategory < NumberOfWorkerCategories; workerCategory++)
            {
                int WorkerCategoryOffset = workerCategory * pop.Length;
                if(IsHardwareAccelerated)
                {
                    VectorMultiply(ret, WorkerCategoryOffset, pop, 0, workerSplits, WorkerCategoryOffset, pop.Length);
                }
                else
                {
                    for(int i = 0; i < pop.Length; i++)
                    {
                        ret[i + WorkerCategoryOffset] = pop[i] * workerSplits[i + WorkerCategoryOffset];
                    }
                }
            }
            if(KeepLocalData)
            {
                LocalWorkerCategories = ret;
            }
            return ret;
        }

        private SparseTriIndex<float> ConvertResults(float[] results, SparseArray<IZone> zoneSystem)
        {
            SparseTriIndex<float> ret = Data;
            // first create the datastructure
            if(ret == null)
            {
                ret = SparseTriIndex<float>.CreateSimilarArray(new SparseArray<int>(new SparseIndexing()
                {
                    Indexes = new SparseSet[]
                            { new SparseSet()
                                { BaseLocation = 0,
                                    Start = 0,
                                    Stop = NumberOfWorkerCategories - 1 }
                            }
                }), zoneSystem, zoneSystem);
            }
            // now fill it
            var r = ret.GetFlatData();
            var numberOfZones = r[0].Length;
            Parallel.For(0, numberOfZones, (int i) =>
            {
                for(int workerCategory = 0; workerCategory < r.Length; workerCategory++)
                {
                    var workerCategoryMatrix = r[workerCategory];
                    var pos = sizeof(float) * ((workerCategoryMatrix.Length * workerCategoryMatrix.Length) * workerCategory + workerCategoryMatrix.Length * i);
                    Buffer.BlockCopy(results, pos, workerCategoryMatrix[i], 0, numberOfZones);
                }
            });
            return ret;
        }

        [SubModelInformation(Required = true, Description = "The file containing the worker category splits (Zone,WCat,P)")]
        public FileLocation WorkerCategorySplits;

        const int NumberOfWorkerCategories = 3;

        public float[] WorkerCategories;

        [RunParameter("Reload Worker Categories", false, "Should we load worker categories every time this data source is reloaded?")]
        public bool ReloadWorkerCategories;


        private float[] LoadWorkerCategories(IZone[] zones, SparseArray<IZone> zoneArray)
        {
            if((!ReloadWorkerCategories) & (WorkerCategories != null))
            {
                return WorkerCategories;
            }
            var ret = new float[zones.Length * NumberOfWorkerCategories];
            using (CsvReader reader = new CsvReader(WorkerCategorySplits))
            {
                //burn header
                reader.LoadLine(out int columns);
                // read data
                while(reader.LoadLine(out columns))
                {
                    if(columns < 3)
                    {
                        continue;
                    }
                    reader.Get(out int zone, 0);
                    reader.Get(out int category, 1);
                    reader.Get(out float probability, 2);
                    zone = zoneArray.GetFlatIndex(zone);
                    // categories are 1 indexed however we want 0 indexed
                    category -= 1;
                    if(zone < 0 | category < 0 | category >= NumberOfWorkerCategories) continue;
                    ret[zone + (zones.Length * category)] = probability;
                }
            }
            return (WorkerCategories = ret);
        }

        public bool RuntimeValidation(ref string error)
        {
            // load auto network + transit network
            foreach(var network in Root.NetworkData)
            {
                var networkName = network.NetworkType;
                if(networkName == AutoNetworkName)
                {
                    AutoNetwork = network;
                }
                if(networkName == TransitNetworkName)
                {
                    TransitNetwork = network;
                }
            }
            if(AutoNetwork == null)
            {
                error = "In '" + Name + "' the auto network could not be found!";
                return false;
            }
            if(TransitNetwork == null)
            {
                error = "In '" + Name + "' the transit network could not be found!";
                return false;
            }
            // check resources for proper types
            if(!EmployedPopulationResidenceByZone.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the Employed Population Residence By Zone is not of type SparseArray<float>!";
                return false;
            }
            if(!JobsByZone.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the Jobs By Zone is not of type SparseArray<float>!";
                return false;
            }
            if(KFactors != null && !KFactors.CheckResourceType<SparseTwinIndex<float>>())
            {
                error = "In '" + Name + "' the KFactors are not of type SparseTwinIndex<float>!";
                return false;
            }
            return true;
        }

        public void UnloadData()
        {
            Loaded = false;
        }
    }
}
