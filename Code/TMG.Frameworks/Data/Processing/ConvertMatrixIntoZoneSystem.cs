/*
    Copyright 2019-2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;

namespace TMG.Frameworks.Data.Processing;

[ModuleInformation(Description = "This module is designed to create a copy of the given source matrix converting" +
    " it into the model system's zone system.  Zones that don't exist in the current zone system will be ignored and zones" +
    " in the zone system but that are not present in the source matrix will return the default value, typically zero.")]
public sealed class ConvertMatrixIntoZoneSystem : IDataSource<SparseTwinIndex<float>>
{
    [DoNotAutomate]
    private ITravelDemandModel _root;

    public bool Loaded => _data != null;

    public string Name { get; set; }

    public float Progress => 0f;

    private IConfiguration _config;

    public ConvertMatrixIntoZoneSystem(IConfiguration config)
    {
        _config = config;
    }

    public Tuple<byte, byte, byte> ProgressColour => new(50,150,50);

    [SubModelInformation(Required = true, Description = "The matrix to convert into the model's zone system.")]
    public IDataSource<SparseTwinIndex<float>> Source;

    [SubModelInformation(Required = false, Description = "An optional zone system to convert into. If not selected the Root zone system will be used.")]
    public IDataSource<IZoneSystem> ZoneSystem;

    private SparseTwinIndex<float> _data;

    public SparseTwinIndex<float> GiveData()
    {
        return _data;
    }

    public void LoadData()
    {
        SparseTwinIndex<float> ret = CreateDestinationMatrix();
        var loadedSource = !Source.Loaded;
        if (loadedSource)
        {
            Source.LoadData();
        }
        var sourceMatrix = Source.GiveData();
        if (loadedSource)
        {
            Source.UnloadData();
        }

        var flatRet = ret.GetFlatData();
        foreach (var oIndexSet in ret.Indexes.Indexes)
        {
            foreach (var dIndexSet in oIndexSet.SubIndex.Indexes)
            {
                var oRange = oIndexSet.Stop - oIndexSet.Start + 1;
                for (int o = 0; o < oRange; o++)
                {
                    var dRange = dIndexSet.Stop - dIndexSet.Start + 1;
                    for (int d = 0; d < dRange; d++)
                    {
                        flatRet[oIndexSet.BaseLocation + o][dIndexSet.BaseLocation + d] = sourceMatrix[oIndexSet.Start + o, dIndexSet.Start + d];
                    }
                }
            }
        }
        _data = ret;
    }

    private SparseTwinIndex<float> CreateDestinationMatrix()
    {
        if (ZoneSystem is not null)
        {
            var loaded = ZoneSystem.Loaded;
            if(!loaded) ZoneSystem.LoadData();
            var ret = ZoneSystem.GiveData().ZoneArray.CreateSquareTwinArray<float>();
            if (!loaded) ZoneSystem.UnloadData();
            return ret;
        }
        else
        {
            if (!_root.ZoneSystem.Loaded)
            {
                _root.ZoneSystem.LoadData();
            }
            return _root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
        }
    }

    public bool RuntimeValidation(ref string error)
    {
        if (ZoneSystem is not null)
        {
            // No need to find a root if we have a source already.
            return true;
        }
        // Find our source of the zone system
        if (!TMG.Functions.ModelSystemReflection.GetRootOfType(_config, typeof(ITravelDemandModel), this, out var root))
        {
            error = $"Could not find a root of type {typeof(ITravelDemandModel).FullName} and there was no ZoneSystem provided!";
            return false;
        }
        if (root.Module is ITravelDemandModel model)
        {
            _root = model;
        }
        else
        {
            // This should never happen
            error = "We were not able to get out an ITravelDemandModel after reflecting a found root of that type!";
            return false;
        }
        return true;
    }

    public void UnloadData()
    {
        _data = null;
    }
}
