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
using TMG.Modes;
using XTMF;

namespace TMG.GTAModel.Modes
{
    [ModuleInformation( Description = "This mode is designed to be a base framework for Utility Components.  No other calculation is performed besides "
        + "from the UtilityComponents.  Feasibility is always true untill the Feasibility Calculation is filled in with an "
        + "ICalculation<Pair<IZone, IZone>, bool> module." )]
    public class BlankMode : IUtilityComponentMode
    {
        [SubModelInformation( Description = "Used to test for mode feasibility.", Required = false )]
        public ICalculation<Pair<IZone, IZone>, bool> FeasibilityCalculation;

        [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
        public float CurrentlyFeasible { get; set; }

        [RunParameter( "Mode Name", "", "The name of this mode.  It should be unique to every other mode." )]
        public string ModeName { get; set; }

        public string Name { get; set; }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        [SubModelInformation( Description = "The components used to build the utility of the mode.", Required = false )]
        public List<IUtilityComponent> UtilityComponents { get; set; }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            float total = 0f;
            for ( int i = 0; i < UtilityComponents.Count; i++ )
            {
                total += UtilityComponents[i].CalculateV( origin, destination, time );
            }
            return total;
        }

        public bool Feasible(IZone origin, IZone destination, Time time)
        {
            if ( CurrentlyFeasible <= 0 )
            {
                return false;
            }
            if ( FeasibilityCalculation != null )
            {
                return FeasibilityCalculation.ProduceResult( new Pair<IZone, IZone>( origin, destination ) );
            }
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}