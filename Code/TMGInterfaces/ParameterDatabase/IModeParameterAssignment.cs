/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;

namespace TMG.ParameterDatabase
{
    public interface IModeParameterAssignment : IModule
    {
        /// <summary>
        /// The mode we are working with
        /// </summary>
        IModeChoiceNode Mode { get; }

        /// <summary>
        /// Build up our parameters to blend with
        /// </summary>
        /// <param name="parameters">The parameters to be loaded</param>
        /// <param name="weight">The amount we will be blending for them</param>
        void AssignBlendedParameters(List<Parameter> parameters, float weight);

        /// <summary>
        /// Assign the given value to the parameter
        /// </summary>
        void AssignParameters(List<Parameter> parameters);

        /// <summary>
        /// Complete the blending and assign the parameters
        /// </summary>
        void FinishBlending();

        /// <summary>
        /// Start up blending mode
        /// </summary>
        void StartBlend();
    }
}