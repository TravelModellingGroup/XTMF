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
using XTMF;

namespace TMG.AgentBased.Tours;

/// <summary>
/// This interface describes the basic
/// requirements for what a trip is.
/// </summary>
public interface ITrip
{
    /// <summary>
    /// The time that the
    /// </summary>
    Time ActivityStartTime { get; set; }

    /// <summary>
    /// This is the chosen primary mode.
    /// It will not reflect changes when adding
    /// Intermediate chains
    /// </summary>
    IMode ChosenMode { get; set; }

    /// <summary>
    /// The time that the trip leaves at
    /// </summary>
    Time DepartureTime { get; set; }

    /// <summary>
    /// The location that this trip ends at
    /// </summary>
    IZone Destination { get; }

    /// <summary>
    /// The activity that the person is going to.
    /// </summary>
    IActivity DestinationActivity { get; }

    /// <summary>
    /// If this trip is going to be diverted
    /// from its original destination, this
    /// field will contain the replaned tour.
    /// </summary>
    ITripChain IntermediateChain { get; set; }

    /// <summary>
    /// The location that this trip starts at
    /// </summary>
    IZone Origin { get; }

    /// <summary>
    /// The trip chain that this trip belongs to
    /// </summary>
    ITripChain TripChain { get; }

    /// <summary>
    /// The index into the trip chain for
    /// this trip
    /// </summary>
    int TripIndex { get; set; }
}