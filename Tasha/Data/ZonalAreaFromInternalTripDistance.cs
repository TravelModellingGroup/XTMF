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
using Datastructure;
using TMG;
using XTMF;

namespace Tasha.Data;

[ModuleInformation(Description =
    "This module is designed to convert internal distances into zonal areas using the formula <p></p>")]
public class ZonalAreaFromInternalTripDistance : IDataSource<SparseArray<float>>
{
    public bool Loaded
    {
        get;set;
    }

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    SparseArray<float> Data;

    public SparseArray<float> GiveData()
    {
        return Data;
    }

    [RootModule]
    public ITravelDemandModel Root;

    public void LoadData()
    {
        var zoneSystem = Root.ZoneSystem.ZoneArray;
        Data = zoneSystem.CreateSimilarArray<float>();
        var data = Data.GetFlatData();
        var zones = zoneSystem.GetFlatData();
        for(int i = 0; i < data.Length; i++)
        {
            // internal distance = (sqrt(area)/6) <=> area = (6*internal distance)^2
            var iDistance = zones[i].InternalDistance * 6;
            data[i] = iDistance * iDistance;
        }
        Loaded = true;
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void UnloadData()
    {
        Loaded = false;
        Data = null;
    }
}
