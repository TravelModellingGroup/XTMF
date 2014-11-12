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

namespace XTMF
{
    public interface IModelSystemRepository : IEnumerable<IModelSystem>
    {
        /// <summary>
        /// The model systems currently loaded in this
        /// installation of XTMF
        /// </summary>
        IList<IModelSystem> ModelSystems { get; }

        /// <summary>
        /// add a new type of model system to the model system repository
        /// </summary>
        /// <param name="modelSystem">The type that implements IModelSystem</param>
        void Add(IModelSystem modelSystem);

        /// <summary>
        /// Remove the model system from the model system repository
        /// </summary>
        /// <param name="modelSystem">The model system to remove</param>
        /// <returns>True if a model system was removed, false if it was not (or doe snot exist)</returns>
        bool Remove(IModelSystem modelSystem);
    }
}