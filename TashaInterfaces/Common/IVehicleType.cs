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

namespace Tasha.Common
{
    /// <summary>
    /// Defines a class of vehicle
    /// </summary>
    public interface IVehicleType : IModule
    {
        /// <summary>
        /// Are there a limited set of this type of vehicle
        /// </summary>
        bool Finite { get; }

        /// <summary>
        /// The name for this class of vehicle
        /// </summary>
        string VehicleName { get; }

        /// <summary>
        /// Can the person use this type of vehicle
        /// </summary>
        /// <param name="person">The person to test for</param>
        /// <returns>If the person can use it</returns>
        bool CanUse(ITashaPerson person);
    }
}