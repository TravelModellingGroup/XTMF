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

namespace XTMF
{
    public interface IResource : IModule
    {
        /// <summary>
        /// The unique name for this resource
        /// </summary>
        string ResourceName { get; }

        /// <summary>
        /// Provides access to the resource
        /// </summary>
        /// <typeparam name="T">The type of the resource to try to aquire.</typeparam>
        /// <returns>Returns null if the resource is not of the given type, otherwise provides the resource.</returns>
        T AquireResource<T>();

        /// <summary>
        /// Checks to see if the resource is of the given type
        /// </summary>
        /// <param name="t">The type to check for</param>
        /// <returns></returns>
        bool CheckResourceType(Type dataType);

        /// <summary>
        /// Checks to see if the resource is of the given type
        /// </summary>
        /// <typeparam name="T">The type to check for.</typeparam>
        /// <returns></returns>
        bool CheckResourceType<T>();

        /// <summary>
        /// Release the usage of this resource
        /// </summary>
        void ReleaseResource();

        /// <summary>
        /// Gets the raw data-source contained by this resource.
        /// </summary>
        /// <returns>The contained datasource</returns>
        IDataSource GetDataSource();

        /// <summary>
        /// Get the type that this resource holds
        /// </summary>
        /// <returns>The type that this resource holds</returns>
        Type GetResourceType();
    }
}