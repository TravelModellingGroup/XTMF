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
using TMG;
using XTMF;
using Datastructure;
using TMG.Functions;
namespace Tasha.Data;

[ModuleInformation(Description =
    @"This module is designed to multiply two rates together for each OD.")]
public class MultiplyODRates : IDataSource<SparseTwinIndex<float>>
{
    private SparseTwinIndex<float> Data;

    [RootModule]
    public ITravelDemandModel Root;

    [SubModelInformation(Required = true, Description = "The first Matrix")]
    public IResource FirstRateToApply;

    [SubModelInformation(Required = true, Description = "The second Matrix.")]
    public IResource SecondRateToApply;

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
        var first = FirstRateToApply.AcquireResource<SparseTwinIndex<float>>();
        var firstRate = first.GetFlatData();
        var secondRate = SecondRateToApply.AcquireResource<SparseTwinIndex<float>>().GetFlatData();
        SparseTwinIndex<float> data;
        data = first.CreateSimilarArray<float>();
        var flatData = data.GetFlatData();
        for (int i = 0; i < flatData.Length; i++)
        {
            VectorHelper.Multiply(flatData[i], 0, firstRate[i], 0, secondRate[i], 0, flatData[i].Length);
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
        if (!FirstRateToApply.CheckResourceType<SparseTwinIndex<float>>())
        {
            error = "In '" + Name + "' the first rates resource is not of type SparseTwinIndex<float>!";
            return false;
        }
        if (!SecondRateToApply.CheckResourceType<SparseTwinIndex<float>>())
        {
            error = "In '" + Name + "' the second rate resource is not of type SparseTwinIndex<float>!";
            return false;
        }
        return true;
    }
}
