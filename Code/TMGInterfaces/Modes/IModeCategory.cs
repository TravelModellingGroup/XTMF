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
using XTMF;

namespace TMG
{
    public interface IModeCategory : IModeChoiceNode
    {
        /// <summary>
        /// The children of this category
        /// </summary>
        List<IModeChoiceNode> Children { get; }

        /// <summary>
        /// The relation between the nested modes
        /// </summary>
        float Correlation { get; }

        /// <summary>
        /// Calculate the V component for this upper level
        /// without the logsum of the children
        /// </summary>
        /// <param name="origin">Where we are starting from</param>
        /// <param name="destination">Where we are going to</param>
        /// <param name="time">The time of day the trip starts at</param>
        /// <returns>The utility of the upper level</returns>
        float CalculateCombinedV(IZone origin, IZone destination, Time time);
    }
}