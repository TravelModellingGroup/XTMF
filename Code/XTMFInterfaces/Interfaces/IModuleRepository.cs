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

namespace XTMF
{
    /// <summary>
    /// Provides access to all of the loaded
    /// models in XTMF
    /// </summary>
    public interface IModuleRepository : IEnumerable<Type>
    {
        /// <summary>
        /// Provides access to the modules that
        /// exist in this XTMF installation
        /// </summary>
        IList<Type> Modules { get; }

        /// <summary>
        /// Add a new module to the module repository
        /// </summary>
        /// <param name="module">The module that you wish to add</param>
        /// <returns>If the module was able to be added successfully</returns>
        bool AddModule(Type module);

        Type GetModuleType(string typeName);

        /// <summary>
        /// Unload a type from the repository
        /// </summary>
        /// <param name="type">The type to remove from the IModuleRepository</param>
        void Unload(Type type);
    }
}