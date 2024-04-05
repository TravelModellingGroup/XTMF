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
using TMG.GTAModel.DataUtility;
using XTMF;

namespace TMG.GTAModel.Modes.FeasibilityCalculations;

[ModuleInformation( Description =
    @"This module is designed to return true if the first data point is in a different region from the second.  It will 
also return true if the region is inside of the 'Excepted Regions'.  This is likely to be used as part of a mode feasibility test." )]
public class DifferentRegions : ICalculation<Pair<IZone, IZone>, bool>
{
    [RunParameter( "Excepted Regions", "1", typeof( NumberList ), "The regions to excuse from this feasibility calculation." )]
    public NumberList Exceptions;

    public void Load()
    {

    }

    public bool ProduceResult(Pair<IZone, IZone> data)
    {
        var first = data.First.RegionNumber;
        if ( first != data.Second.RegionNumber )
        {
            return true;
        }
        // the only other way for it to work is if they are in the excepted list
        return Exceptions.Contains( first );
    }

    public void Unload()
    {

    }

    public string Name { get; set; }

    public float Progress { get { return 0f; } }

    public Tuple<byte, byte, byte> ProgressColour { get { return null; } }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
