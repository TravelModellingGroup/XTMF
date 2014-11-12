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
using TMG.Emme;
using XTMF;

namespace TMG.NetworkEstimation
{
    /// <summary>
    /// Used to support a more moduler form of calculating error
    /// </summary>
    public interface IErrorTally : IModule
    {
        /// <summary>
        /// Compute the error
        /// </summary>
        /// <param name="parameters">The parameters that we are running</param>
        /// <param name="truth">The truth data for the lines</param>
        /// <param name="predicted">The predicted data for the lines</param>
        /// <returns>The computed error value</returns>
        float ComputeError(ParameterSetting[] parameters, TransitLine[] truth, TransitLine[] predicted);
    }
}