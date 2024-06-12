/*
    Copyright 2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;

namespace Tasha.Validation.Report;

[ModuleInformation(Description = "Gives a time bound when generating the report.")]
public sealed class TimePeriod : IModule
{

    [RunParameter("Start Time", "6:00", typeof(Time), "The start time of the time period.", Index = 0)]
    public Time StartTime;

    [RunParameter("End Time", "9:00", typeof(Time), "The end time of the time period, exclusive.", Index = 1)]
    public Time EndTime;

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    private float _startTime;
    private float _endTime;

    public bool RuntimeValidation(ref string error)
    {
        _startTime = StartTime.ToMinutes();
        _endTime = EndTime.ToMinutes();
        return true;
    }

    /// <summary>
    /// Checks if the start time is within the time period. 
    /// </summary>
    /// <param name="tripStartTime">The start time of the trip to test.</param>
    /// <returns>True if the start time of the trip </returns>
    internal bool Contains(Time tripStartTime)
    {
        return (tripStartTime >= StartTime) & (tripStartTime < EndTime);
    }

    /// <summary>
    /// Checks if the start time is within the time period. 
    /// </summary>
    /// <param name="minutesFromMidnight">The start time of the trip to test.</param>
    /// <returns>True if the start time of the trip </returns>
    internal bool Contains(float minutesFromMidnight)
    {
        return (minutesFromMidnight >= _startTime) & (minutesFromMidnight < _endTime);
    }

    /// <summary>
    /// Sets the value to 1 if the specified trip start time falls within the time period, otherwise sets it to 0.
    /// </summary>
    /// <param name="tripStartTime">The trip start time to check.</param>
    /// <returns>1 if the trip start time falls within the time period, otherwise 0.</returns>
    internal float SetIfContains(Time tripStartTime)
    {
        return tripStartTime >= StartTime && tripStartTime < EndTime ? 1 : 0;
    }

}

/// <summary>
/// Provides extension methods for interacting with
/// arrays of TimePeriod.
/// </summary>
public static class TimePeriodExtensions
{

    /// <summary>
    /// Gets the index of the time period that contains the specified trip start time.
    /// </summary>
    /// <param name="periods">The array of time periods to search.</param>
    /// <param name="tripStartTime">The start time of the trip to check.</param>
    /// <returns>The index of the time period that contains the trip start time, or -1 if no time period contains the trip start time.</returns>
    public static int GetIndex(this TimePeriod[] periods, Time tripStartTime)
    {
        for (int i = 0; i < periods.Length; i++)
        {
            if (periods[i].Contains(tripStartTime))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// Gets the index of the time period that contains the specified start time in minutes from midnight.
    /// </summary>
    /// <param name="periods">The array of time periods to search.</param>
    /// <param name="minutesFromMidnight">The start time of the trip to check in minutes from midnight.</param>
    /// <returns>The index of the time period that contains the start time, or -1 if no time period contains the start time.</returns>
    public static int GetIndex(this TimePeriod[] periods, float minutesFromMidnight)
    {
        for (int i = 0; i < periods.Length; i++)
        {
            if (periods[i].Contains(minutesFromMidnight))
            {
                return i;
            }
        }
        return -1;
    }
}