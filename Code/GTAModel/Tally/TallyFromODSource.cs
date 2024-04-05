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
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Tally;

[ModuleInformation( Description =
    "This module is designed to facilitate the loading of data straight into a tally from any applicable IReadODData<float> compatible source, such as .311 file or an ordered 'O,D,Data' CSV file"
    )]
public class TallyFromODSource : IModeAggregationTally
{
    [SubModelInformation( Description = "The source to read in the data to apply to the tally.", Required = true )]
    public IReadODData<float> DataSource;

    [RootModule]
    public I4StepModel Root;

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
        get;
        set;
    }

    public void IncludeTally(float[][] currentTally)
    {
        var zoneArray = Root.ZoneSystem.ZoneArray;
        foreach ( var point in DataSource.Read() )
        {
            var o = zoneArray.GetFlatIndex( point.O );
            var d = zoneArray.GetFlatIndex( point.D );
            if ( ( o >= 0 ) & ( o < currentTally.Length ) )
            {
                if ( ( d >= 0 ) & ( d < currentTally[o].Length ) )
                {
                    currentTally[o][d] += point.Data;
                }
            }
        }
    }

    public virtual bool RuntimeValidation(ref string error)
    {
        return true;
    }
}