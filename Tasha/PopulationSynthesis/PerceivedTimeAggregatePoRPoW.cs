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
using XTMF;
using TMG;
using Datastructure;
using System.Threading.Tasks;
using TMG.Input;
using TMG.Functions;
using System.IO;

namespace Tasha.PopulationSynthesis;

// ReSharper disable once InconsistentNaming
public class PerceivedTimeAggregatePoRPoW : IDataSource<SparseTriIndex<float>>
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

    ITripComponentData TransitNetwork;

    public sealed class Segment : IModule
    {
        [RunParameter("SegmentConstant", 0.0f, "The constant for this segment")]
        public float SegmentConstant
        {
            get
            {
                return _SegmentConstant;
            }
            set
            {
                _SegmentConstant = value;
                ExpSegmentConstant = (float)Math.Exp(value);
            }
        }

        private float _SegmentConstant;

        internal float ExpSegmentConstant;

        [RunParameter("Auto Time", 0.0f, "The weight of the auto travel time between zones.")]
        public float AutoTime;

        [RunParameter("Passenger Time", 0.0f, "The weight of the auto travel time between zones.")]
        public float PassengerTime;

        [RunParameter("Transit Time", 0.0f, "The total travel time by transit's weight.")]
        public float TransitTime;

        [RunParameter("Transit Constant", 0.0f, "The constant applied for transit.")]
        public float TransitConstant;

        [RunParameter("Include Distance Term", false, "Should distance be included in the model?")]
        public bool IncludeDistanceTerm;

        [RunParameter("Distance Constant", 0.0f, "The constant applied for distance.")]
        public float DistanceConstant;

        [RunParameter("Distance Factor", 0.0f, "The constant applied against the zonal distances.")]
        public float DistanceFactor;

        private float _IntrazonalConstant;
        [RunParameter("Intrazonal", 0.0f, "A constant applied to intrazonals.")]
        public float IntrazonalConstant
        {
            get
            {
                return _IntrazonalConstant;
            }
            set
            {
                _IntrazonalConstant = value;
                ExpIntrazonalConstant = (float)Math.Exp(value);
            }
        }

        /// <summary>
        /// Use this value to help remove the cost of Exp
        /// </summary>
        internal float ExpIntrazonalConstant;

        private float _IntraPDConstant;
        [RunParameter("IntraPD", 0.0f, "A constant applied to intra-Planning-District linkages.")]
        public float IntraPDConstant
        {
            get
            {
                return _IntraPDConstant;
            }
            set
            {
                _IntraPDConstant = value;
                ExpIntraPDConstant = (float)Math.Exp(value);
            }
        }

        /// <summary>
        /// Use this value to help remove the cost of Exp
        /// </summary>
        internal float ExpIntraPDConstant;

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
    /// <param name="pdD"></param>
    /// <param name="zoneO">The flat origin index</param>
    /// <param name="pdO"></param>
    /// <param name="zoneD"></param>
    /// <param name="workerIndex"></param>
    /// <returns>The utility between the two zones.</returns>
    private float CalculateUtilityToE(int pdO, int pdD, int zoneO, int zoneD, int workerIndex)
    {
        var segment = GetSegment(pdO, pdD);
        if (segment == null)
        {
            return 0;
        }
        // Worker Categories:
        // 0 = No Car / No License
        // 1 = Less cars than people with licenses
        // 2 = More or equal cars to persons with licenses
        float perceivedTime = AutoNetwork.TravelTime(zoneO, zoneD, TimeOfDay).ToMinutes();
        var utility = Math.Exp((workerIndex == 0 ? segment.PassengerTime : segment.AutoTime) * perceivedTime +
                                  (workerIndex == 2 ? segment.SaturatedVehicles : 0));
        // transit
        TransitNetwork.GetAllData(zoneO, zoneD, TimeOfDay, out float trueTime, out float walk, out float wait, out perceivedTime, out float cost);
        if (perceivedTime > 0)
        {
            utility += Math.Exp(segment.TransitTime * perceivedTime + segment.TransitConstant);
        }
        if (segment.IncludeDistanceTerm)
        {
            utility += Math.Exp(segment.DistanceConstant + segment.DistanceFactor * Root.ZoneSystem.Distances.GetFlatData()[zoneO][zoneD]);
        }
        var constants = segment.ExpSegmentConstant;
        if (zoneO == zoneD) constants *= segment.ExpIntrazonalConstant;
        if (pdO == pdD) constants *= segment.ExpIntraPDConstant;
        return (float)(utility * constants);
    }

    public int[][] HighPerformanceMap;


    private Segment GetSegment(int pdO, int pdD)
    {
        var segments = Segments;
        if (HighPerformanceMap != null)
        {
            var index = HighPerformanceMap[pdO][pdD];
            return index >= 0 ? segments[index] : null;
        }
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i].OriginPDs.Contains(pdO)
                && segments[i].DestinationPDs.Contains(pdD))
            {
                return segments[i];
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
        if (data == null)
        {
            LocalData = data = new float[zones.Length * zones.Length * NumberOfWorkerCategories];
        }
        var pds = PlanningDistricts;
        if (pds == null)
        {
            PlanningDistricts = pds = zones.Select((zone) => zone.PlanningDistrict).ToArray();
        }
        if (KeepLocalData)
        {
            CreateHighPerformanceLookup(zoneArray);
        }
        float[] workerSplits = LoadWorkerCategories(zones, zoneArray);
        SparseTwinIndex<float> kFactors;
        if (KFactors != null)
        {
            kFactors = KFactors.AcquireResource<SparseTwinIndex<float>>();
            Parallel.For(0, zones.Length, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
            {
                var iPD = pds[i];
                for (int k = 0; k < NumberOfWorkerCategories; k++)
                {
                    int offset = k * zones.Length * zones.Length + i * zones.Length;
                    for (int j = 0; j < zones.Length; j++)
                    {
                        // use distance in km
                        data[offset + j] = kFactors[iPD, pds[j]] * CalculateUtilityToE(iPD, pds[j], i, j, k);
                    }
                }
            });
            KFactors.ReleaseResource();
        }
        else
        {
            Parallel.For(0, zones.Length, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
            {
                var iPD = pds[i];
                for (int k = 0; k < NumberOfWorkerCategories; k++)
                {
                    int offset = k * zones.Length * zones.Length + i * zones.Length;
                    for (int j = 0; j < zones.Length; j++)
                    {
                        // use distance in km
                        data[offset + j] = CalculateUtilityToE(iPD, pds[j], i, j, k);
                    }
                }
            });
        }

        SparseArray<float> employmentSeekers = EmployedPopulationResidenceByZone.AcquireResource<SparseArray<float>>();
        var jobs = CreateNormalizedJobs(employmentSeekers, JobsByZone.AcquireResource<SparseArray<float>>().GetFlatData());
        var results = GravityModel3D.ProduceFlows(MaxIterations, Epsilon,
                            CreateWorkersByCategory(employmentSeekers, workerSplits),
                            jobs, data,
                            NumberOfWorkerCategories, zones.Length);
        if (Root is IIterativeModel itModel && itModel.CurrentIteration > 0)
        {
            AverageResults(results, PreviousResults);
        }
        Data = ConvertResults(results, zoneArray);
        PreviousResults = results;
        if (!KeepLocalData)
        {
            LocalData = null;
            PlanningDistricts = null;
            WorkerCategories = null;
        }
        Loaded = true;
    }

    private static void AverageResults(float[] results, float[] previousResults)
    {
        VectorHelper.Average(results, 0, results, 0, previousResults, 0, results.Length);
    }

    private float[] PreviousResults;

    private void CreateHighPerformanceLookup(SparseArray<IZone> zoneArray)
    {
        if (HighPerformanceMap == null)
        {
            var pds = ZoneSystemHelper.CreatePdArray<int>(zoneArray);
            var pdIndexes = pds.ValidIndexArray();
            HighPerformanceMap = new int[pdIndexes.Max() + 1][];
            Parallel.For(0, HighPerformanceMap.Length, i =>
            {
                var row = HighPerformanceMap[i] = new int[HighPerformanceMap.Length];
                for (int j = 0; j < row.Length; j++)
                {
                    int index = -1;
                    for (int k = 0; k < Segments.Length; k++)
                    {
                        if (Segments[k].OriginPDs.Contains(i) && Segments[k].DestinationPDs.Contains(j))
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
        var totalPop = VectorHelper.Sum(pop, 0, pop.Length);
        var totalEmployment = VectorHelper.Sum(employment, 0, employment.Length);
        var ret = new float[employment.Length];
        VectorHelper.Multiply(ret, 0, employment, 0, totalPop / totalEmployment, ret.Length);
        return ret;
    }

    private float[] LocalWorkerCategories;

    private float[] CreateWorkersByCategory(SparseArray<float> occPopByZone, float[] workerSplits)
    {
        if (KeepLocalData && LocalWorkerCategories != null)
        {
            return LocalWorkerCategories;
        }
        var pop = occPopByZone.GetFlatData();
        var ret = new float[NumberOfWorkerCategories * pop.Length];
        for (int workerCategory = 0; workerCategory < NumberOfWorkerCategories; workerCategory++)
        {
            int workerCategoryOffset = workerCategory * pop.Length;
            VectorHelper.Multiply(ret, workerCategoryOffset, pop, 0, workerSplits, workerCategoryOffset, pop.Length);
        }
        if (KeepLocalData)
        {
            LocalWorkerCategories = ret;
        }
        return ret;
    }

    private SparseTriIndex<float> ConvertResults(float[] results, SparseArray<IZone> zoneSystem)
    {
        SparseTriIndex<float> ret = Data ?? SparseTriIndex<float>.CreateSimilarArray(new SparseArray<int>(new SparseIndexing()
            {
                Indexes =
                        [ new SparseSet()
                            { BaseLocation = 0,
                                Start = 0,
                                Stop = NumberOfWorkerCategories - 1 }
                        ]
            }), zoneSystem, zoneSystem);
        // now fill it
        var r = ret.GetFlatData();
        var numberOfZones = r[0].Length;
        Parallel.For(0, numberOfZones, i =>
        {
            for (int workerCategory = 0; workerCategory < r.Length; workerCategory++)
            {
                var workerCategoryMatrix = r[workerCategory];
                var pos = sizeof(float) * ((workerCategoryMatrix.Length * workerCategoryMatrix.Length) * workerCategory + numberOfZones * i);
                Buffer.BlockCopy(results, pos, workerCategoryMatrix[i], 0, numberOfZones * sizeof(float));
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
        if ((!ReloadWorkerCategories) & (WorkerCategories != null))
        {
            return WorkerCategories;
        }
        var ret = new float[zones.Length * NumberOfWorkerCategories];
        try
        {
            using CsvReader reader = new(WorkerCategorySplits);
            //burn header
            reader.LoadLine(out int columns);
            // read data
            while (reader.LoadLine(out columns))
            {
                if (columns < 3)
                {
                    continue;
                }
                reader.Get(out int zone, 0);
                reader.Get(out int category, 1);
                reader.Get(out float probability, 2);
                zone = zoneArray.GetFlatIndex(zone);
                // categories are 1 indexed however we want 0 indexed
                category -= 1;
                if (zone < 0 | category < 0 | category >= NumberOfWorkerCategories) continue;
                ret[zone + (zones.Length * category)] = probability;
            }
        }
        catch(IOException e)
        {
            throw new XTMFRuntimeException(this, e, $"Unable to read worker category file at '{WorkerCategorySplits.GetFilePath()}'. {e.Message}");
        }
        return (WorkerCategories = ret);
    }

    public bool RuntimeValidation(ref string error)
    {
        // load auto network + transit network
        foreach (var network in Root.NetworkData)
        {
            var networkName = network.NetworkType;
            if (networkName == AutoNetworkName)
            {
                AutoNetwork = network;
            }
            if (networkName == TransitNetworkName)
            {
                TransitNetwork = network as ITripComponentData;
            }
        }
        if (AutoNetwork == null)
        {
            error = "In '" + Name + "' the auto network could not be found!";
            return false;
        }
        if (TransitNetwork == null)
        {
            error = "In '" + Name + "' the transit network could not be found!";
            return false;
        }
        // check resources for proper types
        if (!EmployedPopulationResidenceByZone.CheckResourceType<SparseArray<float>>())
        {
            error = "In '" + Name + "' the Employed Population Residence By Zone is not of type SparseArray<float>!";
            return false;
        }
        if (!JobsByZone.CheckResourceType<SparseArray<float>>())
        {
            error = "In '" + Name + "' the Jobs By Zone is not of type SparseArray<float>!";
            return false;
        }
        if (KFactors != null && !KFactors.CheckResourceType<SparseTwinIndex<float>>())
        {
            error = "In '" + Name + "' the KFactors are not of type SparseTwinIndex<float>!";
            return false;
        }
        return true;
    }

    public void UnloadData()
    {
        Loaded = false;
        if (!KeepLocalData)
        {
            Data = null;
        }
    }
}
