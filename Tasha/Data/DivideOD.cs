﻿/*
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


public class DivideOD : IDataSource<SparseTwinIndex<float>>
{
    private SparseTwinIndex<float> Data;

    [RootModule]
    public ITravelDemandModel Root;

    [SubModelInformation(Required = true, Description = "The value to use for the numerator.")]
    public IResource FirstRateToApply;

    [SubModelInformation(Required = true, Description = "The value to use for the denominator.")]
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
        var firstRate = FirstRateToApply.AcquireResource<SparseTwinIndex<float>>();
        var secondRate = SecondRateToApply.AcquireResource<SparseTwinIndex<float>>();
        SparseTwinIndex<float> data = firstRate.CreateSimilarArray<float>();
        var flatFirst = firstRate.GetFlatData();
        var flatSecond = secondRate.GetFlatData();
        var flat = data.GetFlatData();
        for (int i = 0; i < flat.Length; i++)
        {
            VectorHelper.Divide(flat[i], 0, flatFirst[i], 0, flatSecond[i], 0, flat[i].Length);
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
            error = "In '" + Name + "' the first rates resource is not of type SparseArraySparseTwinIndex<float>!";
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
