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
/// Defines the LinkedParameter structure for XTMF, allowing a single assignment to affect multiple parameters across different modules.
/// </summary>
public interface ILinkedParameter
{
    /// <summary>
    /// The name of the linked parameters
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// The list of parameters that are linked
    /// </summary>
    List<IModuleParameter> Parameters { get; }

    /// <summary>
    /// The string value that all parameters will be assigned to
    /// </summary>
    string Value { get; }

    /// <summary>
    /// Attempt to add the parameter to the set of linked parameters
    /// </summary>
    /// <param name="parameter">The parameter to try to add</param>
    /// <param name="error">An error message in case of failure</param>
    /// <returns>If we were able to add the parameter</returns>
    /// <remarks>If this new parameter is of a non compatible type from the set value this parameter can not be added.</remarks>
    bool Add(IModuleParameter parameter, ref string error);

    /// <summary>
    /// Remove a parameter from the LinkedParameter
    /// </summary>
    /// <param name="parameter">The parameter to remove</param>
    /// <param name="error">An error message in case of failure</param>
    /// <returns>True if the parameter was removed</returns>
    bool Remove(IModuleParameter parameter, ref string error);

    /// <summary>
    /// The value that is to be set to all of the parameters
    /// </summary>
    /// <param name="value">A string that represents it</param>
    /// <param name="error">An error message in case of failure</param>
    /// <returns>If we were successful in assinging the value to all of the parameters</returns>
    bool SetValue(string value, ref string error);
}