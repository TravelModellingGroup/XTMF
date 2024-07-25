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
using System;
using System.Linq;
using Tasha.Common;
using TMG;
using TMG.Emme;
using TMG.Input;
using XTMF;

namespace Tasha.Validation.Calibration;

[ModuleInformation(Description = "This module allows you to get a matrix of activities conducted" +
    " by people with the given demographics.")]
public sealed class ExtractActivitiesByDemographics : IPostHousehold
{

    [RunParameter("Start Time", "6:00", typeof(Time), "The start time of the activity to capture, inclusive.", Index = 0)]
    public Time StartTime;

    [RunParameter("End Time", "9:00", typeof(Time), "The start time of the activity to capture, exclusive.", Index = 1)]
    public Time EndTime;

    [RunParameter("Age Ranges", "0-200", typeof(RangeSet), "The valid ages to get.", Index = 3)]
    public RangeSet AgeRanges;

    public sealed class SelectedActivity : IModule
    {
        [RunParameter("Activity", Activity.PrimaryWork, "An activity type to include.")]
        public Activity Activity;

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

    [SubModelInformation(Required = true, Description = "The activities to add to the matrix.")]
    public SelectedActivity[] Activities;

    [RootModule]
    public ITravelDemandModel Root;

    private int _targetIteration;

    private Activity[] _containedActivities;

    private SparseArray<IZone> _zones;

    private SparseTwinIndex<float> _matrix;

    [SubModelInformation(Required = true, Description = "The location to save the EMME binary matrix to.")]
    public FileLocation SaveTo;

    public void Load(int maxIterations)
    {
        _targetIteration = maxIterations - 1;
        _containedActivities = Activities.Select(a => a.Activity).ToArray();
    }

    public void IterationStarting(int iteration)
    {
        if (_targetIteration != iteration)
        {
            return;
        }
        // Only do work on the last iteration
        _zones = Root.ZoneSystem.ZoneArray;
        if (_matrix is null)
        {
            _matrix = _zones.CreateSquareTwinArray<float>();
        }
        else
        {
            var flatMatrix = _matrix.GetFlatData();
            for (int i = 0; i < flatMatrix.Length; i++)
            {
                Array.Clear(flatMatrix[i], 0, flatMatrix[i].Length);
            }
        }
    }

    private readonly record struct Entry(int FlatOrigin, int FlatDestination, float ExpansionFactor);

    public void Execute(ITashaHousehold household, int iteration)
    {
        if (_targetIteration != iteration)
        {
            return;
        }
        // Make a buffer for the entries to limit the number of locks
        int numberOfEntries = 0;
        int maxEntries = household.Persons.Length * 10;
        Span<Entry> entries = stackalloc Entry[maxEntries];


        void WriteEntries(Span<Entry> entries)
        {
            if (numberOfEntries <= 0)
            {
                return;
            }
            var flatMatrix = _matrix.GetFlatData();
            lock (this)
            {
                for (int i = 0; i < numberOfEntries; i++)
                {
                    flatMatrix[entries[i].FlatOrigin][entries[i].FlatDestination] += entries[i].ExpansionFactor;
                }
            }
            numberOfEntries = 0;
        }

        void AddEntry(Span<Entry> entries, IZone origin, IZone destination, float expansionFactor)
        {
            var flatOrigin = _zones.GetFlatIndex(origin.ZoneNumber);
            var flatDestination = _zones.GetFlatIndex(destination.ZoneNumber);
            entries[numberOfEntries++] = new Entry(flatOrigin, flatDestination, expansionFactor);
            if (numberOfEntries >= maxEntries)
            {
                WriteEntries(entries);
            }
        }

        // Only do work on the last iteration
        foreach (var person in household.Persons)
        {
            var expansionFactor = person.ExpansionFactor;
            foreach (var tripChain in person.TripChains)
            {
                foreach (var trip in tripChain.Trips)
                {
                    Time startTime = GetStartTime(trip);
                    if (Array.IndexOf(_containedActivities, trip.Purpose) >= 0
                        && startTime >= StartTime && startTime < EndTime
                        && AgeRanges.Contains(person.Age))
                    {
                        AddEntry(entries, trip.OriginalZone, trip.DestinationZone, expansionFactor);
                    }
                }
            }
        }

        WriteEntries(entries);
    }

    public void IterationFinished(int iteration)
    {
        if (_targetIteration != iteration)
        {
            return;
        }
        new EmmeMatrix(_zones, _matrix.GetFlatData())
            .Save(SaveTo, false);
    }
    private static Time GetStartTime(ITrip trip)
    {
        return trip.Purpose switch
        {
            Activity.Home or Activity.StayAtHome or Activity.ReturnFromWork or Activity.ReturnFromSchool => trip.TripStartTime,
            _ => trip.ActivityStartTime,
        };
    }

    public string Name { get; set; }

    public float Progress => throw new NotImplementedException();

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
