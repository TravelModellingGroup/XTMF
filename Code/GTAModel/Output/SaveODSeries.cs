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
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Output;

public class SaveODSeries : ISaveODDataSeries<float>
{
    [RunParameter("Input File Format", "BinaryData%X.bin", "The file series to be read in and sumed.  The %X will be replaced by the index number")]
    public FileFromOutputDirectory InputFileBase;

    [RunParameter("Starting Index", 0, "The index of the files to start at.")]
    public int StartingIndex;

    [SubModelInformation(Required = true, Description = "The module to write the data.")]
    public ISaveODData<float> Writer;

    private int CurrentIndex;

    public string Name { get; set; }

    public float Progress
    {
        get { return 0f; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return null; }
    }

    public void Reset()
    {
        CurrentIndex = StartingIndex;
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void SaveMatrix(SparseTwinIndex<float> matrix)
    {
        Writer.SaveMatrix(matrix, GetFilename(CurrentIndex++));
    }

    public void SaveMatrix(float[][] data)
    {
        Writer.SaveMatrix(data, GetFilename(CurrentIndex++));
    }

    public void SaveMatrix(float[] data)
    {
        Writer.SaveMatrix(data, GetFilename(CurrentIndex++));
    }

    private string GetFilename(int index)
    {
        var fileNameWithIndexing = InputFileBase.GetFileName();
        int indexOfInsert = fileNameWithIndexing.IndexOf("%X", StringComparison.InvariantCulture);
        if (indexOfInsert == -1)
        {
            throw new XTMFRuntimeException(this, "In '" + Name
                + "' the parameter 'Input File Format' does not contain a substitution '%X' in order to progress through the series!  Please update the parameter to include the substitution.");
        }
        return fileNameWithIndexing.Insert(indexOfInsert, index.ToString()).Replace("%X", "");
    }
}