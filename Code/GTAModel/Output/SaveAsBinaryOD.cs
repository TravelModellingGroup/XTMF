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
using System.IO;
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Output;

[ModuleInformation(
    Description = "Save data in a binary format.  For each zone it will save each OD as a floating point number without any formatting."
    )]
public class SaveAsBinaryOD : ISaveODData<float>
{
    [RootModule]
    public ITravelDemandModel Root;

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
        return true;
    }

    public void SaveMatrix(SparseTwinIndex<float> matrix, string fileName)
    {
        var dir = Path.GetDirectoryName( fileName );
        if ( !String.IsNullOrWhiteSpace( dir ) )
        {
            if ( !Directory.Exists( dir ) )
            {
                Directory.CreateDirectory( dir );
            }
        }
        using var writer = new BinaryWriter(File.Open(fileName, FileMode.Create));
        var data = matrix.GetFlatData();
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == null)
            {
                for (int j = 0; j < data.Length; j++)
                {
                    writer.Write((float)0);
                }
            }
            else
            {
                SaveLine(data[i], writer);
            }
        }
        writer.Flush();
    }

    public void SaveMatrix(float[][] data, string fileName)
    {
        var dir = Path.GetDirectoryName( fileName );
        if ( !String.IsNullOrWhiteSpace( dir ) )
        {
            if ( !Directory.Exists( dir ) )
            {
                Directory.CreateDirectory( dir );
            }
        }
        using var writer = new BinaryWriter(File.Open(fileName, FileMode.Create));
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == null)
            {
                for (int j = 0; j < data.Length; j++)
                {
                    writer.Write((float)0);
                }
            }
            else
            {
                SaveLine(data[i], writer);
            }
        }
    }

    public void SaveMatrix(float[] data, string fileName)
    {
        var dir = Path.GetDirectoryName( fileName );
        if ( !String.IsNullOrWhiteSpace( dir ) )
        {
            if ( !Directory.Exists( dir ) )
            {
                Directory.CreateDirectory( dir );
            }
        }
        using var writer = new BinaryWriter(File.Open(fileName, FileMode.Create));
        SaveLine(data, writer);
    }

    private static void SaveLine(float[] oneLine, BinaryWriter writer)
    {
        var temp = new byte[oneLine.Length * sizeof( float )];
        Buffer.BlockCopy( oneLine, 0, temp, 0, temp.Length );
        writer.Write( temp, 0, temp.Length );
    }
}