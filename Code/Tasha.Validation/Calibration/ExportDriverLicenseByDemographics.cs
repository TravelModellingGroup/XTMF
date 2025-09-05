/*
    Copyright 2025 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tasha.Common;
using TMG;
using TMG.Input;
using XTMF;

using static Tasha.Validation.Calibration.Utilities;

namespace Tasha.Validation.Calibration;

[ModuleInformation(Description = "Generates a CSV with the column ZoneNumber followed the number of people with the " +
    "selected criteria that live within the zone who hold a license.")]
public sealed class ExportDriverLicenseByDemographics : IPostHousehold
{
    [RootModule]
    public ITravelDemandModel Root;

    [SubModelInformation(Required = true, Description = "The location to write the CSV file.")]
    public FileLocation SaveTo;

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

    [RunParameter("Age Ranges", "0-200", typeof(RangeSet), "The valid ages to get.", Index = 3)]
    public RangeSet AgeRanges;

    [RunParameter("Income Classes", "0-100", typeof(RangeSet), "The household income classes to include.")]
    public RangeSet IncomeClasses;

    private float[] _dlicCounts;

    private SparseArray<IZone> _zones;

    private int _targetIteration;

    public void Load(int maxIterations)
    {
        _targetIteration = maxIterations - 1;
    }

    public void IterationStarting(int iteration)
    {
        _zones = Root.ZoneSystem.ZoneArray;
        if (_dlicCounts is null)
        {
            // [..(HasLicense,NoLicense)]
            _dlicCounts = new float[_zones.Count * 2];
        }
        else
        {
            Array.Clear(_dlicCounts, 0, _dlicCounts.Length);
        }
    }

    private Lock _writeLock = new();

    public void Execute(ITashaHousehold household, int iteration)
    {
        // Only write the last iteration
        if (_targetIteration != iteration)
        {
            return;
        }

        // Check household characteristics for removal
        if(!IncomeClasses.Contains(household.IncomeClass))
        {
            return;
        }

        var householdZone = _zones.GetFlatIndex(household.HomeZone.ZoneNumber) * 2;
        float hasLicense = 0.0f;
        float noLicense = 0.0f;
        foreach (var person in household.Persons)
        {
            if (IsSelectedDemographic(person))
            {
                var expansionFactor = person.ExpansionFactor;
                if (person.Licence)
                {
                    hasLicense += expansionFactor;
                }
                else
                {
                    noLicense += expansionFactor;
                }
            }
        }
        
        lock (_writeLock)
        {
            _dlicCounts[householdZone] += hasLicense;
            _dlicCounts[householdZone + 1] += noLicense;
        }
    }

    private bool IsSelectedDemographic(ITashaPerson person)
    {
        return AgeRanges.Contains(person.Age)
            && IsSelected(_occupations, _rejectOccupations, person.Occupation)
            && IsSelected(_employmentStatuses, _rejectEmploymentStatuses, person.EmploymentStatus)
            && IsSelected(_studentStatuses, _rejectStudentStatuses, person.StudentStatus);
    }

    public void IterationFinished(int iteration)
    {
        // Only write the last iteration
        if (_targetIteration != iteration)
        {
            return;
        }
        Span<char> buffer = stackalloc char[32];
        using var writer = new StreamWriter(SaveTo);
        writer.WriteLine("ZoneNumber,HasLicense,NoLicense,TotalPersons");
        var flatZones = _zones.GetFlatData();
        for (var i = 0; i < flatZones.Length; i++)
        {
            TMG.Functions.Utilities.Write(writer, flatZones[i].ZoneNumber, buffer);
            writer.Write(',');
            TMG.Functions.Utilities.Write(writer, _dlicCounts[i * 2], buffer);
            writer.Write(',');
            TMG.Functions.Utilities.Write(writer, _dlicCounts[i * 2 + 1], buffer);
            writer.Write(',');
            TMG.Functions.Utilities.WriteLine(writer, _dlicCounts[i * 2] + _dlicCounts[i * 2 + 1], buffer);
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
