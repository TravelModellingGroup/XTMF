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
using XTMF;

namespace TMG.GTAModel.Modes.FeasibilityCalculations;

public class MaximumDistance : ICalculation<Pair<IZone, IZone>, bool>
{
    [RunParameter( "Maximum Distance", 3000f, "The maximum distance in meters." )]
    public float MaxDist;

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

    public void Load()
    {
    }

    public bool ProduceResult(Pair<IZone, IZone> data)
    {
        return Root.ZoneSystem.Distances[data.First.ZoneNumber, data.Second.ZoneNumber] <= MaxDist;
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void Unload()
    {
    }
}