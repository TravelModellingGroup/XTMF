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

namespace Tasha.Common
{
    /// <summary>
    /// Implement this to be automatically loaded to run after Tasha# finishes processing all iterations
    /// </summary>
    public interface IPostRun : IModule
    {
        /// <summary>
        /// This will be run after Tasha has finished
        /// </summary>
        void Execute();

        /// <summary>
        /// Load all of the data you need from
        /// the global configuration.
        /// </summary>
        /// <param name="config">Access to the configuration</param>
        void Load(IConfiguration config);
    }
}