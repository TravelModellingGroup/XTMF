/*
    Copyright 2021 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG.Input;
using XTMF;
using System.IO;
using Datastructure;

namespace TMG.Frameworks.Data.Saving;

[ModuleInformation(Description = "Saves the given matrix into the ESRI json matrix format.")]
public sealed class SaveODToESRIMatrix : ISelfContainedModule
{
    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    [SubModelInformation(Required = true, Description = "The location to save the file to.")]
    public FileLocation SaveTo;

    [SubModelInformation(Required = true, Description = "The matrix to save.")]
    public IDataSource<SparseTwinIndex<float>> MatrixToSave;

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    private static SparseTwinIndex<float> LoadMatrix(IDataSource<SparseTwinIndex<float>> matrixSource)
    {
        var wasLoaded = matrixSource.Loaded;
        if (!wasLoaded)
        {
            matrixSource.LoadData();
        }
        var ret = matrixSource.GiveData();
        if (!wasLoaded)
        {
            matrixSource.UnloadData();
        }
        return ret;
    }

    public void Start()
    {
        var matrix = LoadMatrix(MatrixToSave);
        var zones = matrix.ValidIndexArray();
        var data = matrix.GetFlatData();
        try
        {
            using var writer = new StreamWriter(SaveTo);
            writer.Write("{\"zone_ids\":[");
            for (int i = 0; i < zones.Length; i++)
            {
                if (i > 0)
                {
                    writer.Write(',');
                }
                writer.Write(zones[i]);
            }
            writer.Write("],\"data\":[");
            for (int i = 0; i < data.Length; i++)
            {
                if (i > 0)
                {
                    writer.Write(',');
                }
                writer.Write('[');
                for (int j = 0; j < data[i].Length; j++)
                {
                    if (j > 0)
                    {
                        writer.Write(',');
                    }
                    // The format requests that data is rounding to two decimal places
                    writer.Write("{0:0.00}", data[i][j]);
                }
                writer.Write(']');
            }
            writer.Write("]}");
        }
        catch (IOException e)
        {
            throw new XTMFRuntimeException(this, e);
        }
    }
}
