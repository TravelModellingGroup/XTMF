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
using System.Collections.Concurrent;
using Tasha.Common;
using XTMF;

namespace Tasha.ModeChoice;

/// <summary>
/// This describes the data for a trip
/// regarding the mode choices.
/// </summary>
internal sealed class ModeData
{
    /// <summary>
    /// The rror terms for each mode
    /// </summary>
    public double[] Error;

    /// <summary>
    /// The Feasibility of the modes
    /// </summary>
    public bool[] Feasible;

    /// <summary>
    /// The V for all of the modes
    /// </summary>
    public double[] V;

    internal static ITashaRuntime TashaRuntime;
    private static ConcurrentBag<ModeData> ModeDataPool = [];

    [ThreadStatic]
    private static Random Random;

    /// <summary>
    /// Create new mode data for a trip
    /// </summary>
    private ModeData()
    {
        V = new double[TashaRuntime.NonSharedModes.Count + TashaRuntime.SharedModes.Count];
        Feasible = new bool[TashaRuntime.NonSharedModes.Count + TashaRuntime.SharedModes.Count];
        Error = new double[TashaRuntime.NonSharedModes.Count + TashaRuntime.SharedModes.Count];
    }

    /// <summary>
    /// Load the mode data from a trip
    /// </summary>
    /// <param name="trip">The trip to load the data from</param>
    /// <returns>The mode data for the trip</returns>
    public static ModeData Get(ITrip trip)
    {
        return (ModeData)trip["MD"];
    }

    public static ModeData MakeModeData()
    {
        ModeDataPool.TryTake(out ModeData md);
        return md ?? new ModeData();
    }

    /// <summary>
    /// Generates the error terms for this mode data
    /// </summary>
    public void GenerateError()
    {
        var modes = TashaRuntime.NonSharedModes;
        var modesLength = modes.Count;
        for (int i = 0; i < modesLength; i++)
        {
            // Don't bother computing things that we won't use
            if (Feasible[i])
            {
                Error[i] = GetNormal() * modes[i].VarianceScale;
            }
            else
            {
                Error[i] = 0;
            }
        }
        var sharedModes = TashaRuntime.SharedModes;
        var sharedModesLength = sharedModes.Count;
        for (int i = 0; i < sharedModesLength; i++)
        {
            // Compute all of the shared modes here
            Error[i + modesLength] = GetNormal() * sharedModes[i].VarianceScale;
        }
    }

    public void Recycle()
    {
        ModeDataPool.Add(this);
    }

    /// <summary>
    /// Store this mode data to a trip
    /// </summary>
    /// <param name="trip">The trip to store this to</param>
    public void Store(ITrip trip)
    {
        trip.Attach("MD", this);
    }

    /// <summary>
    /// Get the U Value for a given mode
    /// </summary>
    /// <param name="mode">The mode to look at</param>
    /// <returns>The Utility of that mode</returns>
    public double U(int mode)
    {
        if ((mode >= V.Length) | (mode < 0))
        {
            throw new XTMFRuntimeException(null, "Tried to access a mode that doesn't exist!");
        }
        return V[mode] + Error[mode];
    }

    private static float GetNormal()
    {
        Random ??= new Random(TashaRuntime.RandomSeed);
        double ret = -6;
        for (int i = 0; i < 12; i++)
        {
            ret += Random.NextDouble();
        }
        return (float)ret;
    }
}