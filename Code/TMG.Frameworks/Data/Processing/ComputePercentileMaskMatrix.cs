/*
    Copyright 2025 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.Runtime.InteropServices;
using TMG.Data;
using XTMF;

namespace TMG.Frameworks.Data.Processing;

[ModuleInformation(Description = "This module allows you to create a mask matrix where a percentile of the OD pairs (by selected spatial segmentation) is selected. Pairs will be added until it would exceed the value, the final value is included.")]
public sealed class ComputePercentileMaskMatrix : IDataSource<SparseTwinIndex<float>>
{
    [RootModule]
    public ITravelDemandModel Root;

    [SubModelInformation(Required = true, Description = "The matrix to use for computing the percentile mask.")]
    public IDataSource<SparseTwinIndex<float>> InputMatrix;

    private SparseTwinIndex<float> _data;

    public enum SpatialAggregation
    {
        Zone,
        PlanningDistrict,
        Region,
        Custom
    }

    [RunParameter("Spatial Aggregation", SpatialAggregation.Zone, "The spatial aggregation level to use. If using Custom, Custom Aggregation needs to be set.")]
    public SpatialAggregation AggregationLevel = SpatialAggregation.Zone;

    [SubModelInformation(Required = false, Description = "The custom aggregation to use, make sure to set Spatial Aggregation to Custom before using.")]
    public IDataSource<ZoneMap> CustomAggregation;

    [RunParameter("Percentile", 0.8f, "The cutoff for mappings that get a 1 in the mask.  A value of 0.8 would be 80% of the input value would be added.")]
    public float Percentile = 0.8f;

    [RunParameter("Ignore Spatial Zeros", true, "Should we ignore spatial elements with the value of zero when computing the mask?")]
    public bool IgnoreZeros;

    public SparseTwinIndex<float> GiveData()
    {
        return _data;
    }

    public bool Loaded => _data is not null;

    public void LoadData()
    {
        (Dictionary<(int, int), float> spatialRecords, int[] indexes) = AggregateInput();
        var ret = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
        var flatret = ret.GetFlatData();
        var sum = spatialRecords.Values.Sum();
        var acc = 0.0f;
        List<(int, int)> toSet = [];
        foreach (var entry in spatialRecords.OrderByDescending(x => x.Value))
        {
            acc += entry.Value / sum;
            toSet.Add(entry.Key);
            if (acc >= Percentile)
            {
                break;
            }
        }
        // TODO: Calibrate this threshold
        const int thresholdForBinarySearch = 120;
        // Use a binary search if we have a lot of data to go through, this requires the data to be sorted first.
        if (toSet.Count > thresholdForBinarySearch)
        {
            toSet.Sort();
        }
        // Set the values now
        System.Threading.Tasks.Parallel.For(0, flatret.Length, i =>
        {
            var row = flatret[i];
            if (toSet.Count > thresholdForBinarySearch)
            {
                for (int j = 0; j < flatret.Length; j++)
                {
                    if (toSet.BinarySearch((indexes[i], indexes[j])) > 0)
                    {
                        row[j] = 1.0f;
                    }
                }
            }
            else
            {
                for (int j = 0; j < flatret.Length; j++)
                {
                    if (toSet.Contains((indexes[i], indexes[j])))
                    {
                        row[j] = 1.0f;
                    }
                }
            }

        });
        _data = ret;
    }

    /// <summary>
    /// Aggregate the input matrix based on the selected spatial aggregation level.
    /// </summary>
    /// <returns>A dictionary with the aggregated results and the indexes for each zone.</returns>
    private (Dictionary<(int, int), float>, int[]) AggregateInput()
    {
        var ret = new Dictionary<(int, int), float>();
        // Get the unique
        int[] indexes = [.. GetIndexes()];
        var loaded = InputMatrix.Loaded;
        if (!loaded)
        {
            InputMatrix.LoadData();
        }
        var inputData = InputMatrix.GiveData();
        if (!loaded)
        {
            InputMatrix.UnloadData();
        }

        // Go through the input data and aggregate it based on the selected spatial aggregation
        var flatInputData = inputData.GetFlatData();
        if (IgnoreZeros)
        {
            for (int i = 0; i < indexes.Length; i++)
            {
                var row = flatInputData[i];
                if (indexes[i] == 0)
                {
                    continue;
                }
                for (int j = 0; j < indexes.Length; j++)
                {
                    var value = row[j];
                    if (value > 0f && indexes[j] != 0)
                    {
                        var key = (indexes[i], indexes[j]);
                        CollectionsMarshal.GetValueRefOrAddDefault(ret, key, out _) += value;
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < indexes.Length; i++)
            {
                var row = flatInputData[i];
                for (int j = 0; j < indexes.Length; j++)
                {
                    var value = row[j];
                    if (value > 0f)
                    {
                        var key = (indexes[i], indexes[j]);
                        CollectionsMarshal.GetValueRefOrAddDefault(ret, key, out _) += value;
                    }
                }
            }
        }
        return (ret, indexes);
    }

    private IEnumerable<int> GetIndexes()
    {
        var zones = Root.ZoneSystem.ZoneArray;
        switch (AggregationLevel)
        {
            case SpatialAggregation.Zone:
                return zones.ValidIndexies();
            case SpatialAggregation.PlanningDistrict:
                return zones.GetFlatData().Select(z => z.PlanningDistrict);
            case SpatialAggregation.Region:
                return zones.GetFlatData().Select(z => z.RegionNumber);
            case SpatialAggregation.Custom:
                {
                    var loaded = CustomAggregation.Loaded;
                    if (!loaded)
                    {
                        CustomAggregation.LoadData();
                    }
                    int[] customMap = CustomAggregation.GiveData().Map;
                    if (!loaded)
                    {
                        CustomAggregation.UnloadData();
                    }
                    return customMap;
                }
            default:
                {
                    throw new InvalidOperationException("Unknown Spatial Aggregation level.");
                }
        }
    }

    public void UnloadData()
    {
        _data = null;
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        if (SpatialAggregation.Custom == AggregationLevel && CustomAggregation is null)
        {
            error = "Custom Aggregation must be set if Spatial Aggregation is set to Custom.";
            return false;
        }
        if (CustomAggregation is not null && AggregationLevel != SpatialAggregation.Custom)
        {
            error = "Custom Aggregation is only allowed if Spatial Aggregation is set to Custom.";
            return false;
        }
        return true;
    }
}
