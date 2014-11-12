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

namespace TMG.GTAModel
{
    [ModuleInformation( Description = "This module is designed to allow the representation of a set of ranges."
        + "Typically this is used for expressing indexing into a dataset such as "
        + " which generations should be combined in order to do distribution." )]
    public class BlendSet : IModule
    {
        [RunParameter( "Blend Set", "1", "The set of index to blend together." )]
        public RangeSet Set;

        public string Name { get; set; }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public virtual bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}