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
namespace Tasha.Scheduler;

public enum ScheduleConflictType
{
    /// <summary>
    /// There is no conflict
    /// </summary>
    NoConflict,

    /// <summary>
    /// This event goes before the positioned episode
    /// </summary>
    Prior,

    /// <summary>
    /// This event goes at the end of the positioned episode
    /// </summary>
    Posterior,

    /// <summary>
    /// The event that you are trying to add needs to go inbetween another event
    /// </summary>
    Split,

    /// <summary>
    /// The event that you are trying to add completely overlaps another event
    /// </summary>
    CompleteOverlap
}

/// <summary>
/// Describes the type of conflict that comes from the insertion of an episode
/// </summary>
public struct ConflictReport
{
    /// <summary>
    /// The location described by the Conflict Type
    /// </summary>
    public int Position;

    /// <summary>
    /// The type of conflict that will occure
    /// </summary>
    public ScheduleConflictType Type;
}