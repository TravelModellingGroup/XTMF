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
using XTMF;
using TMG.Functions;
namespace Tasha.Data;

[ModuleInformation(Description = "This module will multiply the input by a constant value.")]
public class MultiplyODResourceByConstant : IDataSource<SparseTwinIndex<float>>
{
    [SubModelInformation(Required = false, Description = "The resource to multiply")]
    public IResource ResourceToMultiply;

    [SubModelInformation(Required = false, Description = "The raw data source to multiply.  Either this or the resource must be filled out.")]
    public IDataSource<SparseTwinIndex<float>> RawToMultiply;

    [RunParameter("Factor", 1.0f, "The factor to multiply the rates by in order to produce our results.")]
    public float Factor;

    public bool Loaded
    {
        get; set;
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
        var resource = ModuleHelper.GetDataFromDatasourceOrResource(RawToMultiply, ResourceToMultiply, RawToMultiply != null);
        var otherData = resource.GetFlatData();
        var ourResource = resource.CreateSimilarArray<float>();
        var data = ourResource.GetFlatData();
        for (int i = 0; i < data.Length; i++)
        {
            VectorHelper.Multiply(data[i], 0, otherData[i], 0, Factor, data[i].Length);
        }
        Data = ourResource;
        Loaded = true;
    }

    public bool RuntimeValidation(ref string error)
    {
        return this.EnsureExactlyOneAndOfSameType(RawToMultiply, ResourceToMultiply, ref error);
    }

    public void UnloadData()
    {
        Data = null;
        Loaded = false;
    }
}
