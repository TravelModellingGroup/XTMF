/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG.Functions;
using TMG.Data;
using XTMF;
// ReSharper disable InconsistentNaming


namespace TMG.Frameworks.Data.Processing;

[ModuleInformation(Description = "This module is designed to condense a zone system OD matrix into a given arbitrary map.")]
// ReSharper disable once InconsistentNaming
public class AggregateZoneODByZoneMap : IDataSource<SparseTwinIndex<float>>
{
    public bool Loaded
    {
        get
        {
            return Data != null;
        }
    }

    private SparseTwinIndex<float> Data;

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    [SubModelInformation(Required = false, Description = "The zone map to use loaded from a datasource")]
    public IDataSource<ZoneMap> ZoneMapFromDataSource;

    [SubModelInformation(Required = false, Description = "The zone map to use loaded from a resource")]
    public IResource ZoneMapFromResource;

    [RunParameter("Unload Zone Source", true, "Should we unload the ZoneMap's source after loading?")]
    public bool UnloadZoneSource;

    [SubModelInformation(Required = false, Description = "The data to aggregate from a datasource")]
    public IDataSource<SparseTwinIndex<float>> ODDataFromDataSource;

    [SubModelInformation(Required = false, Description = "The data to aggregate from a resource")]
    public IResource ODDataFromResource;

    [RunParameter("Unload ODData Source", true, "Should we unload the ODData source after loading?")]
    public bool UnloadODDataSource;

    public SparseTwinIndex<float> GiveData()
    {
        return Data;
    }

    public void LoadData()
    {
        var map = ModuleHelper.GetDataFromDatasourceOrResource(ZoneMapFromDataSource, ZoneMapFromResource, UnloadZoneSource);
        var zoneData = ModuleHelper.GetDataFromDatasourceOrResource(ODDataFromDataSource, ODDataFromResource, UnloadODDataSource).GetFlatData();
        var bins = map.MapValues.ToArray();
        var mapOd = SparseArray<float>.CreateSparseArray(bins, null).CreateSquareTwinArray<float>();
        var flatMapData = mapOd.GetFlatData();
        var toZones = map.KeyToZoneIndex;
        for (int originBin = 0; originBin < bins.Length; originBin++)
        {
            var originZones = toZones[bins[originBin]];
            for (int i = 0; i < originZones.Count; i++)
            {
                var originRow = zoneData[originZones[i]];
                for (int destinationBin = 0; destinationBin < bins.Length; destinationBin++)
                {
                    var destinationZones = toZones[bins[destinationBin]];
                    for (int j = 0; j < destinationZones.Count; j++)
                    {
                        flatMapData[originBin][destinationBin] += originRow[destinationZones[j]];
                    }
                }
            }
        }
        Data = mapOd;
    }

    public bool RuntimeValidation(ref string error)
    {
        return this.EnsureExactlyOneAndOfSameType(ZoneMapFromDataSource, ZoneMapFromResource, ref error) 
            && this.EnsureExactlyOneAndOfSameType(ODDataFromDataSource, ODDataFromResource, ref error);
    }


    public void UnloadData()
    {
        Data = null;
    }
}
