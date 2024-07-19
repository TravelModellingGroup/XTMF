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
using TMG.Input;
using XTMF;

namespace TMG.Frameworks.Data.DataTypes;

[ModuleInformation(Description = "Provides the ability to create a matrix with the shape of the given zone system.")]
public sealed class MatrixInZoneSystem : IDataSource<SparseTwinIndex<float>>
{
    [SubModelInformation(Required = true, Description = "The zone system to create the matrix in the shape of.")]
    public IDataSource<IZoneSystem> ZoneSystem;

    [SubModelInformation(Required = false, Description = "A source of OD data to load in.")]
    public IReadODData<float> DataReader;

    private SparseTwinIndex<float> _data;

    public SparseTwinIndex<float> GiveData()
    {
        return _data;
    }

    public bool Loaded => _data is not null;

    public void LoadData()
    {
        var zoneSystem = Load(ZoneSystem);
        var zones = zoneSystem.ZoneArray;
        var matrix = zones.CreateSquareTwinArray<float>();
        if (DataReader is not null)
        {
            foreach (var entry in DataReader.Read())
            {
                var o = entry.O;
                var d = entry.D;
                var value = entry.Data;
                matrix[o, d] = value;
            }
        }
        _data = matrix;
    }

    private static T Load<T>(IDataSource<T> dataSource)
    {
        var wasLoaded = dataSource.Loaded;
        if (!wasLoaded)
        {
            dataSource.LoadData();
        }
        var ret = dataSource.GiveData();
        if (!wasLoaded)
        {
            dataSource.UnloadData();
        }
        return ret;
    }

    public void UnloadData()
    {
        _data = null;
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new (50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

}
