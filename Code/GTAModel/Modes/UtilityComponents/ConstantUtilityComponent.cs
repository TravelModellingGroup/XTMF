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

namespace TMG.GTAModel.Modes.UtilityComponents
{
    public class ConstantUtilityComponent : IUtilityComponent
    {
        [RunParameter( "Constant", 0f, "A constant value to add to the utlity of the containing mode." )]
        public float Constant;

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

        [RunParameter( "Component Name", "Constant", "The name of this Utility Component.  This name should be unique for each mode." )]
        public string UtilityComponentName
        {
            get;
            set;
        }

        public float CalculateV(IZone origin, IZone destination, XTMF.Time time)
        {
            return Constant;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public override string ToString()
        {
            return this.UtilityComponentName;
        }
    }
}