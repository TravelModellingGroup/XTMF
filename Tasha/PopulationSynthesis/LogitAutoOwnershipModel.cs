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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Schema;
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.PopulationSynthesis;

[ModuleInformation(Description = "This module is designed to produce a discrete number of vehicles when constructing a house.")]
public sealed class LogitAutoOwnershipModel : ICalculation<ITashaHousehold, int>
{

    [ModuleInformation(Description = "Gives the systematic utility for choosing these number of cars for a given household.")]
    public sealed class AutoNode : IModule
    {
        [RunParameter("Number of Vehicles", 0, "The number of vehicles this option represents.")]
        public int NumberOfVehicles;

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

        [RunParameter("B_DriverLicenses", 0.0f, "A factor to apply against the total number of driver licenses.  This is in addition to any coming from the categorizes.")]
        public float B_DriverLicenses;

        [RunParameter("B_FTWorkers", 0.0f, "The factor to apply against the total number of full-time workers in the household.")]
        public float B_FTWorkers;

        [SubModelInformation(Required = false, Description = "The utility to use for the home zone.")]
        public IDataSource<SparseArray<float>> ZoneBasedUtility;

        [SubModelInformation(Required = false, Description = "The driver license values to use.")]
        public Category[] DriverLicenses;

        [SubModelInformation(Required = false, Description = "The income values to use.")]
        public Category[] Incomes;

        private SparseArray<float> _zoneUtility = null!;

        [ModuleInformation(Description = "Provides a general way of giving a constant for a particular number of something coming from the household.")]
        public sealed class Category : IModule
        {
            [RunParameter("Indexes", 0, "The indexes that this category represents.", Index = 0)]
            public RangeSet Index;

            [RunParameter("Value", 0.0f, "The value this option represents.", Index = 1)]
            public float Value;

            public string Name { get; set; }

            public float Progress => 0f;

            public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        internal void Load()
        {
            ZoneBasedUtility.LoadData();
            _zoneUtility = ZoneBasedUtility.GiveData();

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal float GetUtility(ref NodeData nodeData)
        {
            var v = _zoneUtility.GetFlatData()[nodeData.FlatTAZ];

            // TODO: Find the specification for the rest of the parameters
            v +=
                (B_DriverLicenses * nodeData.DriverLicenses)
                + (B_FTWorkers * nodeData.FTWorkers)
                + GetLookup(DriverLicenses, nodeData.DriverLicenses)
                + GetLookup(Incomes, nodeData.IncomeClass)
                ;

            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private static float GetLookup(Category[] categories, int index)
        {
            foreach (var cat in categories)
            {
                if (cat.Index.Contains(index))
                {
                    return cat.Value;
                }
            }
            // If nothing is specified zero.
            return 0f;
        }

        internal void Unload()
        {
            _zoneUtility = null!;
            ZoneBasedUtility.UnloadData();
        }

        public bool RuntimeValidation(ref string error)
        {
            if (NumberOfVehicles < 0)
            {
                error = "You can not have a negative number of vehicles within a household.";
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Contains all of the variables used in the AutoNode calculation, grabbed once to improve performance.
    /// </summary>
    /// <param name="FlatTAZ">The TAZ that the household lives in.</param>
    /// <param name="IncomeClass">The income class for the household.</param>
    /// <param name="DriverLicenses">The number of Driver Licences in the household.</param>
    /// <param name="FTWorkers">The number of full-time workers in the household. </param>
    /// <param name="AverageWorkSchoolAIVTT">The average AIVTT to get to work or school.</param>
    /// <param name="AverageWorkSchoolAIVTT">The average TPVTT to get to work or school.</param>
    /// <param name="AverageWorkSchoolAIVTT">The average distance (km) to get to work or school.</param>
    internal record struct NodeData
        (int FlatTAZ, int IncomeClass, int DriverLicenses, float FTWorkers, float AverageWorkSchoolAIVTT, float AverageWorkSchoolTPTT, float AverageWorkSchoolDistance);

    [SubModelInformation(Required = true, Description = "The different options for the number of vehicles to generate.")]
    public AutoNode[] Nodes = null!;

    [RunParameter("Random Seed", 12345, "The random seed to use for selecting the discrete number of vehicles.")]
    public int RandomSeed;

    private Random _random;

    [RootModule]
    public ITravelDemandModel Root = null!;

    private SparseArray<IZone> _zones = null!;

    public void Load()
    {
        _random = new Random(RandomSeed);
        if (!Root.ZoneSystem.Loaded)
        {
            Root.ZoneSystem.LoadData();
        }
        _zones = Root.ZoneSystem.ZoneArray;
    }

    public int ProduceResult(ITashaHousehold data)
    {
        var flatHouseholdZone = _zones.GetFlatIndex(data.HomeZone.ZoneNumber);
        // Make sure that the household zone is valid
        if (flatHouseholdZone < 0)
        {
            ThrowInvalidHouseholdZone(data.HomeZone.ZoneNumber);
        }
        // Gather the data needed to compute the nodes' utilities.        
        var persons = data.Persons;
        NodeData nodeData = new()
        {
            FlatTAZ = flatHouseholdZone,
            DriverLicenses = GetDriverLicenses(persons),
            FTWorkers = GetFTWorkers(persons),
            AverageWorkSchoolAIVTT = persons.Average(p => 0.0f),
            AverageWorkSchoolTPTT = persons.Average(p => 0.0f),
            AverageWorkSchoolDistance = persons.Average(p => 0.0f),
            IncomeClass = data.IncomeClass
        };

        // Note: The number of auto nodes will be very small so this is safe
        Span<float> ev = stackalloc float[Nodes.Length];
        double total = 0.0;
        for (int i = 0; i < Nodes.Length; i++)
        {
            total += (ev[i] = MathF.Exp(Nodes[i].GetUtility(ref nodeData)));
        }
        var pop = _random.NextSingle() * (float)total;
        // We don't need to test the last option
        for (int i = 0; i < Nodes.Length - 1; i++)
        {
            pop -= ev[i];
            if (pop <= 0)
            {
                return Nodes[i].NumberOfVehicles;
            }
        }
        // If we run into rounding issues, round it to be in the final bin.
        return Nodes[^-1].NumberOfVehicles;
    }

    private static int GetDriverLicenses(ITashaPerson[] persons)
    {
        int count = 0;
        foreach (var person in persons)
        {
            count += person.Licence ? 1 : 0;
        }
        return count;
    }

    private static int GetFTWorkers(ITashaPerson[] persons)
    {
        int count = 0;
        foreach (var person in persons)
        {
            var empStatus = person.EmploymentStatus;
            count += ((empStatus == TTSEmploymentStatus.FullTime) | (empStatus == TTSEmploymentStatus.WorkAtHome_FullTime)) ? 1 : 0;
        }
        return count;
    }

    [DoesNotReturn]
    private void ThrowInvalidHouseholdZone(int zoneNumber)
    {
        throw new XTMFRuntimeException(this, $"A household with an invalid household TAZ was found when running auto ownership. TAZ = {zoneNumber}!");
    }

    public void Unload()
    {
        _random = null!;
        _zones = null!;
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        if (Nodes.Length <= 0)
        {
            error = "The auto ownership module requires at least one option for the number of vehicles!";
            return false;
        }
        return true;
    }
}
