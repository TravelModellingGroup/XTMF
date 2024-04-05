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
using TMG;
using XTMF;

namespace Tasha.Common;

/// <summary>
/// Represents a single trip to an event
/// </summary>
public interface ITrip : IAttachable
{
    /// <summary>
    /// When does this trip start?
    /// </summary>
    Time ActivityStartTime { get; }

    /// <summary>
    /// Where does this trip go?
    /// </summary>
    IZone DestinationZone { get; }

    /// <summary>
    /// Used for dropping people off
    /// </summary>
    IZone IntermediateZone { get; set; }

    /// <summary>
    /// Which mode is it going to use
    /// </summary>
    ITashaMode Mode { get; set; }

    ITashaMode[] ModesChosen { get; }

    /// <summary>
    /// Where does this trip start?
    /// </summary>
    IZone OriginalZone { get; }

    List<ITashaPerson> Passengers { get; set; }

    /// <summary>
    /// What is the purpose of this trip?
    /// </summary>
    Activity Purpose { get; set; }

    /// <summary>
    ///
    /// </summary>
    ITashaPerson SharedModeDriver { get; set; }

    Time TravelTime { get; }

    /// <summary>
    /// The chain this trip is in
    /// </summary>
    ITripChain TripChain { get; set; }

    /// <summary>
    /// The associated trip number for a person
    /// </summary>
    int TripNumber { get; }

    Time TripStartTime { get; }

    ITrip Clone();

    void Recycle();
}