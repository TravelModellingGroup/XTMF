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

namespace Tasha.Common;

/// <summary>
/// Various methods to determining availability of drivers, vehicles etc for trips.
/// </summary>
public interface IResourceAvailability : IModule
{
    /// <summary>
    /// Gets a driver for the given trip, which must use the IMode specified
    /// as the mode of transport.
    /// </summary>
    /// <param name="trip"></param>
    /// <param name="mode"></param>
    /// <returns></returns>
    bool AssignPossibleDrivers(ITrip trip, ISharedMode mode);

    /// <summary>
    /// Determines if a vehicle is available at the given time for the household
    /// </summary>
    /// <param name="veqType"></param>
    /// <param name="start"></param>
    /// <param name="end"></param>
    /// <param name="hh"></param>
    /// <returns></returns>

    int NumVehiclesAvailable(IVehicleType veqType, Time start, Time end, ITashaHousehold hh);
}