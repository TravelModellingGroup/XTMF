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
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace Tasha.Common
{
    public interface ITourDependentMode : ITashaMode
    {
        /// <summary>
        /// Calculates the tour dependent portion of the utility for the trip
        /// </summary>
        /// <param name="chain">The trip chain to evaluate</param>
        /// <param name="tripIndex">The index in the chain that we are computing</param>
        /// <param name="dependentUtility">The utility to add to the independent portion of the utility</param>
        /// <param name="OnSelection">A function that can act on the trip chain.  This will be executed before passenger mode is evaluated.</param>
        /// <returns>True if the tour is feasible</returns>
        bool CalculateTourDependentUtility(ITripChain chain, int tripIndex, out float dependentUtility, out Action<ITripChain> OnSelection);
    }
}
