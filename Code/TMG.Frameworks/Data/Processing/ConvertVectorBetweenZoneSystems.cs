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
using XTMF;

namespace TMG.Frameworks.Data.Processing;

[ModuleInformation(Description = "This module is designed to convert a vector between two" +
    " zone systems using a mapping file that relates the factor to apply.")]
public sealed class ConvertVectorBetweenZoneSystems : IDataSource<SparseArray<float>>
{
    private volatile bool _loaded;

    private SparseArray<float> _data;

    [RootModule]
    public ITravelDemandModel Root;

    [SubModelInformation(Required = true, Description = "")]
    public IDataSource<SparseArray<float>> OriginalVector;

    [SubModelInformation(Required = false, Description = "The zone system the data will be converted for, leave blank to use the model system's zone system.")]
    public IDataSource<IZoneSystem> ConvertToZoneSystem;

    [SubModelInformation(Required = true, Description = "The location to read the CSV mapping file from, OriginalZone,ConvertedZone,Fraction")]
    public FileLocation MapFile;

    public SparseArray<float> GiveData()
    {
        return _data;
    }

    public bool Loaded => _loaded;

    public void LoadData()
    {
        var original = LoadDataSource(OriginalVector);
        var destination = LoadDataSource(ConvertToZoneSystem ?? Root.ZoneSystem).ZoneArray;
        var map = ColumnNormalize(BuildMapping(MapFile, original.ValidIndexArray(), destination.ValidIndexArray()), original.Count);
        _data = ConvertData(original, destination, map);
        _loaded = true;
    }

    private SparseArray<float> ConvertData(SparseArray<float> original, SparseArray<IZone> destination, float[] map)
    {
        var ret = destination.CreateSimilarArray<float>();
        System.Threading.Tasks.Parallel.For(0, destination.Count, (int destinationIndex) =>
        {
            var originalVector = original.GetFlatData().AsSpan();
            var mapVector = map.AsSpan(destinationIndex * originalVector.Length, originalVector.Length);
            // You might want to look into the false sharing that this store operation is going to cause
            ret.GetFlatData()[destinationIndex] = VectorHelper.MultiplyAndSumNoStore(originalVector, mapVector);
        });
        return ret;
    }

    private static float[] BuildMapping(FileLocation mapFile, int[] originalZones, int[] convertToZones)
    {
        var map = new float[originalZones.Length * convertToZones.Length];
        using (var reader = new CsvReader(mapFile))
        {
            reader.LoadLine();
            while (reader.LoadLine(out int columns))
            {
                if (columns >= 3)
                {
                    reader.Get(out int origin, 0);
                    reader.Get(out int destination, 1);
                    reader.Get(out float ratio, 2);
                    // convert the indexes into flat index look ups
                    origin = Array.BinarySearch(originalZones, origin);
                    destination = Array.BinarySearch(convertToZones, destination);
                    map[destination * originalZones.Length + origin] = ratio;
                }
            }
        }
        return map;
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


    private static T LoadDataSource<T>(IDataSource<T> dataSource)
    {
        if (!dataSource.Loaded)
        {
            dataSource.LoadData();
            var ret = dataSource.GiveData();
            dataSource.UnloadData();
            return ret;
        }
        else
        {
            return dataSource.GiveData();
        }
    }

    public void UnloadData()
    {
        lock (this)
        {
            _data = null;
            _loaded = false;
        }
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50,150,50);

    public bool RuntimeValidation(ref string error)
    {
        if(ConvertToZoneSystem is null && Root.ZoneSystem is null)
        {
            error = "At least one of ConvertToZoneSystem or the model system's Zone System need to be defined!";
            return false;
        }
        return true;
    }
}
