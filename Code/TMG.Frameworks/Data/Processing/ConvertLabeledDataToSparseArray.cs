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
using TMG.Frameworks.Data.DataTypes;
using XTMF;

namespace TMG.Frameworks.Data.Processing;


public class ConvertLabeledDataToSparseArray : IDataSource<SparseArray<float>>
{
    public bool Loaded { get; set; }

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    [SubModelInformation(Required = true, Description = "")]
    public IDataSource<LabeledData<float>> Labeled;

    private SparseArray<float> _Data;

    public SparseArray<float> GiveData()
    {
        return _Data;
    }

    public void LoadData()
    {
        LabeledData<float> baseData;
        var alreadyLoaded = Labeled.Loaded;
        if (!alreadyLoaded)
        {
            Labeled.LoadData();
            baseData = Labeled.GiveData();
            Labeled.UnloadData();
        }
        else
        {
            baseData = Labeled.GiveData();
        }
        var data = baseData.OrderBy(k => k.Key).Select(pair => pair.Value).ToArray();
        _Data = new SparseArray<float>(new SparseIndexing() { Indexes = [new SparseSet() { Start = 0, Stop = data.Length - 1 }] }, data);
        Loaded = true;
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void UnloadData()
    {
        Loaded = false;
        _Data = null;
    }
}
