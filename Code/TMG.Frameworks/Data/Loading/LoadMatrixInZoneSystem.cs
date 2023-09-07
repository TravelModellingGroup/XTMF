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
using System.Diagnostics.CodeAnalysis;
using TMG.Input;
using XTMF;

namespace TMG.Frameworks.Data.Loading;

[ModuleInformation(Description = "Load a matrix with the given zone system.")]
public sealed class LoadMatrixInZoneSystem : IDataSource<SparseTwinIndex<float>>
{
    private volatile bool _loaded = false;
    private SparseTwinIndex<float> _data;

    [SubModelInformation(Required = true, Description = "The zone system that the resulting vector should match.")]
    public IDataSource<IZoneSystem> ZoneSystem;

    [SubModelInformation(Required = true, Description = "The data reason to load in the data from, only the origin will be considered.")]
    public IReadODData<float> DataSource;

    [RunParameter("Continue After Invalid Zone", false, "Should the model system continue after an invalid zone number has been read in?")]
    public bool ContinueAfterInvalidZone;

    public SparseTwinIndex<float> GiveData()
    {
        return _data;
    }

    public bool Loaded => _loaded;

    public void LoadData()
    {
        var zoneSystem = Load(ZoneSystem);
        var data = zoneSystem.CreateSquareTwinArray<float>();
        if (ContinueAfterInvalidZone)
        {
            foreach (var entry in DataSource.Read())
            {
                _ = data.TryStore(entry.O, entry.D, entry.Data);
            }
        }
        else
        {
            foreach (var entry in DataSource.Read())
            {
                if (!data.TryStore(entry.O, entry.D, entry.Data))
                {
                    ThrowInvalidIndex(entry.O, entry.D, entry.Data);
                }
            }
        }
        _data = data;
        _loaded = true;
    }

    [DoesNotReturn]
    private void ThrowInvalidIndex(int origin, int destination, float data)
    {
        throw new XTMFRuntimeException(this, $"We ran into an issue where a zone {origin} was trying to be assigned to with the value of {data} but that origin does not exist!");
    }

    private static SparseArray<IZone> Load(IDataSource<IZoneSystem> zoneSystem)
    {
        if (zoneSystem.Loaded)
        {
            return zoneSystem.GiveData().ZoneArray;
        }
        else
        {
            zoneSystem.LoadData();
            var ret = zoneSystem.GiveData().ZoneArray;
            zoneSystem.UnloadData();
            return ret;
        }
    }

    public void UnloadData()
    {
        _loaded = false;
        _data = null;
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50,150,50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
