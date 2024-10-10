/*
    Copyright 2016-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Text;
using System.Threading.Tasks;
using XTMF;

namespace TMG.Frameworks.Data.Loading;

[ModuleInformation(Description = "This module create a mask matrix for different selected origins and destination combinations." +
    " To be selected the cell must be both contained within the origins and destinations.")]
public sealed class ZoneSystemMaskMatrix : IDataSource<SparseTwinIndex<float>>
{
    [RunParameter("Origins", "1", typeof(RangeSet), "The range of origin values to include in the mask.")]
    public RangeSet Origins;

    [RunParameter("Destinations", "1", typeof(RangeSet), "The range of destination values to include in the mask.")]
    public RangeSet Destinations;

    private SparseTwinIndex<float> _data = null;

    [SubModelInformation(Required = false, Description = "An optional zone system to use. If not selected the Root zone system will be used.")]
    public IDataSource<IZoneSystem> ZoneSystem;

    public bool Loaded => _data is not null;

    public enum FillData
    {
        ZoneNumber,
        PlanningDistrict,
        Region,
        FlatIndex
    }

    [RunParameter("Fill With", FillData.PlanningDistrict, "The type of information to use for selection what to activate for the mask.")]
    public FillData FillWith;

    private IConfiguration _config;

    public ZoneSystemMaskMatrix(IConfiguration config)
    {
        _config = config;
    }

    public void LoadData()
    {
        SparseArray<IZone> zoneSystem = GetZoneSystem();
        int[] intVector = GetSpatialIndexes(zoneSystem);
        var data = zoneSystem.CreateSquareTwinArray<float>();
        var flatData = data.GetFlatData();
        System.Threading.Tasks.Parallel.For(0, intVector.Length, (i) =>
        {
            // No need to process if this is not an origin
            if (!Origins.Contains(intVector[i]))
            {
                return;
            }
            var row = flatData[i];
            for (int j = 0; j < row.Length; j++)
            {
                row[j] = Destinations.Contains(intVector[j]) ? 1.0f : 0.0f;
            }
        });
        _data = data;
    }

    [DoNotAutomate]
    private ITravelDemandModel _root;

    private SparseArray<IZone> GetZoneSystem()
    {
        static SparseArray<IZone> Get(IDataSource<IZoneSystem> zoneSystem)
        {
            var loaded = zoneSystem.Loaded;
            if (!loaded)
            {
                zoneSystem.LoadData();
            }
            var ret = zoneSystem.GiveData().ZoneArray;
            if (!loaded)
            {
                zoneSystem.UnloadData();
            }
            return ret;
        }
        return _root is not null ? Get(_root.ZoneSystem) : Get(ZoneSystem);
    }

    private int[] GetSpatialIndexes(SparseArray<IZone> zonesSystem)
    {
        return FillWith switch
        {
            FillData.PlanningDistrict => zonesSystem.GetFlatData().Select(z => z.PlanningDistrict).ToArray(),
            FillData.Region => zonesSystem.GetFlatData().Select(z => z.RegionNumber).ToArray(),
            FillData.FlatIndex => Enumerable.Range(0, zonesSystem.GetFlatData().Length).ToArray(),
            FillData.ZoneNumber => zonesSystem.GetFlatData().Select(z => z.ZoneNumber).ToArray(),
            _ => throw new XTMFRuntimeException(this, "Unknown FillData Type!")
        };
    }

    public SparseTwinIndex<float> GiveData()
    {
        return _data;
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
        if(root.Module is ITravelDemandModel model)
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
}

