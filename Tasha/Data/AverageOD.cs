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
using Datastructure;
using System;
using TMG;
using TMG.Functions;
using XTMF;

namespace Tasha.Data;

[ModuleInformation(
    Description = "This module is designed to take the average of the two given matrices.  It can then store the result in the first matrix or create a new one to pass on to the next step."
    )]
public class AverageOD : IDataSource<SparseTwinIndex<float>>
{
    [RootModule]
    public ITravelDemandModel Root;

    public bool Loaded { get; set; }

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    private SparseTwinIndex<float> Data;

    [SubModelInformation(Required = false, Description = "The first matrix if loading from a resource")]
    public IResource FirstMatrix;

    [SubModelInformation(Required = false, Description = "The second matrix if loading from a resource")]
    public IResource SecondMatrix;

    [SubModelInformation(Required = false, Description = "The first matrix if loading from a data source")]
    public IDataSource<SparseTwinIndex<float>> FirstDataSource;

    [SubModelInformation(Required = false, Description = "The second matrix if loading from a data source")]
    public IDataSource<SparseTwinIndex<float>> SecondDataSource;

    [RunParameter("Overwrite First", false, "Should we save memory by building the result in the first matrix?")]
    public bool OverwriteFirst;

    public SparseTwinIndex<float> GiveData()
    {
        return Data;
    }

    public void LoadData()
    {
        SparseTwinIndex<float> first = ModuleHelper.GetDataFromDatasourceOrResource(FirstDataSource, FirstMatrix);
        SparseTwinIndex<float> second = ModuleHelper.GetDataFromDatasourceOrResource(SecondDataSource, SecondMatrix);
        SparseTwinIndex<float> ret = GetResultMatrix(first);
        var data = ret.GetFlatData();
        var f = first.GetFlatData();
        var s = second.GetFlatData();
        for (int i = 0; i < data.Length; i++)
        {
            VectorHelper.Average(data[i], 0, f[i], 0, s[i], 0, data[i].Length);
        }
        Data = ret;
        Loaded = true;
    }

    private SparseTwinIndex<float> GetResultMatrix(SparseTwinIndex<float> first)
    {
        if (OverwriteFirst)
        {
            return first;
        }
        return first.CreateSimilarArray<float>();
    }

    public bool RuntimeValidation(ref string error)
    {
        return this.EnsureExactlyOneAndOfSameType(FirstDataSource, FirstMatrix, ref error)
            && this.EnsureExactlyOneAndOfSameType(SecondDataSource, SecondMatrix, ref error);
    }

    public void UnloadData()
    {
        Data = null;
        Loaded = false;
    }
}
