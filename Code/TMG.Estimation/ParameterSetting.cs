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
namespace TMG.Estimation;

/// <summary>
/// This class provides the basis for parameter selection and estimation
/// </summary>
public class ParameterSetting
{
    /// <summary>
    /// The current setting for this parameter
    /// </summary>
    public float Current;
    /// <summary>
    /// The parameters to edit
    /// </summary>
    public string[] Names;
    /// <summary>
    /// The smallest value allowed
    /// </summary>
    public float Minimum;
    /// <summary>
    /// The largest value allowed
    /// </summary>
    public float Maximum;
    /// <summary>
    /// The value to use for the null hypothesis
    /// </summary>
    public float NullHypothesis;
    /// <summary>
    /// The size of the parameter
    /// </summary>
    /// <returns>The length of the parameter in parameter space.</returns>
    public float Size { get { return Maximum - Minimum; } }
}