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
using Datastructure;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;
using System;
using Tasha.Common;
using TMG.Input;
using TMG;
using XTMF;
using TMG.Functions;
using System.IO;
using TMG.Emme;

namespace Tasha.EMME;

[ModuleInformation(Description = "Generate matrices where drivers are travelling to facilitate trips by passengers not on their tour.")]
public sealed class CreateHourlyMatricesWithFacilitatePassenger : IPostHouseholdIteration
{
    [RootModule]
    public ITravelDemandModel Root;
    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get; set; }

    private float[][][] Matrix;

    [RunParameter("Matrix Prefix", "Facilitated-", "The name to have before the time bin when saving the matrix in the given directory.")]
    public string Prefix;

    [SubModelInformation(Required = true, Description = "The location to save the SOV matrix.")]
    public FileLocation MatrixSaveLocation;

    private SpinLock WriteLock = new(false);

    private int GetFlatIndex(IZone zone)
    {
        return ZoneSystem.GetFlatIndex(zone.ZoneNumber);
    }

    private int HouseholdIterations = 1;

    private record struct Entry(float ExpansionFactor, int TimeBin, int FlatOrigin, int FlatDestination);

    /// <summary>
    /// Provides a modulo operation that works correctly for negative numbers.
    /// </summary>
    /// <param name="number">The numerator</param>
    /// <param name="divisor">The denominator.</param>
    /// <returns>The modulo of the number, if negative it wraps around back into the positive domain.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static int SafeMod(int numerator, int denominator)
    {
        return ((numerator %= denominator) < 0) ? numerator + denominator : numerator;
    }

    public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
    {
        Span<Entry> entries = stackalloc Entry[16];
        int numberOfEntries = 0;

        void AddToMatrix(Span<Entry> entries, Time startTime, float expFactor, int flatOrigin, int flatDestination)
        {
            // Make sure that we are always in the positive time bin space, even if it takes 2 days.
            var timeBin = SafeMod((int)(startTime.ToMinutes() / 60.0f), 24);
            entries[numberOfEntries++] = new Entry(expFactor, timeBin, flatOrigin, flatDestination);
            if (numberOfEntries == entries.Length)
            {
                StoreEntries(entries);
                numberOfEntries = 0;
            }
        }

        // Gather the number of household iterations
        HouseholdIterations = totalHouseholdIterations;
        // now execute
        var persons = household.Persons;
        for (int i = 0; i < persons.Length; i++)
        {
            var expFactor = persons[i].ExpansionFactor;
            var tripChains = persons[i].TripChains;

            for (int j = 0; j < tripChains.Count; j++)
            {
                if (tripChains[j].JointTrip && !tripChains[j].JointTripRep)
                {
                    continue;
                }
                var trips = tripChains[j].Trips;
                // check to see if we should be running access or egress for this person on their trip chain
                for (int k = 0; k < trips.Count; k++)
                {
                    var startTime = trips[k].TripStartTime;
                    var modeChosen = trips[k].Mode;
                    if (Passenger.Mode == modeChosen)
                    {
                        var driversTrip = trips[k]["Driver"] as ITrip;
                        // driver originData
                        var driverOrigin = GetFlatIndex(driversTrip?.OriginalZone);
                        var passengerOrigin = GetFlatIndex(trips[k].OriginalZone);
                        var passengerDestination = GetFlatIndex(trips[k].DestinationZone);
                        var driverDestination = GetFlatIndex(driversTrip?.DestinationZone);

                        var driverTripChain = driversTrip?.TripChain;
                        if (driverTripChain is not null)
                        {
                            var driverStartTime = driversTrip.TripStartTime;
                            float driverExpansionFactor = driverTripChain.Person.ExpansionFactor;

                            // For this module we only need to check if the driver
                            // needs to travel to the passenger, or continue on after
                            // dropping off the passenger.

                            if (driverOrigin != passengerOrigin)
                            {
                                AddToMatrix(entries, startTime, driverExpansionFactor, driverOrigin, passengerOrigin);
                            }
                            
                            if (passengerDestination != driverDestination)
                            {
                                AddToMatrix(entries, startTime, driverExpansionFactor, passengerDestination, driverDestination);
                            }
                        }
                        else
                        {
                            // If the driver is coming from home
                            throw new XTMFRuntimeException(this, "We found a passenger trip that has no driver!?");
                        }
                    }
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsDriverAlreadyOnRoad(ITrip driversTrip)
    {
        // The pure passenger trip chain only has 1 trip
        // And all other trip chains need to have at least 2
        return driversTrip.TripChain.Trips.Count > 1;
    }

    private void StoreEntries(ReadOnlySpan<Entry> toWrite)
    {
        bool taken = false;
        WriteLock.Enter(ref taken);
        foreach (var entry in toWrite)
        {
            var row = Matrix[entry.TimeBin][entry.FlatOrigin];
            row[entry.FlatDestination] += entry.ExpansionFactor;
        }
        WriteLock.Exit(true);
    }

    public sealed class ModeLink : IModule
    {
        [RootModule]
        public ITashaRuntime Root;

        [RunParameter("Mode Name", "Auto", "The name of the mode")]
        public string ModeName;

        [DoNotAutomate]
        public ITashaMode Mode;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool RuntimeValidation(ref string error)
        {
            foreach (var mode in Root.AllModes)
            {
                if (mode.ModeName == ModeName)
                {
                    Mode = mode;
                    return true;
                }
            }
            error = "In '" + Name + "' we were unable to find a mode called '" + ModeName + "'";
            return false;
        }
    }

    [SubModelInformation(Required = true, Description = "The link to the passenger mode.")]
    public ModeLink Passenger;

    private SparseArray<IZone> ZoneSystem;
    private int NumberOfZones;

    public void IterationStarting(int iteration, int totalIterations)
    {
        // get the newest zone system
        ZoneSystem = Root.ZoneSystem.ZoneArray;
        NumberOfZones = ZoneSystem.Count;
        if (Matrix is null)
        {
            // one matrix for each 15 minute period
            Matrix = new float[24][][];
            for (int timeBin = 0; timeBin < Matrix.Length; timeBin++)
            {
                Matrix[timeBin] = new float[NumberOfZones][];
                for (int i = 0; i < Matrix[timeBin].Length; i++)
                {
                    Matrix[timeBin][i] = new float[NumberOfZones];
                }
            }
        }
        else
        {
            // clear out old trips
            for (int i = 0; i < Matrix.Length; i++)
            {
                for (int j = 0; j < Matrix[i].Length; j++)
                {
                    Array.Clear(Matrix[i][j], 0, Matrix[i][j].Length);
                }
            }
        }
    }

    public void IterationFinished(int iteration, int totalIterations)
    {
        Parallel.For(0, Matrix.Length, (int i) =>
        {
            // write to disk, converting the matrix to normalize the household iterations out.
            VectorHelper.Multiply(Matrix[i], Matrix[i], 1.0f / HouseholdIterations);
            var saveTo = Path.Combine(MatrixSaveLocation, $"{Prefix}{i}.mtx.gz");
            Directory.CreateDirectory(MatrixSaveLocation);
            new EmmeMatrix(ZoneSystem, Matrix[i]).Save(saveTo, true);
        });
    }

    private void MinZero(float[][] matrix)
    {
        Parallel.For(0, matrix.Length, i =>
        {
            var row = matrix[i];
            for (int j = 0; j < row.Length; j++)
            {
                row[j] = Math.Max(row[j], 0.0f);
            }
        });
    }

    public void Load(int maxIterations)
    {

    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void HouseholdComplete(ITashaHousehold household, bool success)
    {

    }

    public void HouseholdStart(ITashaHousehold household, int householdIterations)
    {

    }
}
