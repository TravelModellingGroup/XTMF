/*
    Copyright 2014 James Vaughan for integration into XTMF.

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
using Tasha.Common;
using XTMF;
using TMG;
using Datastructure;
using TMG.Input;
using System.IO;

namespace Tasha.Validation.Data;

[ModuleInformation(Description =
@"This module is designed to export a general SparseArray<float> resource to disk."
    )]
public class ExportSparseArrayOfFloat : IPostIteration, IPostRun
{
    [RootModule]
    public ITravelDemandModel Root;

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    [SubModelInformation(Required = true, Description = "The SparseArray<float> resource to output.")]
    public IResource ToOutput;

    [SubModelInformation(Required = true, Description = "The location to save the output to. CSV (SparseIndex,Value)")]
    public FileLocation OutputFile;

    public void Execute()
    {
        var sparse = ToOutput.AcquireResource<SparseArray<float>>();
        var data = sparse.GetFlatData();
        using StreamWriter writer = new(OutputFile);
        writer.WriteLine("SparseIndex,Value");
        for (int i = 0; i < data.Length; i++)
        {
            writer.Write(sparse.GetSparseIndex(i));
            writer.Write(',');
            writer.WriteLine(data[i]);
        }
    }

    public void Execute(int iterationNumber, int totalIterations)
    {
        Execute();
    }

    public void Load(IConfiguration config)
    {
    }

    public void Load(IConfiguration config, int totalIterations)
    {

    }

    public bool RuntimeValidation(ref string error)
    {
        if(!ToOutput.CheckResourceType<SparseArray<float>>())
        {
            error = "In '" + Name + "' the resource specified is not a SparseArray<float> resource!";
            return false;
        }
        return true;
    }
}
