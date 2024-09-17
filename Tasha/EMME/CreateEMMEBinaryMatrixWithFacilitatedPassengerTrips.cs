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
using TMG.Emme;
using TMG.Input;
using TMG;
using XTMF;
using TMG.Functions;

namespace Tasha.EMME;

[ModuleInformation(Description = "Creates a matrix of the facilitated passenger trips for the given time selection.")]
public sealed class CreateEMMEBinaryMatrixWithFacilitatedPassengerTrips : IPostHouseholdIteration
{
    [RootModule]
    public ITravelDemandModel Root;
    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get; set; }

    private float[][] Matrix;

    [SubModelInformation(Required = true, Description = "The location to save the SOV matrix.")]
    public FileLocation MatrixSaveLocation;

    [RunParameter("Start Time", "6:00AM", typeof(Time), "The start of the time to record.")]
    public Time StartTime;

    [RunParameter("End Time", "9:00AM", typeof(Time), "The end of the time to record (non inclusive).")]
    public Time EndTime;

    private SpinLock WriteLock = new(false);

    private int GetFlatIndex(IZone zone)
    {
        return _zoneSystem.GetFlatIndex(zone.ZoneNumber);
    }

    private int HouseholdIterations = 1;

    private record struct Entry(float ExpansionFactor, int FlatOrigin, int FlatDestination);

    public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
    {
        Span<Entry> entries = stackalloc Entry[16];
        int numberOfEntries = 0;

        void AddToMatrix(Span<Entry> entries, Time startTime, float expFactor, int flatOrigin, int flatDestination)
        {
            if ((startTime < StartTime) | (startTime >= EndTime))
            {
                return;
            }
            entries[numberOfEntries++] = new Entry(expFactor, flatOrigin, flatDestination);
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
                if ((tripChains[j].EndTime < StartTime) | (tripChains[j].StartTime) > EndTime)
                {
                    continue;
                }
                if (tripChains[j].JointTrip && !tripChains[j].JointTripRep)
                {
                    continue;
                }
                var trips = tripChains[j].Trips;
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
                        if (driverTripChain != null)
                        {
                            float driverExpansionFactor = driverTripChain.Person.ExpansionFactor;
                            
                            // add in our 3 trip leg data
                            if (driverOrigin != passengerOrigin)
                            {
                                // this really is driver on joint
                                AddToMatrix(entries, startTime, driverExpansionFactor, driverOrigin, passengerOrigin);
                            }
                            
                            if (passengerDestination != driverDestination)
                            {
                                AddToMatrix(entries, startTime, driverExpansionFactor, passengerDestination, driverDestination);
                            }
                        }
                    }
                }
            }
        }

        // If there are any entries left, store them.
        if (numberOfEntries > 0)
        {
            StoreEntries(entries[..numberOfEntries]);
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
            var row = Matrix[entry.FlatOrigin];
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

    private SparseArray<IZone> _zoneSystem;
    private int NumberOfZones;

    public void IterationStarting(int iteration, int totalIterations)
    {
        // get the newest zone system
        _zoneSystem = Root.ZoneSystem.ZoneArray;
        NumberOfZones = _zoneSystem.Count;
        if (Matrix == null)
        {
            Matrix = new float[NumberOfZones][];
            for (int i = 0; i < Matrix.Length; i++)
            {
                Matrix[i] = new float[NumberOfZones];
            }
        }
        else
        {
            // clear out old trips
            for (int i = 0; i < Matrix.Length; i++)
            {
                Array.Clear(Matrix[i], 0, Matrix[i].Length);
            }
        }
    }

    public void IterationFinished(int iteration, int totalIterations)
    {
        //Min each OD to Zero
        MinZero(Matrix);
        // write to disk
        new EmmeMatrix(_zoneSystem, Matrix).Save(MatrixSaveLocation, true);
    }

    private void MinZero(float[][] matrix)
    {
        
        Parallel.For(0, matrix.Length, i =>
        {
            var row = matrix[i];
            for (int j = 0; j < row.Length; j++)
            {
                VectorHelper.Max(row, row, 0.0f);
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
