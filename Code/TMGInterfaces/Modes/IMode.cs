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

namespace TMG
{
    public interface IMode : IModeChoiceNode
    {
        /// <summary>
        /// Get the type (name) of the network type to store the information in
        /// </summary>
        string NetworkType { get; }

        /// <summary>
        /// Gets if this mode does not use a personal vehical
        /// </summary>
        bool NonPersonalVehicle { get; }

        /// <summary>
        /// How much does it cost to go between the zones?
        /// </summary>
        /// <param name="origin">Where to start from</param>
        /// <param name="destination">Where to go to</param>
        /// <returns>Cost of going between the zones</returns>
        float Cost(IZone origin, IZone destination, Time time);

        /// <summary>
        /// Get how long it will take to get between zones
        /// </summary>
        /// <param name="origin">The zone to start from</param>
        /// <param name="destination">The zone to go to</param>
        /// <param name="time">The time of the day in (hhmm.ss)</param>
        /// <returns>Travel time in minutes between the zones, Not a Number if it is not possible</returns>
        Time TravelTime(IZone origin, IZone destination, Time time);
    }
}