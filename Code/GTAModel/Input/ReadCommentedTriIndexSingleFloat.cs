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
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Input;

public class ReadCommentedTriIndexSingleFloat : IDataSource<SparseTriIndex<float>>
{
    [RunParameter( "File Name", "Data.txt", "The file that we will be loading in as a Tri-Indexed data source relative to the mdel systems input directory." )]
    public FileFromInputDirectory FileName;

    [RootModule]
    public IModelSystemTemplate Root;

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
        if ( Data == null )
        {
            LoadTriIndexedData();
        }
    }

    public bool RuntimeValidation(ref string error)
    {
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
        using CommentedCsvReader reader = new(FileName.GetFileName(Root.InputBaseDirectory));
        while (reader.NextLine())
        {
            // skip blank lines
            if (reader.NumberOfCurrentCells < 4)
            {
                continue;
            }
            reader.Get(out int f, 0);
            reader.Get(out int s, 1);
            reader.Get(out int t, 2);
            reader.Get(out float d, 3);
            first.Add(f);
            second.Add(s);
            third.Add(t);
            data.Add(d);
        }
    }

    private void LoadTriIndexedData()
    {
        // first 2 columns are the first 2, the remaineder are the last dimension enumerated
        List<int> first = [];
        List<int> second = [];
        List<int> third = [];
        List<float> data = [];
        StoreData( first, second, third, data );
        Data = SparseTriIndex<float>.CreateSparseTriIndex([.. first], [.. second], [.. third], [.. data]);
    }
}