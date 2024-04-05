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

namespace Tasha.Common;

/// <summary>
/// Represents a vehicle type for Tasha#
/// </summary>
public interface IVehicle
{
    /// <summary>
    /// Which household does this vehicle belong to
    /// </summary>
    ITashaHousehold Household { get; }

    /// <summary>
    /// Which modes are this vehicle used for
    /// </summary>
    ICollection<ITashaMode> Modes { get; }

    /// <summary>
    /// What type of vehicle is this?
    /// </summary>
    IVehicleType VehicleType { get; }

    /// <summary>
    /// Let go of all resources and
    /// get ready to be re-used for
    /// another household
    /// </summary>
    void Recycle();
}