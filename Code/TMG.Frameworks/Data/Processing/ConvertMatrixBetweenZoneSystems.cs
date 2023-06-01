/*
    Copyright 2023 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Numerics;
using TMG.Functions;
using TMG.Input;
using System.Linq;
using XTMF;
using System.Runtime.CompilerServices;
using System.IO;

namespace TMG.Frameworks.Data.Processing;

[ModuleInformation(Description = "Converts a matrix between two zone systems using a mapping file that gives the factor to apply between zones.")]
public sealed class ConvertMatrixBetweenZoneSystems : IDataSource<SparseTwinIndex<float>>
{
    [RootModule]
    public ITravelDemandModel Root;

    [SubModelInformation(Required = false, Description = "The zone system the OD data will be converted for, leave blank to use the model system's zone system.")]
    public IDataSource<IZoneSystem> ConvertToZoneSystem;

    [SubModelInformation(Required = true, Description = "The data to convert")]
    public IDataSource<SparseTwinIndex<float>> Original;

    [SubModelInformation(Required = true, Description = "The location to read the CSV mapping file from, OriginalZone,ConvertedZone,Fraction")]
    public FileLocation MapFile;

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public enum Aggregations
    {
        Sum,
        Average
    }

    [RunParameter("Aggregation", "Sum", typeof(Aggregations), "The aggregation to apply")]
    public Aggregations Aggregation;

    public bool Loaded { get; set; }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    private SparseTwinIndex<float> _data;

    public SparseTwinIndex<float> GiveData() => _data;

    public void LoadData()
    {
        var convertToZones = LoadZoneSystem(ConvertToZoneSystem ?? Root.ZoneSystem);
        var ret = SparseTwinIndex<float>.CreateSquareTwinIndex(convertToZones, convertToZones);
        var original = GetData(Original);
        var originalZones = original.ValidIndexArray();
        var flat = ret.GetFlatData();
        var map = ColumnNormalize(BuildMapping(originalZones, convertToZones), originalZones.Length);
        switch (Aggregation)
        {
            case Aggregations.Sum:
                ApplySum(map, flat, original);
                break;
            case Aggregations.Average:
                ApplyAverage(map, flat, original);
                break;
        }
        _data = ret;
        Loaded = true;
    }

    private static float[] ColumnNormalize(float[] map, int columns)
    {
        var rows = map.Length / columns;
        int column = 0;
        for (; column < columns - Vector<float>.Count; column += Vector<float>.Count)
        {
            var vTotal = Vector<float>.Zero;
            for (int row = 0; row < rows; row++)
            {
                vTotal += new Vector<float>(map, row * columns + column);
            }
            vTotal = VectorHelper.SelectIfFinite(Vector<float>.One / vTotal, Vector<float>.Zero);
            for (int row = 0; row < rows; row++)
            {
                int index = row * columns + column;
                (new Vector<float>(map, index) * vTotal).CopyTo(map, index);
            }
        }
        for (; column < columns; column++)
        {
            var total = 0.0f;
            for (int row = 0; row < rows; row++)
            {
                total += map[row * columns + column];
            }
            total = 1.0f / total;
            if (float.IsNaN(total) || float.IsInfinity(total))
            {
                total = 0.0f;
            }
            for (int row = 0; row < rows; row++)
            {
                map[row * columns + column] *= total;
            }
        }
        return map;
    }

    private void ApplySum(float[] map, float[][] flatRet, SparseTwinIndex<float> original)
    {
        var flatOrigin = original.GetFlatData();
        System.Threading.Tasks.Parallel.For(0, flatRet.Length, (int i) =>
        {
            for (int j = 0; j < flatRet[i].Length; j++)
            {
                flatRet[i][j] = ComputeSum(map, flatOrigin, i, j);
            }
        });
    }

    private void ApplyAverage(float[] map, float[][] flatRet, SparseTwinIndex<float> original)
    {
        var flatOrigin = original.GetFlatData();
        System.Threading.Tasks.Parallel.For(0, flatRet.Length, (int i) =>
        {
            for (int j = 0; j < flatRet[i].Length; j++)
            {
                flatRet[i][j] = ComputeAverage(map, flatOrigin, i, j);
            }
        });
    }

    private static float ComputeAverage(float[] map, float[][] fromMatrix, int retRow, int retColumn)
    {
        var ret = 0.0f;
        var rowBase = fromMatrix.Length * retRow;
        var columnBase = fromMatrix.Length * retColumn;
        Vector<float> vRet = Vector<float>.Zero;
        Vector<float> vFactorSum = Vector<float>.Zero;
        float factorSum = 0.0f;
        for (int i = 0; i < fromMatrix.Length; i++)
        {
            var iFactor = map[rowBase + i];
            var row = fromMatrix[i];
            if (iFactor > 0)
            {
                int j = 0;
                Vector<float> iFactorV = new Vector<float>(iFactor);
                for (; j < row.Length - Vector<float>.Count; j += Vector<float>.Count)
                {
                    var rowV = new Vector<float>(row, j);
                    var mapV = new Vector<float>(map, columnBase + j);
                    var factor = iFactorV * mapV;
                    vFactorSum += factor;
                    vRet += rowV * factor;
                }
                for (; j < row.Length; j++)
                {
                    var factor = iFactor * map[columnBase + j];
                    factorSum += factor;
                    ret += row[j] * factor;
                }
            }
        }
        ret += Vector.Sum(vRet);
        ret /= (factorSum + Vector.Sum(vFactorSum));
        return ret;
    }

    private float ComputeSum(float[] map, float[][] fromMatrix, int retRow, int retColumn)
    {
        var ret = 0.0f;
        var rowBase = fromMatrix.Length * retRow;
        var columnBase = fromMatrix.Length * retColumn;
        for (int i = 0; i < fromMatrix.Length; i++)
        {
            var iFactor = map[rowBase + i];
            if (iFactor > 0)
            {
                ret += iFactor * VectorHelper.MultiplyAndSumNoStore(fromMatrix[i].AsSpan(), map.AsSpan(columnBase, fromMatrix.Length));
            }
        }
        return ret;
    }

    private SparseTwinIndex<float> GetData(IDataSource<SparseTwinIndex<float>> original)
    {
        if (original.Loaded)
        {
            return original.GiveData();
        }
        else
        {
            original.LoadData();
            var ret = original.GiveData();
            original.UnloadData();
            return ret;
        }
    }

    private float[] BuildMapping(int[] originalZones, int[] convertToZones)
    {
        var map = new float[originalZones.Length * convertToZones.Length];
        using var reader = new CsvReader(MapFile, true);
        reader.LoadLine();
        while (reader.LoadLine(out int columns))
        {
            if (columns >= 3)
            {
                reader.Get(out int origin, 0);
                reader.Get(out int destination, 1);
                reader.Get(out float ratio, 2);
                // convert the indexes into flat index look ups
                var flatOrigin = Array.BinarySearch(originalZones, origin);
                var flatDestination = Array.BinarySearch(convertToZones, destination);
                if (flatOrigin < 0)
                {
                    throw new XTMFRuntimeException(this, $"The zone origin zone {origin} does not exist in the zone system on row {reader.LineNumber + 1}!");
                }
                else if (flatDestination < 0)
                {
                    throw new XTMFRuntimeException(this, $"The zone destination zone {destination} does not exist in the zone system on row {reader.LineNumber + 1}!");
                }
                map[GetMapIndex(flatDestination, flatOrigin, originalZones.Length)] = ratio;
            }
        }
        return map;
    }

    private static int GetMapIndex(int flatDestination, int flatOrigin, int totalOriginZones)
    {
        return flatDestination * totalOriginZones + flatOrigin;
    }

    private static int[] LoadZoneSystem(IDataSource<IZoneSystem> zoneSystem)
    {
        if (!zoneSystem.Loaded)
        {
            zoneSystem.LoadData();
        }
        return zoneSystem.GiveData()
            .ZoneArray.GetFlatData()
            .Select(z => z.ZoneNumber)
            .ToArray();
    }

    public void UnloadData()
    {
        _data = null;
        Loaded = false;
    }
}
