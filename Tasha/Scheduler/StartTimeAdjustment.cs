/*
    Copyright 2022 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
using System;
using XTMF;

namespace Tasha.Scheduler;

/// <summary>
/// Used for calibrating the start time of activity
/// episodes.
/// </summary>
public sealed class StartTimeAdjustment : IModule
{
    [RunParameter("Distribution ID Range", "0-262", typeof(RangeSet), 0, "The distribution ID's to alter.")]
    public RangeSet DistributionIDs;

    [RunParameter("Home Planning Districts", "1-46", typeof(RangeSet), 1, "The planning districts to alter.  The home zone is used for comparison.")]
    public RangeSet HomePlanningDistricts;

    [RunParameter("Work Planning District", "0-46", typeof(RangeSet), 2, "The planning district of work for the person, or 0 if there is no work zone.")]
    public RangeSet WorkPlanningDistrict;

    [RunParameter("StartTime", "6:00", typeof(Time), 3, "The start time that this will apply to.")]
    public Time StartTime
    {
        get { return _startTime; }
        set
        {
            _startTime = value;
            StartTimeQuantum = TimeToQuantum(value);
        }
    }

    private Time _startTime;

    [RunParameter("EndTime", "9:00", typeof(Time), 4, "The end time that this will apply to.")]
    public Time EndTime
    {
        get { return _endTime; }
        set
        {
            _endTime = value;
            EndTimeQuantum = TimeToQuantum(value);
        }
    }

    private Time _endTime;

    private static int TimeToQuantum(Time time)
    {
        return (int)(time - Time.StartOfDay).ToMinutes() / 15;
    }

    [RunParameter("Factor", 1.0f, 5, "The factor to apply to this modification for start time rates in the time range.")]
    public float Factor;

    /// <summary>
    /// The start time quantum 15 minute index
    /// </summary>
    public int StartTimeQuantum;

    /// <summary>
    /// The end time quantum 15 minute index
    /// </summary>
    public int EndTimeQuantum;

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
