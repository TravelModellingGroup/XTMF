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

public class ReadFixedFormatODData : IReadODData<float>
{
    [RunParameter("Data Column Length", 13, "The number of text characters for the Data column.")]
    public int DataLenth;

    [RunParameter("Data Column Start", 9, "The starting position of the Data column.")]
    public int DataStart;

    [RunParameter("Destination Column Length", 5, "The number of text characters for the Destination column.")]
    public int DestinationLenth;

    [RunParameter("Destination Column Start", 4, "The starting position of the Destination column.")]
    public int DestinationStart;

    [RunParameter("E To The Data", false, "Return e^(data) instead of just data")]
    public bool EToTheData;

    [RunParameter("File Name", "Data.txt", typeof(FileFromOutputDirectory), "The fixed format text file relative to the output directory.")]
    public FileFromOutputDirectory FixedFormatFile;

    [RunParameter("Origin Column Length", 4, "The number of text characters for the Origin column.")]
    public int OriginLenth;

    [RunParameter("Origin Column Start", 0, "The starting position of the Origin column.")]
    public int OriginStart;

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

    public Tuple<byte, byte, byte> ProgressColour => null;

    public IEnumerable<ODData<float>> Read()
    {
        // if there isn't anything just exit
        if (!FixedFormatFile.ContainsFileName()) yield break;
        // otherwise load in the data
        ODData<float> currentData = new();
        StreamReader reader;
        try
        {
            reader = new StreamReader(FixedFormatFile.GetFileName());
        }
        catch (IOException e)
        {
            throw new XTMFRuntimeException(this, "In '" + Name + "' we were unable to open up the file named '" + FixedFormatFile.GetFileName() + "' with the exception '" + e.Message + "'");
        }
        // find the amount of data in the line that we need in order to process anything
        var dataInLine = Math.Max(OriginStart + OriginLenth, DestinationStart + DestinationLenth);
        dataInLine = Math.Max(dataInLine, DataStart + DataLenth);
        using (reader)
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                // if there is not enough data just continue
                if (line.Length < dataInLine) continue;
                currentData.O = int.Parse(line.Substring(OriginStart, OriginLenth));
                currentData.D = int.Parse(line.Substring(DestinationStart, DestinationLenth));
                if (EToTheData)
                {
                    currentData.Data = (float)(Math.Exp(double.Parse(line.Substring(DataStart, DataLenth))));
                }
                else
                {
                    currentData.Data = (float)double.Parse(line.Substring(DataStart, DataLenth));
                }
                yield return currentData;
            }
        }
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}