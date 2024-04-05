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
using System.Collections.Generic;
using System.IO;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Input;

public class ReadODBinaryData : IReadODData<float>
{
    [RunParameter("Input File", "Data.bin", typeof(FileFromOutputDirectory), "The name of the file to load in, based in the current run directory.")]
    public FileFromOutputDirectory FileToRead;

    [RootModule]
    public ITravelDemandModel Root;

    public string Name
    {
        get;
        set;
    }

    public float Progress
    {
        get { return 0; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return null; }
    }

    public IEnumerable<ODData<float>> Read()
    {
        // if there isn't anything just exit
        if (!FileToRead.ContainsFileName()) yield break;
        // otherwise load in the data
        ODData<float> currentData = new();
        var zoneArray = Root.ZoneSystem.ZoneArray;
        var zoneNumbers = zoneArray.ValidIndexArray();
        var zones = zoneArray.GetFlatData();

        BinaryReader reader;
        try
        {
            reader = new BinaryReader(File.OpenRead(FileToRead.GetFileName()));
        }
        catch (IOException e)
        {
            throw new XTMFRuntimeException(this, "In '" + Name + "' we were unable to open up the file named '" + FileToRead.GetFileName() + "' with the exception '" + e.Message + "'");
        }
        var fileSize = reader.BaseStream.Length;
        // make sure the file is of the right size (a float is 4 bytes)
        if (fileSize != 4 * zones.Length * zones.Length)
        {
            reader.Close();
            throw new XTMFRuntimeException(this, "In '" + Name + "' we found the file named '" + FileToRead.GetFileName() + "' was not a flat binary OD data file for the current zone system!");
        }
        using (reader)
        {
            for (int o = 0; o < zones.Length; o++)
            {
                currentData.O = zoneNumbers[o];
                for (int d = 0; d < zones.Length; d++)
                {
                    currentData.D = zoneNumbers[d];
                    currentData.Data = reader.ReadSingle();
                    yield return currentData;
                }
            }
        }
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}