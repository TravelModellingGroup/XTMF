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
using Tasha.Common;

namespace Tasha.Validation.Calibration;

internal static class Utilities
{
    /// <summary>
    /// Checks if a value is selected based on the provided selected and rejected arrays.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="selected">The array of selected values.</param>
    /// <param name="rejected">The array of rejected values.</param>
    /// <param name="value">The value to check.</param>
    /// <returns>True if the value is selected, false otherwise.</returns>
    internal static bool IsSelected<T>(T[] selected, T[] rejected, T value)
    {
        if (selected is not null && selected.Length > 0)
        {
            return Array.IndexOf(selected, value) >= 0;
        }
        if (rejected is not null && rejected.Length > 0)
        {
            return Array.IndexOf(rejected, value) < 0;
        }
        return true;
    }

    /// <summary>
    /// Gets the start time for the given trip based on its purpose.
    /// </summary>
    /// <param name="trip">The trip for which to get the start time.</param>
    /// <returns>The start time of the trip.</returns>
    internal static Time GetStartTime(ITrip trip)
    {
        return trip.Purpose switch
        {
            Activity.Home or Activity.StayAtHome or Activity.ReturnFromWork or Activity.ReturnFromSchool => trip.TripStartTime,
            _ => trip.ActivityStartTime,
        };
    }
}
