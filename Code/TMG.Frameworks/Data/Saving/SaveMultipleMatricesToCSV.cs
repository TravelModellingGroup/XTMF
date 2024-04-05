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
using System.Linq;
using TMG.Input;
using XTMF;

namespace TMG.Frameworks.Data.Saving;

[ModuleInformation(Description = "Combine the results of multiple matrices to a single CSV.  " +
    "The header starts with the origin then destination columns.  " +
    "It then follows with one column for each of the matrices.")]
public sealed class SaveMultipleMatricesToCSV : ISelfContainedModule
{
    [RunParameter("Header", "", "The header to apply to the CSV file.")]
    public string Header;

    [SubModelInformation(Description = "The matrices to combine.", Required = true)]
    public IDataSource<SparseTwinIndex<float>>[] Matrices;

    [SubModelInformation(Required = true, Description = "The location to save the CSV to.")]
    public FileLocation SaveTo;

    [RootModule]
    public ITravelDemandModel Root;

    public void Start()
    {
        var flatData = GetFlatData(Matrices);
        var zones = Root.ZoneSystem.ZoneArray.GetFlatData().Select(z => z.ZoneNumber).ToArray();
        using var writer = new StreamWriter(SaveTo);
        writer.WriteLine(Header);
        for (var i = 0; i < zones.Length; i++)
        {
            for (var j = 0; j < zones.Length; j++)
            {
                writer.Write(zones[i]);
                writer.Write(',');
                writer.Write(zones[j]);
                for (var k = 0; k < flatData.Length; k++)
                {
                    writer.Write(',');
                    writer.Write(flatData[k][i][j]);
                }
                writer.WriteLine();
            }
        }
    }

    private float[][][] GetFlatData(IDataSource<SparseTwinIndex<float>>[] matrices)
    {
        return matrices.Select(ds =>
        {
            var loaded = ds.Loaded;
            if (!loaded)
            {
                ds.LoadData();
            }
            var ret = ds.GiveData();
            if (!loaded)
            {
                ds.UnloadData();
            }
            return ret.GetFlatData();
        }).ToArray();
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        var commas = Header.Count(x => x == ',');
        // One less comma than the number of columns
        if (commas != Matrices.Length + 1)
        {
            error = "The headers must be comma separated and have 2 more than the number of matrices.";
            return false;
        }
        return true;
    }
}
