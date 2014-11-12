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
    /// Represents the interface in which to
    /// attach information to an object in
    /// Tasha#
    /// </summary>
    public interface IAttachable
    {
        /// <summary>
        /// Gets the name of the items stored in the object
        /// </summary>
        IEnumerable<string> Keys { get; }

        /// <summary>
        /// This is another way to call GetVariable
        /// </summary>
        /// <param name="name">The name you gave to the variable</param>
        /// <returns>The variable with that name</returns>
        object this[string name] { get; }

        /// <summary>
        /// Attach the information with the
        /// given name of...
        /// </summary>
        /// <param name="name">The name to attach it with</param>
        /// <param name="value">The object to attach to that name</param>
        void Attach(string name, object value);

        /// <summary>
        /// Gets the variable with the given name
        /// </summary>
        /// <param name="name">The name you attached this variable with</param>
        /// <exception cref="System.Exception">Thrown if the variable does not exist</exception>
        /// <returns>The variable with that name</returns>
        object GetVariable(string name);

        /// <summary>
        /// Call this to release all resources
        /// </summary>
        void Release();
    }
}