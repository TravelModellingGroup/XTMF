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
    public interface ICalculation<O> : IModule
    {
        /// <summary>
        /// Call this before using the module
        /// </summary>
        void Load();

        /// <summary>
        /// Get a result with the use of data
        /// </summary>
        /// <returns>A result based on the given data</returns>
        O ProduceResult();

        /// <summary>
        /// Call this after you have finished using this module, or between iterations
        /// </summary>
        void Unload();
    }

    public interface ICalculation<D, O> : IModule
    {
        /// <summary>
        /// Call this before using the module
        /// </summary>
        void Load();

        /// <summary>
        /// Get a result for the given data
        /// </summary>
        /// <param name="data">The data to use to process the result</param>
        /// <returns>A result based on the given data</returns>
        O ProduceResult(D data);

        /// <summary>
        /// Call this after you have finished using this module, or between iterations
        /// </summary>
        void Unload();
    }
}