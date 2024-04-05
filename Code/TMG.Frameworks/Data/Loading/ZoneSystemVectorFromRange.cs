/*
    Copyright 2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;

namespace TMG.Frameworks.Data.Loading;

[ModuleInformation(Description = "This module is designed to allow the user to define a vector of true and flase float values depending on the range.")]
public sealed class ZoneSystemVectorFromRange : IDataSource<SparseArray<float>>
{
    [RunParameter("If True", 1.0f, "The value to assign if true.")]
    public float IfTrue;

    [RunParameter("If False", 0.0f, "The value to assign if false.")]
    public float IfFalse;

    [RunParameter("True Range", "0-1000", typeof(RangeSet), "The range of zone numbers that invoke true.")]
    public RangeSet Range;

    [RootModule]
    public ITravelDemandModel Root;

    private SparseArray<float> Data;

    public SparseArray<float> GiveData()
    {
        return Data;
    }

    public string Name { get; set; }
    public float Progress => 0f;
    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public bool Loaded => Data != null;

    public void LoadData()
    {
        var zoneSystem = Root.ZoneSystem;
        if (!zoneSystem.Loaded)
        {
            zoneSystem.LoadData();
        }
        var zones = zoneSystem.ZoneArray.GetFlatData();
        var data = zoneSystem.ZoneArray.CreateSimilarArray<float>();
        var flatData = data.GetFlatData();
        for (int i = 0; i < flatData.Length; i++)
        {
            flatData[i] = Range.Contains(zones[i].ZoneNumber) ? IfTrue : IfFalse;
        }
        Data = data;
    }

    public void UnloadData()
    {
        Data = null;
    }
}
