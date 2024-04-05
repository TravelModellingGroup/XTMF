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

namespace Tasha.ModeChoice;

internal static class ModeChoicePerson
{
    /// <summary>
    /// Calculate the Loglikelihood for this person
    /// </summary>
    /// <param name="person">The person to calculate</param>
    public static void CalculateLoglikelihood(this ITashaPerson person)
    {
        // Generate the best possible trips for each person
        // for each vehicle type
        foreach ( var chain in person.TripChains )
        {
            // Now we need to generate all of the different ways
            // that we can possibly go, that are feasible
            chain.GenerateModeSets();
            // Then select the best modeset for each vehicle type
            chain.SelectBestPerVehicleType();
        }
    }
}