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
using TMG;

namespace Tasha.Common
{
    /// <summary>
    /// Defines how a made class will be used by the system
    /// Warnning, this class will be called in a multi-threaded fashion
    /// </summary>
    public interface ITashaMode : IMode
    {
        /// <summary>
        /// Which Vehical [if any] does this mode require
        /// </summary>
        IVehicleType RequiresVehicle { get; }

        double VarianceScale { get; set; }

        /// <summary>
        /// Calculates the V for the given trip using this mode
        /// </summary>
        /// <param name="trip">The trip to calculate for</param>
        /// <returns>The V for this trip</returns>
        double CalculateV(ITrip trip);

        /// <summary>
        /// Check to see if this mode is feasible for the given trip
        /// </summary>
        /// <param name="trip">The trip to check if we can possibly be used for</param>
        /// <returns>If trip is feasible</returns>
        bool Feasible(ITrip trip);

        /// <summary>
        /// Checks to see if this trip chain as a whole is feasable.
        /// </summary>
        /// <remarks>
        /// This is used for modes like Train access, where you need to egress to the same station
        /// so you can pick back up your car.
        /// </remarks>
        /// <param name="tripChain">The trip chain to validate</param>
        /// <returns>If this trip chainis feasable according to this mode</returns>
        bool Feasible(ITripChain tripChain);
    }
}