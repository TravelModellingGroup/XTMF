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
using XTMF;

namespace Tasha.Common
{
    /// <summary>
    /// This interfect provides Tasha# a way to access the plug-in that provides it with households
    /// </summary>
    public interface ILoadHousehold : IModule
    {
        /// <summary>
        /// Gets a specific household based on the given id
        /// returns null if specific household is not found
        /// </summary>
        /// <param name="id">the household id</param>
        /// <returns>the household or null if it doesnt exist</returns>
        ITashaHousehold GetHousehold(int id);

        /// <summary>
        /// Gets an enumeration of the households that we will be processing
        /// </summary>
        /// <param name="config">The configuration file for this run</param>
        /// <returns></returns>
        IEnumerable<ITashaHousehold> GetHouseholds();

        /// <summary>
        /// This is called after Tasha# has finished using the household
        /// </summary>
        /// <param name="household">The household that can be released</param>
        void HouseholdFinished(ITashaHousehold household);
    }
}