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
using XTMF.Interfaces;

namespace XTMF
{
    /// <summary>
    /// The defines a specific instantiation of a ModelSystemTemplate
    /// </summary>
    public interface IModelSystem
    {
        /// <summary>
        /// A string that describes what this model system is for
        /// and about it.
        /// </summary>
        string Description { get; set; }

        List<ILinkedParameter> LinkedParameters { get; }

        /// <summary>
        /// Region Displays as part of this model system.
        /// </summary>
        List<IRegionDisplay> RegionDisplays { get; }

        /// <summary>
        /// The internal structure of the model system
        /// </summary>
        IModelSystemStructure ModelSystemStructure { get; set; }

        /// <summary>
        /// The name of the model system
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Save the model system to disk
        /// </summary>
        bool Save(ref string error);

        /// <summary>
        /// Saves the model system to this disk with the given file name
        /// </summary>
        /// <param name="fileName">The file name to save it at</param>
        /// <param name="error"></param>
        bool Save(string fileName, ref string error);

        /// <summary>
        /// Validate the correctness /completeness of the model system
        /// </summary>
        /// <param name="error">If there is an error, this still will be filled in with additional information about it</param>
        /// <returns>If there was something invalid found in the model system</returns>
        bool Validate(ref string error);
    }
}