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

public class PDFloatDataSource : IDataSource<SparseTwinIndex<float>>
{
    [SubModelInformation( Required = true, Description = "The module that reads in the data that we will aggregate." )]
    public IReadODData<float> Reader;

    [RunParameter("Apply Default", false, "Should we apply the default value before loading in the data?")]
    public bool ApplyDefault;

    [RunParameter("Default Value", 0.0f, "The default value to use.")]
    public float DefaultValue;

    [RootModule]
    public ITravelDemandModel Root;

    private SparseTwinIndex<float> Data;

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

    public SparseTwinIndex<float> GiveData()
    {
        return Data;
    }

    public bool Loaded
    {
        get { return Data != null; }
    }

    public void LoadData()
    {
        var temp = CreatePDArray().CreateSquareTwinArray<float>();
        if (ApplyDefault)
        {
            ApplyDefaultToData( temp );
        }
        foreach ( var point in Reader.Read() )
        {
            if ( temp.ContainsIndex( point.O, point.D ) )
            {
                temp[point.O, point.D] = point.Data;
            }
        }
        Data = temp;
    }

    private void ApplyDefaultToData(SparseTwinIndex<float> temp)
    {
        var value = DefaultValue;
        var data = temp.GetFlatData();
        if ( data.Length == 0 ) return;
        var row = data[0];
        for (int i = 0; i < row.Length; i++ )
        {
            row[i] = value;
        }
        var length = row.Length * sizeof(float);
        for (int i = 1; i < data.Length; i++)
        {
            Buffer.BlockCopy( row, 0, data[i], 0, length );
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

    private SparseArray<int> CreatePDArray()
    {
        var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
        List<int> pdNumbersFound = new( 10 );
        for ( int i = 0; i < zones.Length; i++ )
        {
            var pdID = zones[i].PlanningDistrict;
            if ( !pdNumbersFound.Contains( pdID ) )
            {
                pdNumbersFound.Add( pdID );
            }
        }
        var pdArray = pdNumbersFound.ToArray();
        return SparseArray<int>.CreateSparseArray( pdArray, pdArray );
    }
}