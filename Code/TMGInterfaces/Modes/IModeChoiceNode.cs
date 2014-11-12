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

namespace TMG
{
    public interface IModeChoiceNode : IModule
    {
        /// <summary>
        /// What percentage of the population can currently use this?
        /// </summary>
        float CurrentlyFeasible { get; set; }

        /// <summary>
        /// The name of the mode category.
        /// This can be used for applying additional factors to their variables
        /// </summary>
        string ModeName { get; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="destination"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        float CalculateV(IZone origin, IZone destination, Time time);

        /// <summary>
        /// See if this node is feasible
        /// </summary>
        /// <param name="origin">The starting zone</param>
        /// <param name="destination">The destination zone</param>
        /// <param name="time">The starting time of day</param>
        /// <returns></returns>
        bool Feasible(IZone origin, IZone destination, Time time);
    }
}