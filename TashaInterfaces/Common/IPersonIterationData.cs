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

namespace Tasha.Common
{
    /// <summary>
    /// Iteration Data for a person which corresponds to a household
    /// iteration's mode choices and trip chains
    /// </summary>
    public interface IPersonIterationData
    {
        /// <summary>
        /// Has this household iteration successfully assigned modes to all trips?
        /// </summary>
        bool IterationSuccessful { get; }

        /// <summary>
        /// TripChains is a list of trip chains for this household iteration
        /// </summary>
        List<ITripChain> TripChains { get; set; }

        /// <summary>
        /// The Trip Modes represents all the persons trip and their associated mode
        /// </summary>
        Dictionary<ITrip, ITashaMode> TripModes { get; set; }

        /// <summary>
        /// Gets the mode chosen for the specified trip
        /// Precondition: Must not be an aux trip
        /// </summary>
        /// <param name="trip">The Trip to find</param>
        /// <returns>The mode chosens</returns>
        ITashaMode ModeChosen(ITrip trip);

        /// <summary>
        /// Populates this data with the given persons trip data
        /// </summary>
        /// <param name="person"></param>
        /// <param name="hIteration"></param>
        void PopulateData(ITashaPerson person);

        /// <summary>
        /// Recycle this object for future re-use
        /// </summary>
        void Recycle();
    }
}