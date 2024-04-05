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
using System;
using Tasha.Common;

namespace Tasha.Scheduler;

/// <summary>
/// This interface provides access to different types of schedules that can be produced by a
/// factory for consumption.
/// </summary>
public interface ISchedule
{

    /// <summary>
    /// Used for previous generation Tasha Schedules
    /// </summary>
    /// <returns></returns>
    IEpisode[] Episodes { get; }

    /// <summary>
    /// Get the set of episodes in this schedule currently, in order of start time.
    /// </summary>
    /// <returns>A list of episodes that will be scheduled. Nulls are permitted, please make
    /// sure to check them for null before using!</returns>
    IActivityEpisode[] GenerateScheduledEpisodeList();

    /// <summary>
    /// Insert the episode into the schedule
    /// </summary>
    /// <param name="householdRandom">The household's random number generator.</param>
    /// <param name="episode">The episode to insert into the schedule</param>
    void Insert(Random householdRandom, IActivityEpisode episode);

    /// <summary>
    /// Insert the episode into another episode, splitting it.
    /// </summary>
    /// <param name="householdRandom">The household's random number generator.</param>
    /// <param name="episode">The episode to insert into the schedule.</param>
    /// <param name="into">The episode that will be split in order to add in the new episode</param>
    void InsertInside(Random householdRandom, IActivityEpisode episode, IActivityEpisode into);
}