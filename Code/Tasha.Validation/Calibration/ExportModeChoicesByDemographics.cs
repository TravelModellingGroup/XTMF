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
using Tasha.Common;
using TMG.Input;
using TMG;
using XTMF;
using System.Linq;

using static Tasha.Validation.Calibration.Utilities;
using TMG.Emme;

namespace Tasha.Validation.Calibration;

[ModuleInformation(Description = "This module is used to extract out mode choices by particular demographics.")]
public sealed class ExportModeChoicesByDemographics : IPostHouseholdIteration
{

    [RunParameter("Start Time", "6:00", typeof(Time), "The start time of the activity to capture, inclusive.", Index = 0)]
    public Time StartTime;

    [RunParameter("End Time", "9:00", typeof(Time), "The start time of the activity to capture, exclusive.", Index = 1)]
    public Time EndTime;

    [RunParameter("Age Ranges", "0-200", typeof(RangeSet), "The valid ages to get.", Index = 3)]
    public RangeSet AgeRanges;

    [SubModelInformation(Required = true, Description = "The activities to add to the matrix.")]
    public SelectedActivity[] Activities;
    private Activity[] _activities;

    [SubModelInformation(Required = false, Description = "The occupations to select for, leave blank for all.")]
    public SelectedOccupation[] SelectOccupations;
    private Occupation[] _occupations;

    [SubModelInformation(Required = false, Description = "The employment statuses to select for, leave blank for all.")]
    public SelectedEmploymentStatus[] SelectEmploymentStatuses;
    private TTSEmploymentStatus[] _employmentStatuses;

    [SubModelInformation(Required = false, Description = "The student statuses to select for, leave blank for all.")]
    public SelectedStudentStatuses[] SelectStudentStatuses;
    private StudentStatus[] _studentStatuses;

    [SubModelInformation(Required = false, Description = "The occupations to reject.")]
    public SelectedOccupation[] RejectOccupations;
    private Occupation[] _rejectOccupations;

    [SubModelInformation(Required = false, Description = "The employment statuses to reject.")]
    public SelectedEmploymentStatus[] RejectEmploymentStatuses;
    private TTSEmploymentStatus[] _rejectEmploymentStatuses;

    [SubModelInformation(Required = false, Description = "The student statuses to select to reject.")]
    public SelectedStudentStatuses[] RejectStudentStatuses;
    private StudentStatus[] _rejectStudentStatuses;

    [SubModelInformation(Required = true, Description = "The modes to select for.")]
    public SelectedModes[] SelectedModes;

    [DoNotAutomate]
    private ITashaMode[] _modes;

    [RootModule]
    public ITashaRuntime Root;

    private int _targetIteration;

    private Activity[] _containedActivities;

    private SparseArray<IZone> _zones;

    private SparseTwinIndex<float> _matrix;

    [SubModelInformation(Required = true, Description = "The location to save the EMME binary matrix to.")]
    public FileLocation SaveTo;

    bool _execute = false;

    public void IterationStarting(int iteration, int totalIterations)
    {
        _execute = false;
        if (iteration == totalIterations - 1)
        {
            _execute = true;
            _targetIteration = totalIterations - 1;
            _containedActivities = Activities.Select(a => a.Activity).ToArray();
            _activities = Activities.Select(a => a.Activity).ToArray();
            _occupations = SelectOccupations?.Select(o => o.Occupation).ToArray() ?? [];
            _employmentStatuses = SelectEmploymentStatuses?.Select(e => e.EmploymentStatus).ToArray() ?? [];
            _studentStatuses = SelectStudentStatuses?.Select(s => s.StudentStatus).ToArray() ?? [];
            _modes = SelectedModes.GetModes(this, Root);
            _rejectOccupations = RejectOccupations?.Select(o => o.Occupation).ToArray() ?? [];
            _rejectEmploymentStatuses = RejectEmploymentStatuses?.Select(e => e.EmploymentStatus).ToArray() ?? [];
            _rejectStudentStatuses = RejectStudentStatuses?.Select(s => s.StudentStatus).ToArray() ?? [];
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
    }

    public void HouseholdStart(ITashaHousehold household, int householdIterations)
    {
        // Do nothing
    }

    public void HouseholdComplete(ITashaHousehold household, bool success)
    {
        // Do nothing
    }

    public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
    {
        if (!_execute)
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
            if (!AgeRanges.Contains(person.Age)
                    || !IsSelected(_occupations, _rejectOccupations, person.Occupation)
                    || !IsSelected(_employmentStatuses, _rejectEmploymentStatuses, person.EmploymentStatus)
                    || !IsSelected(_studentStatuses, _rejectStudentStatuses, person.StudentStatus)
               )
            {
                continue;
            }
            var expansionFactor = person.ExpansionFactor;
            foreach (var tripChain in person.TripChains)
            {
                foreach (var trip in tripChain.Trips)
                {
                    var mode = trip.Mode;
                    if (!IsModeContained(mode))
                    {
                        continue;
                    }
                    Time startTime = GetStartTime(trip);
                    if (Array.IndexOf(_containedActivities, trip.Purpose) >= 0
                        && startTime >= StartTime && startTime < EndTime)
                    {
                        AddEntry(entries, trip.OriginalZone, trip.DestinationZone, expansionFactor);
                    }
                }
            }
        }

        WriteEntries(entries);
    }
    
    /// <summary>
    /// Checks if the specified mode is contained in the list of modes.
    /// </summary>
    /// <param name="selectedMode">The mode to check.</param>
    /// <returns><c>true</c> if the mode is contained in the list of modes; otherwise, <c>false</c>.</returns>
    private bool IsModeContained(ITashaMode selectedMode)
    {
        return Array.IndexOf(_modes, selectedMode) >= 0;
    }

    public void IterationFinished(int iteration, int totalIterations)
    {
        if(_execute)
        {
            new EmmeMatrix(_zones, _matrix.GetFlatData())
            .Save(SaveTo, false);
        }
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new (50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

}
