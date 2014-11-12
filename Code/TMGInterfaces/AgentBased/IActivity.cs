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
using TMG.AgentBased.Agents;
using XTMF;

namespace TMG.AgentBased.Tours
{
    public interface IActivity : IModule
    {
        /// <summary>
        /// The name of the activity
        /// </summary>
        string ActivityName { get; }

        /// <summary>
        /// Is this activity a household activity?
        /// </summary>
        bool HouseholdActivity { get; }

        /// <summary>
        /// How important is this activity for this person?
        /// </summary>
        /// <param name="p">The person to check against</param>
        /// <returns>The priority for this activity</returns>
        ActivityPriorities ActivityPriority(IPerson p);

        /// <summary>
        /// Create a list of activity episodes for this person
        /// </summary>
        /// <param name="p">The person to generate the activities for</param>
        /// <returns></returns>
        List<IActivityEpisode> GenerateActivities(IPersonAgent p);

        /// <summary>
        /// Create a list of activity episodes for the household
        /// </summary>
        /// <param name="h">The household to generate activities for</param>
        /// <returns>A list of activities for this household</returns>
        List<IActivityEpisode> GenerateActivities(IHouseholdAgent h);
    }
}