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
using System;

namespace TMG.Estimation;

public interface IEstimationClientModelSystem : IModelSystemTemplate
{
    /// <summary>
    /// The function that will be executed in order to get the fitness of the parameters
    /// </summary>
    Func<float> RetrieveValue { get; set; }
    /// <summary>
    /// The client model system that will run for each parameter set,
    /// </summary>
    IModelSystemTemplate MainClient { get; }
    /// <summary>
    /// The current set of parameters that we are working on, and its context
    /// </summary>
    ClientTask CurrentTask { get; }
    /// <summary>
    /// A copy of the parameters.  The current value of that parameters are not reflective
    /// of what is being processed.  Use CurrentTask for that.  The order of parameters
    /// will be the same.
    /// </summary>
    ParameterSetting[] Parameters { get; }
}