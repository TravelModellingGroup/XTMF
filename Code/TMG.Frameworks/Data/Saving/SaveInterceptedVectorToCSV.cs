/*
    Copyright 2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.IO;
using System.Threading;
using TMG.Functions;
using TMG.Input;
using XTMF;

namespace TMG.Frameworks.Data.Saving;

[ModuleInformation(Description = "This module provides a way of storing a matrix after it has been loaded but before it has been used.")]
public sealed class SaveInterceptedVectorToCSV : IDataSource<SparseArray<float>>
{

    [SubModelInformation(Required = true, Description = "The matrix that will be stored before being returned.")]
    public IDataSource<SparseArray<float>> ToIntercept;

    [SubModelInformation(Required = true, Description = "The location to save the intercepted matrix to.  MTX format.")]
    public FileLocation SaveTo;

    public SparseArray<float> GiveData()
    {
        return ToIntercept.GiveData();
    }

    public bool Loaded => ToIntercept.Loaded;

    private Lock _lock = new();

    public void LoadData()
    {
        lock (_lock)
        {
            ToIntercept.LoadData();
            Save(GiveData());
        }
    }

    private void Save(SparseArray<float> data)
    {
        int[] indexes = data.ValidIndexArray();
        var flatData = data.GetFlatData();
        using var writer = new StreamWriter(SaveTo);
        // write header
        writer.WriteLine("TAZ,Value");
        // write data
        Span<char> buffer = stackalloc char[32];
        for (int i = 0; i < flatData.Length; i++)
        {
            writer.Write(indexes[i]);
            writer.Write(',');
            Utilities.Write(writer, flatData[i], buffer);
        }
    }

    public void UnloadData()
    {
        lock (_lock)
        {
            ToIntercept.UnloadData();
        }
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

}
