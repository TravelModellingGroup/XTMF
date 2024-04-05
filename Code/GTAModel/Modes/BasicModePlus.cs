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

using System.Collections.Generic;
using TMG.Modes;
using XTMF;

namespace TMG.GTAModel.Modes;

[ModuleInformation( Description = "This module provides all of the features of TMG.GTAModel.Modes.BasicMode with the addition of having support for IUtilityComponent modules to allow for more specific calculations." )]
public class BasicModePlus : BasicMode, IUtilityComponentMode
{
    [SubModelInformation( Description = "Additional systematic utility functions.", Required = false )]
    public List<IUtilityComponent> UtilityComponents
    {
        get;
        set;
    }

    public override float CalculateV(IZone originZone, IZone destinationZone, Time time)
    {
        return base.CalculateV( originZone, destinationZone, time ) + CalculateUtilityComponents( originZone, destinationZone, time );
    }

    private float CalculateUtilityComponents(IZone originZone, IZone destinationZone, Time time)
    {
        float total = 0f;
        if ( UtilityComponents != null )
        {
            foreach ( var uc in UtilityComponents )
            {
                total += uc.CalculateV( originZone, destinationZone, time );
            }
        }
        return total;
    }
}