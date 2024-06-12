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

[ModuleInformation(Description = "")]
public sealed class AgeRange : IModule
{
    [RunParameter("Minimum Age", 0, "The minimum age for this category.")]
    public int MinimumAge;

    [RunParameter("Maximum Age", 200, "The minimum age for this category.")]
    public int MaximumAge;

    [RunParameter("Name", "", "")]
    public string AgeRangeName;

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    /// <summary>
    /// <c>Determines</c> whether the specified age is within the age range.
    /// </summary>
    /// <param name="age">The age to check.</param>
    /// <returns><c>true</c> if the age is within the range; otherwise, <c>false</c>.</returns>
    public bool Contains(int age)
    {
        return (age >= MinimumAge) & (age <= MaximumAge);
    }

}

public static class AgeRangeExtensions
{
    /// <summary>
    /// Gets the index of the age range that contains the specified age.
    /// </summary>
    /// <param name="ageRanges">The array of age ranges to search.</param>
    /// <param name="age">The age to check.</param>
    /// <returns>The index of the age range that contains the specified age, or -1 if no age range contains the age.</returns>
    public static int GetIndex(this AgeRange[] ageRanges, int age)
    {
        for (int i = 0; i < ageRanges.Length; i++)
        {
            if (ageRanges[i].Contains(age))
            {
                return i;
            }
        }
        return -1;
    }
}
