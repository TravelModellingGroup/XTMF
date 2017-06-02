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
    /// This allows for a plug-in to
    /// do something after a household has been finished processing
    /// </summary>
    public interface IPostHousehold : IModule
    {
        /// <summary>
        /// This gets called what TASHA finishes processing a household
        /// </summary>
        /// <param name="household"></param>
        /// <param name="iteration"></param>
        void Execute(ITashaHousehold household, int iteration);

        /// <summary>
        /// Called when an iteration is complete
        /// </summary>
        /// <param name="iteration"></param>
        void IterationFinished(int iteration);

        /// <summary>
        /// Loads the module, with configuration
        /// information
        /// </summary>
        /// <param name="maxIterations">The number of iterations</param>
        void Load(int maxIterations);

        /// <summary>
        /// This will be called before a new iteration begins
        /// </summary>
        /// <param name="iteration"></param>
        void IterationStarting(int iteration);
    }
}