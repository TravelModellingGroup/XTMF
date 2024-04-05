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
using TMG.Modes;
using XTMF;

namespace TMG.GTAModel.Modes.UtilityComponents;

public class ParkingCostUtility : IUtilityComponent
{
    [RunParameter( "Parking Factor", 1f, "The factor applied to the cost of the parking in the destination zone." )]
    public float ParkingFactor;

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

    [RunParameter( "Utility Component Name", "Parking", "The unique name of this component to link with the mode parameter database." )]
    public string UtilityComponentName
    {
        get;
        set;
    }

    public float CalculateV(IZone origin, IZone destination, Time time)
    {
        return destination.ParkingCost * ParkingFactor;
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}