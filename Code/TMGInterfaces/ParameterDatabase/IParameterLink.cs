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

namespace TMG.ParameterDatabase
{
    public interface IParameterLink : IModule
    {
        /// <summary>
        /// The name of the paraemter to access
        /// </summary>
        string ParameterName { get; }

        /// <summary>
        /// Assign the parameter to the variable
        /// </summary>
        void Assign(string value);

        /// <summary>
        /// Work on the blending
        /// </summary>
        /// <param name="value">The value to try to blend</param>
        /// <param name="ammount">From 0 to 1 of how much of this parameter to use</param>
        void BlendedAssignment(string value, float ammount);

        /// <summary>
        /// Complete the blending procedure
        /// </summary>
        void FinishBlending();

        /// <summary>
        /// Start the blending mode
        /// </summary>
        void StartBlending();
    }
}