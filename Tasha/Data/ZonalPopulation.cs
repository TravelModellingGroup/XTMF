﻿
/*
    Copyright 2014-2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

[ModuleInformation(Description = "This module scraps the population for each zone from the zonal information and provides it in a SparseArray<float>.")]
public class ZonalPopulation : IDataSource<SparseArray<float>>
{
    public bool Loaded
    {
        get;set;
    }

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    private SparseArray<float> Data;

    [RootModule]
    public ITravelDemandModel Root;

    public SparseArray<float> GiveData()
    {
        return Data;
    }

    public void LoadData()
    {
        if(!Root.ZoneSystem.Loaded)
        {
            Root.ZoneSystem.LoadData();
        }
        var populationArray = Root.ZoneSystem.ZoneArray.CreateSimilarArray<float>();
        var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
        var data = populationArray.GetFlatData();
        for(int i = 0; i < data.Length; i++)
        {
            data[i] = zones[i].Population;
        }
        Data = populationArray;
        Loaded = true;
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void UnloadData()
    {
        Data = null;
        Loaded = false;
    }
}
