/*
    Copyright 2023 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Runtime.CompilerServices;
using Tasha.Common;
using TMG;
using TMG.Input;
using XTMF;
using Range = Datastructure.Range;

namespace Tasha.XTMFScheduler;

[ModuleInformation(Description = "Provides the ability to assign if a person is" +
    " going to telecommute to work for the given day based on their age, occupation, and workplace.")]
public sealed class RateBasedTelecommuting : ICalculation<ITashaPerson, bool>
{
    [RunParameter("Random Seed", 12345, "The seed to use to fix random number generation")]
    public int RandomSeed;

    [SubModelInformation(Required = true, Description = "")]
    public FileLocation RateFile;

    [RootModule]
    public ITravelDemandModel Root;

    enum WorkPlaceType
    {
        All = 0,
        UsualPlaceOfWork = 1,
        NonFixedPlaceOfWork = 2,
    }

    enum EmploymentStatuses
    {
        FullTime = 1,
        PartTime = 2,
    }

    record struct Entry(Range Ages, Range Income, Range EmploymentStatus, char Occupation, WorkPlaceType Workplace, float Rate);

    private List<Entry> _entries = new();
    private IZone _roamingZone;
    private Random _random;

    public void Load()
    {
        _entries.Clear();
        _random = new Random(RandomSeed);
        _roamingZone = Root.ZoneSystem.Get(Root.ZoneSystem.RoamingZoneNumber);
        // Load in the CSV file for the different demographics
        using var reader = new CsvReader(RateFile, false);
        reader.LoadLine(); // burn header
        var any = false;
        while (reader.LoadLine(out int columns))
        {
            if (columns < 6)
            {
                continue;
            }
            any = true;
            Range ages = ParseAge(reader);
            Range income = ParseIncome(reader);
            Range employmentStatus = ParseEmploymentStatus(reader);
            char occupation = ParseOccupation(reader);
            WorkPlaceType workplace = ParseWorkPlace(reader);
            float rate = GetRate(reader);
            _entries.Add(new Entry(ages, income, employmentStatus, occupation, workplace, rate));
        }

        // Ensure that we loaded at least record.
        if(!any)
        {
            throw new XTMFRuntimeException(this, "No telecommuting records were loaded in. Please make sure there are 6 columns in the order of (AgeRange, IncomeClass," +
                " EmploymentStatuses, Occupation, WorkplaceType, Rate).");
        }
    }

    private Range ParseAge(CsvReader reader)
    {
        reader.Get(out string ages, 0);
        int first;
        var dashIndex = ages.IndexOf('-');
        var plusIndex = ages.IndexOf('+');
        if (dashIndex < 0)
        {
            var end = plusIndex >= 0 ? plusIndex : ages.Length;
            if (!int.TryParse(ages.AsSpan(0, end), out first))
            {
                throw new XTMFRuntimeException(this, $"Unable to parse '{ages}' on line {reader.LineNumber}, invalid text for an age.");
            }
            return new Range(first, plusIndex >= 0 ? int.MaxValue : first);
        }
        var firstSpan = ages.AsSpan(0, dashIndex);
        var secondSpan = ages.AsSpan(dashIndex + 1, ages.Length - dashIndex - 1);
        if (!int.TryParse(firstSpan, out first))
        {
            throw new XTMFRuntimeException(this, $"Unable to parse '{ages}' on line {reader.LineNumber}, when processing the left side of the dash.");
        }
        if(!int.TryParse(secondSpan, out var second))
        {
            throw new XTMFRuntimeException(this, $"Unable to parse '{ages}' on line {reader.LineNumber}, when processing the right side of the dash.");
        }
        return new Range(first, second);       
    }

    private Range ParseIncome(CsvReader reader)
    {
        reader.Get(out string income, 1);
        if(income.Contains("all", StringComparison.OrdinalIgnoreCase))
        {
            return new Range(1, 7);
        }
        int first;
        var dashIndex = income.IndexOf('-');
        var plusIndex = income.IndexOf('+');
        if (dashIndex < 0)
        {
            var end = plusIndex >= 0 ? plusIndex : income.Length;
            if (!int.TryParse(income.AsSpan(0, end), out first))
            {
                throw new XTMFRuntimeException(this, $"Unable to parse '{income}' on line {reader.LineNumber}, invalid text for an income class..");
            }
            return new Range(first, plusIndex >= 0 ? int.MaxValue : first);
        }
        var firstSpan = income.AsSpan(0, dashIndex);
        var secondSpan = income.AsSpan(dashIndex + 1, income.Length - dashIndex - 1);
        if (!int.TryParse(firstSpan, out first))
        {
            throw new XTMFRuntimeException(this, $"Unable to parse '{income}' on line {reader.LineNumber}, when processing the left side of the dash.");
        }
        if (!int.TryParse(secondSpan, out var second))
        {
            throw new XTMFRuntimeException(this, $"Unable to parse '{income}' on line {reader.LineNumber}, when processing the right side of the dash.");
        }
        return new Range(first, second);
    }

    private Range ParseEmploymentStatus(CsvReader reader)
    {
        reader.Get(out string employmentStatus, 2);

        if (employmentStatus.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return new Range(1, 2);
        }
        else if (employmentStatus.Equals("ft", StringComparison.OrdinalIgnoreCase))
        {
            return new Range(1, 1);
        }
        else if (employmentStatus.Equals("pt", StringComparison.OrdinalIgnoreCase))
        {
            return new Range(2, 2);
        }
        throw new XTMFRuntimeException(this, $"Unable to parse '{employmentStatus}' on line {reader.LineNumber}");
    }

    private char ParseOccupation(CsvReader reader)
    {
        reader.Get(out string occupationStatus, 3);
        if (occupationStatus.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return 'A';
        }
        else if (occupationStatus.Equals("p", StringComparison.OrdinalIgnoreCase))
        {
            return (char)Occupation.Professional;
        }
        else if (occupationStatus.Equals("g", StringComparison.OrdinalIgnoreCase))
        {
            return (char)Occupation.Office;
        }
        else if (occupationStatus.Equals("s", StringComparison.OrdinalIgnoreCase))
        {
            return (char)Occupation.Retail;
        }
        else if (occupationStatus.Equals("m", StringComparison.OrdinalIgnoreCase))
        {
            return (char)Occupation.Manufacturing;
        }
        throw new XTMFRuntimeException(this, $"Unable to parse the occupation status '{occupationStatus}' on line {reader.LineNumber}");
    }

    private WorkPlaceType ParseWorkPlace(CsvReader reader)
    {
        reader.Get(out string workplace, 4);

        if (workplace.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return WorkPlaceType.All;
        }
        else if (workplace.Equals("upw", StringComparison.OrdinalIgnoreCase))
        {
            return WorkPlaceType.UsualPlaceOfWork;
        }
        else if (workplace.Equals("nfpw", StringComparison.OrdinalIgnoreCase))
        {
            return WorkPlaceType.NonFixedPlaceOfWork;
        }
        throw new XTMFRuntimeException(this, $"Unable to parse '{workplace}' on line {reader.LineNumber}");
    }

    private float GetRate(CsvReader reader)
    {
        reader.Get(out float rate, 5);
        return rate;
    }

    public bool ProduceResult(ITashaPerson data)
    {
        var income = data.Household.IncomeClass;
        var age = data.Age;
        foreach (var entry in _entries)
        {
            if (entry.Ages.ContainsInclusive(age)
                && entry.Income.ContainsInclusive(income)
                && TestEmploymentStatus(entry.EmploymentStatus, data.EmploymentStatus)
                && TestWorkplace(entry.Workplace, data.EmploymentZone)
                && TestOccupation(entry, data.Occupation))
            {
                return _random.NextSingle() < entry.Rate;
            }
        }
        // By default people do not do telecommuting.
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool TestOccupation(Entry entry, Occupation occupation)
    {
        return entry.Occupation switch
        {
            'A' => true,
            _ => entry.Occupation == (char)occupation,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool TestEmploymentStatus(Range targetEmp, TTSEmploymentStatus personEmp)
    {
        return personEmp switch
        {
            TTSEmploymentStatus.FullTime => targetEmp.ContainsInclusive((int)EmploymentStatuses.FullTime),
            TTSEmploymentStatus.PartTime => targetEmp.ContainsInclusive((int)EmploymentStatuses.PartTime),
            _ => false
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private bool TestWorkplace(WorkPlaceType workplace, IZone employmentZone)
    {
        return workplace switch
        {
            WorkPlaceType.All => true,
            WorkPlaceType.NonFixedPlaceOfWork => employmentZone == _roamingZone,
            _ => employmentZone != _roamingZone,
        };
    }

    public void Unload()
    {
        _entries.Clear();
    }

    public string Name { get; set; } = null!;

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
