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

namespace TMG.Distributed
{
    /// <summary>
    /// The distribution manager is tasked with tracking the lifetimes of tasks
    /// and pushing them to the compute nodes.
    /// </summary>
    public interface IHostDistributionManager : IModelSystemTemplate
    {
        /// <summary>
        /// Add a task for execution and track its lifetime.
        /// </summary>
        /// <param name="taskName">The name of task to execute.</param>
        void AddTask(string taskName);

        /// <summary>
        /// Wait for all currently executing tasks to finish
        /// </summary>
        void WaitAll();

        /// <summary>
        /// Wait for all currently executing tasks to finish
        /// or for the timeout to occur
        /// </summary>
        /// <param name="timeoutMilliseconds">The number of milliseconds to wait</param>
        /// <returns>True if all tasks have finished</returns>
        bool WaitAll(int timeoutMilliseconds);

        /// <summary>
        /// The manager of the client side tasks
        /// </summary>
        /// <returns></returns>
        IClientDistributionManager Client { get; }
    }
}
