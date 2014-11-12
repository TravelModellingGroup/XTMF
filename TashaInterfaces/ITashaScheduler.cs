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
using XTMF;

namespace Tasha.Common
{
    public interface ITashaScheduler : IModule
    {
        /// <summary>
        /// Load the data that the scheduler needs to initialize
        /// </summary>
        void LoadOneTimeLocalData();

        /// <summary>
        /// Run the scheduler on the given household
        /// </summary>
        /// <param name="household"></param>
        void Run(ITashaHousehold household);

        /// <summary>
        /// Calculate the travel time between two different zones
        /// </summary>
        /// <param name="person">The person that is travelling</param>
        /// <param name="origin">The starting zone</param>
        /// <param name="destination">The ending zone</param>
        /// <param name="tashaTime">The start time of the trip</param>
        /// <returns>The length of time needed to make the trip</returns>
        Time TravelTime(ITashaPerson person, IZone origin, IZone destination, Time tashaTime);
    }
}