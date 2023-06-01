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
    /// <summary>
    /// Provides a common interface to view Modules
    /// </summary>
    /// <remarks>Please do not implement this interface directly.  Instead use
    /// one of IModelExecution(I/O/IO) to describe the inputs and outputs of your model</remarks>
    public interface IModule
    {
        /// <summary>
        /// The name of the instance of model
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// The current level of progress between 0 and 1
        /// </summary>
        float Progress { get; }

        /// <summary>
        /// R,G,B colour used for progress bars
        /// </summary>
        Tuple<byte, byte, byte> ProgressColour { get; }

        /// <summary>
        /// Called to check before the start of a Model System in order to ensure that all modules included
        /// are alright with their parameters.  If there is an error it will return false and describe the error
        /// in detail in the error string.
        /// </summary>
        /// <param name="error">The error string returned by a model that has an error</param>
        /// <returns>True if there are no problems with the configuration of this module</returns>
        bool RuntimeValidation(ref string? error);
    }
}