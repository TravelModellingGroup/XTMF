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
    public interface IProject
    {
        /// <summary>
        /// A description of what the project is for
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Lets the project know if any of its setting shave changed
        /// </summary>
        bool HasChanged { get; set; }

        /// <summary>
        /// A List for each model of linked parameters
        /// </summary>
        IReadOnlyList<List<ILinkedParameter>> LinkedParameters { get; }

        /// <summary>
        /// The models that this project contains
        /// </summary>
        IReadOnlyList<IModelSystemStructure> ModelSystemStructure { get; }

        /// <summary>
        /// The name of the project
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Creates the model system from the project structure.
        /// </summary>
        /// <param name="error">Additional information in case of an error</param>
        /// <param name="modelSystem">The model system index to create</param>
        /// <returns>The root of the model system, null if the Project Structure is invalid</returns>
        IModelSystemTemplate CreateModelSystem(ref string error, int modelSystem);

        /// <summary>
        /// Reload the project to its saved state
        /// </summary>
        void Reload();

        /// <summary>
        /// Save the project
        /// </summary>
        bool Save(ref string error);

        /// <summary>
        /// Save the project to a given path
        /// </summary>
        /// <param name="path">The path to save to</param>
        /// <param name="error"></param>
        bool Save(string path, ref string error);

        /// <summary>
        /// Checks to make sure that this model name is acceptable for this
        /// project.
        /// </summary>
        /// <param name="possibleNewModelName">The name that you want to validate</param>
        /// <returns>If that name is valid for this project</returns>
        /// <remarks>Names are invalid if the name is a duplicate of another one already in the project,
        /// or if it contains characters now allowed in a file name.</remarks>
        bool ValidateModelName(string possibleNewModelName);

        /// <summary>
        /// Finds the index of the given model system.
        /// Returns -1 if it is not found.
        /// </summary>
        /// <param name="realModelSystemStructure">The model system to find.</param>
        /// <returns>The index for this model system, -1 if it is not found.</returns>
        int IndexOf(IModelSystemStructure modelSystemStructure);
    }
}