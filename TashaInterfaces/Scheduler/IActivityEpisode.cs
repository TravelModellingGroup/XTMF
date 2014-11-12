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
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Scheduler
{
    public interface IActivityEpisode
    {
        /// <summary>
        /// The location for this activity episode
        /// </summary>
        IZone ActivityLocation { get; set; }

        /// <summary>
        /// The person who owns this activity
        /// </summary>
        ITashaPerson ActivityRepresentative { get; }

        /// <summary>
        /// The project that created this Activity Episode
        /// </summary>
        IProject CreatingProject { get; }

        /// <summary>
        /// The duration of the Activity Episode
        /// </summary>
        Time Duration { get; set; }

        /// <summary>
        /// The end time of the Activity Episode
        /// </summary>
        Time EndTime { get; set; }

        /// <summary>
        /// Holds if this activity belongs to the household, or to an individual
        /// </summary>
        bool IsHouseholdActivity { get; }

        /// <summary>
        ///
        /// </summary>
        ITashaPerson[] JointActivityParticipants { get; }

        /// <summary>
        /// The scheduling priority for this Activity Episode, the higher the faster it is processed
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// The activity purpose for this Activity Episode
        /// </summary>
        Activity Purpose { get; }

        /// <summary>
        /// The start time of the Activity Episode
        /// </summary>
        Time StartTime { get; set; }

        /// <summary>
        /// The resistance to time changes for this Activity Episode
        /// </summary>
        float Weight { get; }
    }
}