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
using Datastructure;
using XTMF;

namespace TMG;

public interface IZoneSystem : IDataSource<IZoneSystem>
{
    /// <summary>
    /// A 2D Sparse representation of the distances between the zones
    /// </summary>
    SparseTwinIndex<float> Distances { get; }

    /// <summary>
    /// Get the number of External Zones
    /// </summary>
    int NumberOfExternalZones { get; }

    /// <summary>
    /// Get the number of Internal Zones
    /// </summary>
    int NumberOfInternalZones { get; }

    /// <summary>
    /// The number of zones
    /// </summary>
    int NumberOfZones { get; }

    /// <summary>
    /// The zone number for roaming places of work
    /// </summary>
    int RoamingZoneNumber { get; set; }

    /// <summary>
    /// A sparse representation of the zone system
    /// </summary>
    SparseArray<IZone> ZoneArray { get; }

    IZone Get(int zoneNumber);
}