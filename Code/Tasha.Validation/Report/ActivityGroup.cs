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
using TMG;
using XTMF;

namespace Tasha.Validation.Report;

/// <summary>
/// Represents a group of activities to report on.
/// </summary>
[ModuleInformation(Description = "Represents a group of activities to report on.")]
public sealed class ActivityGroup : IModule
{

    [ModuleInformation(Description = "A reference to a mode to bind to.")]
    public sealed class Activity : IModule
    {

        [RunParameter("Name", nameof(Common.Activity.PrimaryWork), typeof(Common.Activity), "The activity to bind to.")]
        public Common.Activity ActivityType;

        internal string _activityType = string.Empty;

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

        public bool RuntimeValidation(ref string error)
        {           
            _activityType = Enum.GetName(ActivityType);
            return true;
        }
    }

    [SubModelInformation(Required = true, Description = "")]
    public Activity[] Activities;

    /// <summary>
    /// Determines whether the array of <see cref="ActivityGroup"/> contains the specified <see cref="Common.Activity"/>.
    /// </summary>
    /// <param name="activity">The <see cref="Common.Activity"/> to check for.</param>
    /// <returns><c>true</c> if the array contains the specified activity; otherwise, <c>false</c>.</returns>
    public bool Contains(Common.Activity activity)
    {
        for (int i = 0; i < Activities.Length; i++)
        {
            if (Activities[i].ActivityType == activity)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Determines whether the array of <see cref="ActivityGroup"/> contains the specified activity.
    /// </summary>
    /// <param name="activity">The activity to check for.</param>
    /// <returns><c>true</c> if the array contains the specified activity; otherwise, <c>false</c>.</returns>
    public bool Contains(string activity)
    {
        for (int i = 0; i < Activities.Length; i++)
        {
            if (Activities[i]._activityType == activity)
            {
                return true;
            }
        }
        return false;
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50,150,50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

}

/// <summary>
/// Provides extensions for the <see cref="ActivityGroup"/> class.
/// </summary>
public static class ActivityGroupExtensions
{
    
    /// <summary>
    /// Gets the index of the specified <see cref="Common.Activity"/> in the array of <see cref="ActivityGroup"/>.
    /// </summary>
    /// <param name="activityGroups">The array of <see cref="ActivityGroup"/>.</param>
    /// <param name="activity">The <see cref="Common.Activity"/> to search for.</param>
    /// <returns>The index of the specified activity in the array of activity groups, or -1 if not found.</returns>
    public static int GetIndex(this ActivityGroup[] activityGroups, Common.Activity activity)
    {
        for (int i = 0; i < activityGroups.Length; i++)
        {
            if (activityGroups[i].Contains(activity))
            {
                return i;
            }
        }
        return -1;
    }

    
    /// <summary>
    /// Determines whether the array of <see cref="ActivityGroup"/> contains the specified activity.
    /// </summary>
    /// <param name="activityGroups">The array of <see cref="ActivityGroup"/>.</param>
    /// <param name="activity">The activity to search for.</param>
    /// <returns>The index of the specified activity in the array of activity groups, or -1 if not found.</returns>
    public static int GetIndex(this ActivityGroup[] activityGroups, string activity)
    {
        for (int i = 0; i < activityGroups.Length; i++)
        {
            if (activityGroups[i].Contains(activity))
            {
                return i;
            }
        }
        return -1;
    }
    
}
