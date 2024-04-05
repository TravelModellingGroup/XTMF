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
using System.Linq;
using Datastructure;
using TMG.GTAModel.DataUtility;
using XTMF;

namespace TMG.GTAModel.Input;

[ModuleInformation(Description =
    @"The goal of this class is to provide a dynamic way of reading in csv data.  
The expected format is a number of header lines, given in as a parameter.  Following that it is 
expecting the first two columns to represent the indexes of the first two dimensions.  
The third dimension is expressed by its “Data-column Sparse Space” parameter and the reading 
of that data in the columns that follow along in the row.  These columns are then linked to sparse 
spaces by referencing each column to the parameter, thusly the number of columns for data are expected 
to be the same as in the parameter.")]
public class ReadTriIndexFloatData : IDataSource<SparseTriIndex<float>>
{
    [RunParameter("Data-Column Sparse Space", "1,2,3,4", typeof(NumberList), "The data column's sparse space indexes. (1,2,3,4)")]
    public NumberList DataColumnToSparseSpace;

    [RunParameter("File Name", "Data.txt", "The file that we will be loading in as a Tri-Indexed data source.")]
    public string FileName;

    [RunParameter("First Data Column", 2, "The first column containing data (0 indexed).")]
    public int FirstDataColumn;

    [RunParameter("First Dimension Column", 0, "The column number containing the first dimension (0 indexed).")]
    public int FirstDimensionColumn;

    [RunParameter("Number of Header Lines", 5, "The number of lines before data starts.")]
    public int NumberOfHeaderLines;

    [RootModule]
    public IModelSystemTemplate Root;

    [RunParameter("Second Dimension Column", 1, "The column number containing the second dimension (0 indexed).")]
    public int SecondDimensionColumn;

    protected SparseTriIndex<float> Data;

    public string Name
    {
        get;
        set;
    }

    public float Progress
    {
        get;
        set;
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return null; }
    }

    public SparseTriIndex<float> GiveData()
    {
        return Data;
    }

    public bool Loaded
    {
        get { return Data != null; }
    }

    public void LoadData()
    {
        if (Data == null)
        {
            LoadTriIndexedData();
        }
    }

    public bool RuntimeValidation(ref string error)
    {
        if (DataColumnToSparseSpace.Count < 1)
        {
            error = "In " + Name + " the number of columns must be greater than zero!";
            return false;
        }
        return true;
    }

    public void UnloadData()
    {
        Data = null;
    }

    /// <summary>
    /// Override this method in order to provide the ability to load different formats
    /// </summary>
    /// <param name="first">The first dimension sparse address</param>
    /// <param name="second">The second dimension sparse address</param>
    /// <param name="third">The third dimension sparse address</param>
    /// <param name="data">The data to be stored at the address</param>
    protected virtual void StoreData(List<int> first, List<int> second, List<int> third, List<float> data)
    {
        try
        {
            using CsvReader reader = new(GetFileLocation(FileName));
            var numberOfDataColumns = DataColumnToSparseSpace.Count;
            BurnHeader(reader);
            var dataSpace = DataColumnToSparseSpace.ToArray();
            while (!reader.EndOfFile)
            {
                // skip blank lines
                if (reader.LoadLine() == 0) continue;
                int t;

                reader.Get(out int f, FirstDimensionColumn);
                reader.Get(out int s, SecondDimensionColumn);
                for (int dataCol = 0; dataCol < numberOfDataColumns; dataCol++)
                {
                    t = dataSpace[dataCol];
                    reader.Get(out float d, dataCol + FirstDataColumn);
                    first.Add(f);
                    second.Add(s);
                    third.Add(t);
                    data.Add(d);
                }
            }
        }
        catch (IOException e)
        {
            throw new XTMFRuntimeException(this, e.Message);
        }
    }

    private void BurnHeader(CsvReader reader)
    {
        for (int i = 0; i < NumberOfHeaderLines; i++)
        {
            reader.LoadLine();
        }
    }

    private string GetFileLocation(string fileName)
    {
        var fullPath = fileName;
        if (!Path.IsPathRooted(fullPath))
        {
            fullPath = Path.Combine(Root.InputBaseDirectory, fullPath);
        }
        return fullPath;
    }

    private void LoadTriIndexedData()
    {
        // first 2 columns are the first 2, the remainder are the last dimension enumerated
        List<int> first = [];
        List<int> second = [];
        List<int> third = [];
        List<float> data = [];
        StoreData(first, second, third, data);
        Data = SparseTriIndex<float>.CreateSparseTriIndex([.. first], [.. second], [.. third], [.. data]);
    }
}