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

namespace TMG.Modes
{
    /// <summary>
    /// Provides support for arbitary utility functions
    /// </summary>
    public interface IUtilityComponent : IModule
    {
        /// <summary>
        /// The name of this utility component.  Each mode's utility components should be all named uniquely.
        /// </summary>
        string UtilityComponentName { get; }

        /// <summary>
        /// Calculate this subcomponent of the utility function
        /// </summary>
        /// <param name="origin">The starting location</param>
        /// <param name="destination">The destination location</param>
        /// <param name="time">The time of day this happens at</param>
        /// <returns></returns>
        float CalculateV(IZone origin, IZone destination, Time time);
    }
}