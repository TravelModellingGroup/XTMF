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
    /// Defines a parameter for a model
    /// </summary>
    public interface IModuleParameter
    {
        /// <summary>
        /// The module that this parameter belongs to
        /// </summary>
        IModelSystemStructure BelongsTo { get; }

        /// <summary>
        /// A description of what this parameter is for
        /// </summary>
        string Description { get; }

        /// <summary>
        /// The name of the parameter
        /// </summary>
        string Name { get; }

        /// <summary>
        /// True if it is on a field, false if it is on a property
        /// </summary>
        bool OnField { get; }

        /// <summary>
        /// Should this parameter be displayed in the common parameters?
        /// </summary>
        bool QuickParameter { get; set; }

        /// <summary>
        /// True if this is a system level parameter and should only be
        /// set when being edited by a model system editor
        /// </summary>
        bool SystemParameter { get; }

        /// <summary>
        /// The type for the parameter, might be null if not specified
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// The value of the parameter
        /// </summary>
        object Value { get; set; }

        /// <summary>
        /// The name of the field this is attached to
        /// </summary>
        string VariableName { get; }

        /// <summary>
        /// Create a copy of this parameter
        /// </summary>
        /// <returns>The copy of this parameter</returns>
        IModuleParameter Clone();
    }
}