/*
    Copyright 2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.Runtime.Intrinsics;
using TMG.Functions;
using XTMF;

namespace TMG.Frameworks.Data.Loading;


[ModuleInformation(Description = "This module provides some common matrix types to support calculations on a zone system.")]
public sealed class ZoneSystemMatrix : IDataSource<SparseTwinIndex<float>>
{

    [RootModule]
    public ITravelDemandModel Root;

    public enum MatrixType
    {
        StraightLineZoneDistance = 0,
        ManhattanZoneDistance = 1,
        IntraPDMatrix = 3,
        IntraRegionMatrix = 4,
        ZoneSystemDistanceMatrix = 5,
    }

    [RunParameter("Matrix Type", MatrixType.StraightLineZoneDistance, "The type of data from the zone system to fill the matrix with.")]
    public MatrixType Data;

    private SparseTwinIndex<float> _data = null;

    public SparseTwinIndex<float> GiveData()
    {
        return _data;
    }

    public bool Loaded => _data is not null;

    public void LoadData()
    {
        _data = Data switch
        {
            MatrixType.StraightLineZoneDistance => ComputeStraightLineDistance(),
            MatrixType.ManhattanZoneDistance => ComputeManhattanDistance(),
            MatrixType.IntraPDMatrix => ComputeIntraPDMatrix(),
            MatrixType.IntraRegionMatrix => ComputeIntraRegionMatrix(),
            MatrixType.ZoneSystemDistanceMatrix => CopyZoneSystemDistance(),
            _ => throw new XTMFRuntimeException(this, "Unknown Matrix Type!")
        };
    }

    private SparseTwinIndex<float> ComputeStraightLineDistance()
    {
        var zones = Root.ZoneSystem.ZoneArray;
        var ret = zones.CreateSquareTwinArray<float>();
        var zonePoints = zones.GetFlatData().Select(x => (x.X, x.Y)).ToArray();
        var flatData = ret.GetFlatData();

        for (var i = 0; i < flatData.Length; i++)
        {
            int j;
            // TODO: Write a vector version
            for (j = 0; j < flatData[i].Length; j++)
            {
                var dx = zonePoints[i].X - zonePoints[j].X;
                var dy = zonePoints[i].Y - zonePoints[j].Y;
                flatData[i][j] = MathF.Sqrt(dx * dx + dy * dy);
            }
        }
        return ret;
    }

    private SparseTwinIndex<float> ComputeManhattanDistance()
    {
        var zones = Root.ZoneSystem.ZoneArray;
        var ret = zones.CreateSquareTwinArray<float>();
        var zonePoints = zones.GetFlatData().Select(x => (x.X, x.Y)).ToArray();
        var flatData = ret.GetFlatData();

        for (var i = 0; i < flatData.Length; i++)
        {
            int j;
            // TODO: Write a vector version
            for (j = 0; j < flatData[i].Length; j++)
            {
                var dx = zonePoints[i].X - zonePoints[j].X;
                var dy = zonePoints[i].Y - zonePoints[j].Y;
                flatData[i][j] = MathF.Abs(dx) + MathF.Abs(dy);
            }
        }
        return ret;
    }

    private SparseTwinIndex<float> ComputeIntraPDMatrix()
    {
        var zones = Root.ZoneSystem.ZoneArray;
        var flatZones = zones.GetFlatData();
        var ret = zones.CreateSquareTwinArray<float>();
        var flatRet = ret.GetFlatData();

        // Use the stack to avoid an extra allocation
        Span<int> pd = stackalloc int[flatZones.Length];
        for (int i = 0; i < pd.Length; i++)
        {
            pd[i] = flatZones[i].PlanningDistrict;
        }

        for (var i = 0; i < flatRet.Length; i++)
        {
            int j = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                var one = Vector512<float>.One;
                var zero = Vector512<float>.Zero;
                var iPD = Vector512.Create(pd[i]);
                for (; j < flatRet[i].Length - Vector512<float>.Count; j += Vector512<float>.Count)
                {
                    var jPD = Vector512.LoadUnsafe(ref pd[j]);
                    var cmp = Vector512.Equals(iPD, jPD);
                    var result = VectorHelper.Blend(zero, one, cmp.AsSingle());
                    Vector512.StoreUnsafe(result, ref flatRet[i][j]);
                }
            }
            else if(Vector256.IsHardwareAccelerated)
            {
                var one = Vector256<float>.One;
                var zero = Vector256<float>.Zero;
                var iPD = Vector256.Create(pd[i]);
                for (; j < flatRet[i].Length - Vector256<float>.Count; j += Vector256<float>.Count)
                {
                    var jPD = Vector256.LoadUnsafe(ref pd[j]);
                    var cmp = Vector256.Equals(iPD, jPD);
                    var result = VectorHelper.Blend(zero, one, cmp.AsSingle());
                    Vector256.StoreUnsafe(result, ref flatRet[i][j]);
                }
            }
            for (; j < flatRet[i].Length; j++)
            {
                flatRet[i][j] = pd[i] == pd[j] ? 1.0f : 0.0f;
            }
        }
        return ret;
    }

    private SparseTwinIndex<float> ComputeIntraRegionMatrix()
    {
        var zones = Root.ZoneSystem.ZoneArray;
        var flatZones = zones.GetFlatData();
        var ret = zones.CreateSquareTwinArray<float>();
        var flatRet = ret.GetFlatData();

        // Use the stack to avoid an extra allocation
        Span<int> region = stackalloc int[flatZones.Length];
        for (int i = 0; i < region.Length; i++)
        {
            region[i] = flatZones[i].RegionNumber;
        }

        for (var i = 0; i < flatRet.Length; i++)
        {
            int j = 0;
            if (Vector512.IsHardwareAccelerated)
            {
                var one = Vector512<float>.One;
                var zero = Vector512<float>.Zero;
                var iPD = Vector512.Create(region[i]);
                for (; j < flatRet[i].Length - Vector512<float>.Count; j += Vector512<float>.Count)
                {
                    var jPD = Vector512.LoadUnsafe(ref region[j]);
                    var cmp = Vector512.Equals(iPD, jPD);
                    var result = VectorHelper.Blend(zero, one, cmp.AsSingle());
                    Vector512.StoreUnsafe(result, ref flatRet[i][j]);
                }
            }
            else if (Vector256.IsHardwareAccelerated)
            {
                var one = Vector256<float>.One;
                var zero = Vector256<float>.Zero;
                var iPD = Vector256.Create(region[i]);
                for (; j < flatRet[i].Length - Vector256<float>.Count; j += Vector256<float>.Count)
                {
                    var jPD = Vector256.LoadUnsafe(ref region[j]);
                    var cmp = Vector256.Equals(iPD, jPD);
                    var result = VectorHelper.Blend(zero, one, cmp.AsSingle());
                    Vector256.StoreUnsafe(result, ref flatRet[i][j]);
                }
            }
            for (; j < flatRet[i].Length; j++)
            {
                flatRet[i][j] = region[i] == region[j] ? 1.0f : 0.0f;
            }
        }
        return ret;
    }

    private SparseTwinIndex<float> CopyZoneSystemDistance()
    {
        var zoneSystemDistances = Root.ZoneSystem.Distances;
        var ret = zoneSystemDistances.CreateSimilarArray<float>();
        // Clone the data
        var flatRet = ret.GetFlatData();
        var flatData = zoneSystemDistances.GetFlatData();
        for ( var i = 0; i < flatData.Length; i++)
        {
            Array.Copy(flatData[i], flatRet[i], flatData.Length);
        }
        return ret;
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
        return true;
    }
}
