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
using TMG;
using XTMF;
using Datastructure;
using TMG.Functions;
namespace Tasha.Data;

[ModuleInformation(Description =
    @"This module is designed to normalize the matrix so it sums to 1.")]
public class NormalizeOD : IDataSource<SparseTwinIndex<float>>
{
    private SparseTwinIndex<float> Data;

    [RootModule]
    public ITravelDemandModel Root;

    [SubModelInformation(Required = false, Description = "The matrix to normalize")]
    public IResource ToNormalize;

    [SubModelInformation(Required = false, Description = "Optionally the raw source to normalize")]
    public IDataSource<SparseTwinIndex<float>> RawToNormalize;

    public SparseTwinIndex<float> GiveData()
    {
        return Data;
    }

    public bool Loaded
    {
        get { return Data != null; }
    }

    public void LoadData()
    {
        var zoneArray = Root.ZoneSystem.ZoneArray;
        zoneArray.GetFlatData();
        var firstRate = ModuleHelper.GetDataFromDatasourceOrResource(RawToNormalize, ToNormalize).GetFlatData();
        SparseTwinIndex<float> data;
        data = zoneArray.CreateSquareTwinArray<float>();
        var flatData = data.GetFlatData();
        float sum = 0.0f;
        for (int i = 0; i < firstRate.Length; i++)
        {
            sum += VectorHelper.Sum(firstRate[i], 0, firstRate.Length);
        }
        for (int i = 0; i < flatData.Length; i++)
        {
            VectorHelper.Multiply(flatData[i], 0, firstRate[i], 0, 1.0f / sum, flatData[i].Length);
        }
        Data = data;
    }

    public void UnloadData()
    {
        Data = null;
    }

    public string Name { get; set; }

    public float Progress
    {
        get { return 0f; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return null; }
    }

    public bool RuntimeValidation(ref string error)
    {
        return this.EnsureExactlyOneAndOfSameType(RawToNormalize, ToNormalize, ref error);
    }
}
