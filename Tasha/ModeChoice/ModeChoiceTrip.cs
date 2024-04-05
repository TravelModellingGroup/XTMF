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

/// <summary>
/// This class provides mode choice functionality for
/// Trips
/// </summary>
internal static class ModeChoiceTrip
{
    internal static ITashaRuntime TashaRuntime;

    /// <summary>
    /// Calculate the non random part of the utility for the trip
    /// For each mode
    /// </summary>
    /// <param name="trip">The trip to work on</param>
    public static bool CalculateVTrip(this ITrip trip)
    {
        ModeData data = ModeData.MakeModeData();
        //initializes mode set
        data.Store( trip );
        bool feasible = false;
        var modes = TashaRuntime.NonSharedModes;
        int numberOfModes = modes.Count;
        for ( int i = 0; i < numberOfModes; i++ )
        {
            // start processing the next mode number
            if ( !( data.Feasible[i] = modes[i].Feasible( trip ) ) )
            {
                continue;
            }
            feasible = true;
            data.V[i] = modes[i].CalculateV( trip );
        }
        // This will generate the error for all of the modes
        // including the shared modes
        data.GenerateError();
        return feasible;
    }

    /// <summary>
    /// Get all of the V's for this trip, one for each mode.
    /// </summary>
    /// <param name="trip">The trip to get this from</param>
    /// <returns>An array of V's for each mode</returns>
    public static double[] GetV(this ITrip trip)
    {
        return ModeData.Get( trip ).V;
    }
}