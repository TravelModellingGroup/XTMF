/*
    Copyright 2015-2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

[ModuleInformation(Description = "This module proves an identity matrix which matches the same size as the zone system.")]
public class IdentityMatrix : IDataSource<SparseTwinIndex<float>>
{

    [RootModule]
    public ITravelDemandModel Root;

    public bool Loaded
    {
        get;
        set;
    }

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    private SparseTwinIndex<float> Data;

    public SparseTwinIndex<float> GiveData()
    {
        return Data;
    }

    public void LoadData()
    {
        var matrix = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
        var flat = matrix.GetFlatData();
        for(int i = 0; i < flat.Length; i++)
        {
            flat[i][i] = 1.0f;
        }
        Data = matrix;
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
