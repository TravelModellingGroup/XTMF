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

namespace XTMF
{
    [AttributeUsage( AttributeTargets.Field | AttributeTargets.Property )]
    public class SubModelInformation : Attribute
    {
        /// <summary>
        ///
        /// </summary>
        public SubModelInformation()
        {
            Description = String.Empty;
        }

        /// <summary>
        /// What this model does, or should do
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Is this model required to execute?
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Provides a hint for the order in which sub-models should be ordered
        /// when being displayed to the user.
        /// </summary>
        /// <remarks><
        /// If multiple submodules have the same index they will be ordered in alphabetical order.
        /// </remarks>
        public int Index { get; set; } = int.MaxValue;
    }
}