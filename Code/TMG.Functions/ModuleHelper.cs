/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.IO;

namespace TMG.Functions
{
    /// <summary>
    /// This class is designed to contain functions to aid in module development
    /// </summary>
    public static class ModuleHelper
    {
        /// <summary>
        /// Invoke this method during runtime to ensure that only one of the two given are setup to run and that only one is available.
        /// </summary>
        /// <param name="caller">The method in runtime validation</param>
        /// <param name="resource"></param>
        /// <param name="error">A description of the error if one occurs</param>
        /// <param name="dataSource"></param>
        /// <returns>True if there is no issue, false if both are either filler out or none are.</returns>
        public static bool EnsureExactlyOneAndOfSameType<T>(this IModule caller, IDataSource<T> dataSource, IResource resource, ref string error)
        {
            return caller.EnsureExactlyOne(dataSource, resource, ref error) || !caller.EnsureTypesMatch(dataSource, resource, ref error);
        }

        /// <summary>
        /// Invoke this method during runtime to ensure that only one of the two given are setup to run.
        /// </summary>
        /// <param name="caller">The method in runtime validation</param>
        /// <param name="first">The first module to test</param>
        /// <param name="second">The second module to test</param>
        /// <param name="error">A description of the error if one occurs</param>
        /// <returns>True if there is no issue, false if both are either filler out or none are.</returns>
        public static bool EnsureExactlyOne(this IModule caller, IModule first, IModule second, ref string error)
        {
            if (!(first != null ^ second != null))
            {
                if (first != null)
                {
                    error = "In '" + caller.Name + "' both of the modules '" + first.Name + "' and '" + second.Name + "' have been initialized and where only one is allowed!";
                }
                else
                {
                    error = "In '" + caller.Name + "' neither of the data modules have been initialized!  One of these modules must be in order to continue.";
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// Invoke this method during runtime validation to ensure the type of a resource is the same as a datasource.
        /// </summary>
        /// <typeparam name="T">The type of data</typeparam>
        /// <param name="caller">The calling module</param>
        /// <param name="dataSource">The datasource module to check against</param>
        /// <param name="resource">The resource to check</param>
        /// <param name="error">An error message if one occurs</param>
        /// <returns>True if there is nothing wrong, false otherwise with message.</returns>
        public static bool EnsureTypesMatch<T>(this IModule caller, IDataSource<T> dataSource, IResource resource, ref string error)
        {
            if (resource != null && !resource.CheckResourceType<T>())
            {
                error = "In '" + caller.Name + "' the resource '" + resource.Name + "' was not setup with a datasource of type '" + typeof(T).FullName + "'!";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Get the data from either module.  Optionally unload the data afterwards.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="dataSource">The datasource to check</param>
        /// <param name="resource">The resource to check</param>
        /// <param name="unloadData">Should the source be unloaded afterwards</param>
        /// <returns>The data from one of the modules</returns>
        public static T GetDataFromDatasourceOrResource<T>(IDataSource<T> dataSource, IResource resource, bool unloadData = false)
        {
            T ret;
            if (dataSource != null)
            {
                dataSource.LoadData();
                ret = dataSource.GiveData();
                if (unloadData)
                {
                    dataSource.UnloadData();
                }
            }
            else
            {
                ret = resource.AcquireResource<T>();
                if (unloadData)
                {
                    resource.ReleaseResource();
                }
            }
            return ret;
        }
    }
}
