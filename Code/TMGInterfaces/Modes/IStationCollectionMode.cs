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
using XTMF;

namespace TMG.Modes
{
    public interface IStationCollectionMode : IMode
    {
        /// <summary>
        /// Is this mode in access or egress mode?
        /// </summary>
        bool Access { get; set; }

        /// <summary>
        /// Gets the choices for an origin/destination and stores where the access station is
        /// </summary>
        /// <param name="origin">Where the trip will be starting from</param>
        /// <param name="destination">Where the trip will be going to</param>
        /// <param name="time">The time of day of the trip</param>
        /// <returns>An array of access station zones and another array of the
        /// correlated probabilities of using that access station</returns>
        Tuple<IZone[], IZone[], float[]> GetSubchoiceSplit(IZone origin, IZone destination, Time time);
    }
}