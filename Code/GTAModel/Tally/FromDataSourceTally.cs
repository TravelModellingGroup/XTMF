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
using System.Threading.Tasks;
using Datastructure;
using XTMF;

namespace TMG.GTAModel.Tally;

public class FromDataSourceTally : IModeAggregationTally
{
    [RootModule]
    public ITravelDemandModel Root;

    [SubModelInformation( Required = true, Description = "The data source used for the tally." )]
    public IDataSource<SparseTwinIndex<float>> Source;

    public string Name
    {
        get;
        set;
    }

    public float Progress
    {
        get { return 0f; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return null; }
    }

    public void IncludeTally(float[][] currentTally)
    {
        Source.LoadData();
        var data = Source.GiveData().GetFlatData();
        Source.UnloadData();
        var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
        Parallel.For( 0, zones.Length, i =>
            {
                var row = currentTally[i];
                var dataRow = data[i];
                if ( row == null | dataRow == null ) return;
                for ( int j = 0; j < zones.Length; j++ )
                {
                    row[j] += dataRow[j];
                }
            } );
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}