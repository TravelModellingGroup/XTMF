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

namespace XTMF;

/// <summary>
/// Allows access to the parameters that a module uses
/// </summary>
public interface IModuleParameters : IEnumerable<IModuleParameter>
{
    /// <summary>
    /// The module that these parameters belong to
    /// </summary>
    IModelSystemStructure BelongsTo { get; }

    /// <summary>
    /// The list of parameters that the model uses
    /// </summary>
    IList<IModuleParameter> Parameters { get; }

    /// <summary>
    /// Make a new copy of the parameters
    /// </summary>
    /// <returns>The copy of the parameters</returns>
    IModuleParameters Clone();

    /// <summary>
    /// Save this list of parameters for the module
    /// </summary>
    void Save();
}