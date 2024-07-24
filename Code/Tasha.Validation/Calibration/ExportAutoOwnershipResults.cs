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
using System.IO;
using System.Linq;
using Tasha.Common;
using TMG;
using TMG.Input;
using XTMF;

namespace Tasha.Validation.Calibration;

[ModuleInformation(Description = "Generates a CSV with the column ZoneNumber followed by Auto-(Ground/Apartment)-X which contains the expanded households with that many vehicles.")]
public sealed class ExportAutoOwnershipResults : IPostHousehold
{
    [RootModule]
    public ITravelDemandModel Root;

    [SubModelInformation(Required = true, Description = "The location to write the CSV file.")]
    public FileLocation SaveTo;

    private SparseArray<IZone> _zones;

    [RunParameter("Max Vehicles", 4, "The maximum number of vehicles to report on, any additional vehicles will be added to the last column.")]
    public int MaxVehicles;

    private float[] _autoCounts;

    private int _targetIteration;

    public void Load(int maxIterations)
    {
        _targetIteration = maxIterations - 1;
    }

    public void IterationStarting(int iteration)
    {
        _zones = Root.ZoneSystem.ZoneArray;
        if (_autoCounts is null)
        {
            _autoCounts = new float[_zones.Count * (MaxVehicles + 1) * 2];
        }
        else
        {
            Array.Clear(_autoCounts, 0, _autoCounts.Length);
        }
    }

    public void Execute(ITashaHousehold household, int iteration)
    {
        // Only write the last iteration
        if (_targetIteration != iteration)
        {
            return;
        }
        var householdZone = _zones.GetFlatIndex(household.HomeZone.ZoneNumber);
        var autos = household.Vehicles.Length;
        var expansionFactor = household.ExpansionFactor;
        var dwellingOffset = household.DwellingType == DwellingType.Apartment ? 1 : 0;
        autos = Math.Min(autos, MaxVehicles);
        var index = (householdZone * (MaxVehicles + 1) + autos) * 2 + dwellingOffset;
        lock (_autoCounts)
        {
            _autoCounts[index] += expansionFactor;
        }
    }

    public void IterationFinished(int iteration)
    {
        // Only write the last iteration
        if (_targetIteration != iteration)
        {
            return;
        }
        using var writer = new StreamWriter(SaveTo);
        writer.WriteLine("ZoneNumber," + string.Join(',', Enumerable.Range(0, (MaxVehicles + 1) * 2).Select(i => $"Auto-{((i & 1) == 0 ? "Ground" : "Apartment")}-{i >> 1}")));
        var flatZones = _zones.GetFlatData();
        for (var i = 0; i < flatZones.Length; i++)
        {
            writer.Write(flatZones[i].ZoneNumber);
            for (var j = 0; j < (MaxVehicles + 1) * 2; j++)
            {
                writer.Write($",{_autoCounts[(i * (MaxVehicles + 1) * 2) + j]}");
            }
            writer.WriteLine();
        }
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        if (MaxVehicles < 1)
        {
            error = "Max Vehicles must be at least 1.";
            return false;
        }
        return true;
    }

}
