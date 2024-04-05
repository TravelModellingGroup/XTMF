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

namespace TMG.AgentBased.Tours;

public interface ITripChain : ICollection<ITrip>
{
    /// <summary>
    /// The person that this trip chain belongs to
    /// </summary>
    IPerson Person { get; }

    /// <summary>
    /// The index number of this trip chain for
    /// the person.
    /// </summary>
    int TripChainIndex { get; set; }

    /// <summary>
    /// Get the trip in the trip chain
    /// </summary>
    /// <param name="index">The index to get at</param>
    /// <returns></returns>
    ITrip this[int index]
    {
        get;
    }
}