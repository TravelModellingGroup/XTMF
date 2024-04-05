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
using Tasha.Scheduler;
using TMG;

namespace Tasha.Common;

public interface IEpisode
{
    /// <summary>
    /// The schedule that contains the episode.
    /// Only a schedule should set this property
    /// </summary>
    /// <returns></returns>
    ISchedule ContainingSchedule { get; set; }

    /// <summary>
    /// The person who owns this episode
    /// </summary>
    /// <returns></returns>
    ITashaPerson Owner { get; }

    /// <summary>
    /// The activity that this episode represents
    /// </summary>
    /// <returns></returns>
    Activity ActivityType { get; }

    /// <summary>
    /// The time the episode ends at
    /// </summary>
    Time EndTime { get; }

    /// <summary>
    /// The time the episode starts at
    /// </summary>
    Time StartTime { get; }

    /// <summary>
    /// The duration of the episode
    /// </summary>
    /// <returns></returns>
    Time Duration { get; }

    /// <summary>
    /// The duration of the episode when it was first created
    /// </summary>
    /// <returns></returns>
    Time OriginalDuration { get; }

    /// <summary>
    /// The amount of time to get to the next activity
    /// </summary>
    Time TravelTime { get; }

    /// <summary>
    /// The location that this activity is at
    /// </summary>
    /// <returns></returns>
    IZone Zone { get; }
}