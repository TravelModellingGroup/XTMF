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

        [RunParameter("Constant", 0.0f, "The constant to use for this alternative.")]
        public float Constant;

        [RunParameter("B_DriverLicenses", 0.0f, "A factor to apply against the total number of driver licenses.  This is in addition to any coming from the categorizes.")]
        public float B_DriverLicenses;

        [RunParameter("B_FTWorkers", 0.0f, "The factor to apply against the total number of full-time workers in the household.")]
        public float B_FTWorkers;

        [RunParameter("B_AIVTT", 0.0f, "The factor to apply to the average auto travel time to work or school.")]
        public float B_AIVTT;

        [RunParameter("B_TPTT", 0.0f, "The factor to apply to the average transit perceived travel time to work or school.")]
        public float B_TPTT;

        [RunParameter("B_Distance", 0.0f, "The factor to apply to the average distance (KM) to work or school.")]
        public float B_Distance;

        [RunParameter("B_PopulationDensity", 0.0f, "The factor to apply to the household's population density, ln(popdensity + 1).")]
        public float B_PopulationDensity;

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

        internal void Load(int zoneSystemSize)
        {
            if (ZoneBasedUtility is null)
            {
                return;
            }
            var loaded = ZoneBasedUtility.Loaded;
            if (!loaded)
            {
                ZoneBasedUtility.LoadData();
            }
            _zoneUtility = ZoneBasedUtility.GiveData();
            if (_zoneUtility is not null && _zoneUtility.GetFlatData().Length != zoneSystemSize)
            {
                throw new XTMFRuntimeException(this, $"The zonal utility size is wrong, found {_zoneUtility.GetFlatData().Length} but expected {zoneSystemSize}!");
            }
            if (!loaded)
            {
                ZoneBasedUtility.UnloadData();
            }
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal float GetUtility(ref NodeData nodeData)
        {
            var v = (_zoneUtility?.GetFlatData()[nodeData.FlatTAZ]) ?? 0f;

            // TODO: Find the specification for the rest of the parameters
            v += Constant
                + (B_DriverLicenses * nodeData.DriverLicenses)
                + (B_FTWorkers * nodeData.FTWorkers)
                + GetLookup(DriverLicenses, nodeData.DriverLicenses)
                + GetLookup(Incomes, nodeData.IncomeClass)
                + B_PopulationDensity * nodeData.PopulationDensity
                ;

            return v;
        }

        //[MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
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
    /// <param name="AverageWorkSchoolTPTT">The average TPVTT to get to work or school.</param>
    /// <param name="AverageWorkSchoolDistance">The average distance (km) to get to work or school.</param>
    internal record struct NodeData
        (int FlatTAZ, int IncomeClass, int DriverLicenses, float FTWorkers, float AverageWorkSchoolAIVTT, float AverageWorkSchoolTPTT, float AverageWorkSchoolDistance, float PopulationDensity);

    [SubModelInformation(Required = true, Description = "The different options for the number of vehicles to generate.")]
    public AutoNode[] Nodes = null!;

    [RunParameter("Random Seed", 12345, "The random seed to use for selecting the discrete number of vehicles.")]
    public int RandomSeed;

    private Random _random;

    [RootModule]
    public ITravelDemandModel Root = null!;

    private SparseArray<IZone> _zones = null!;

    [RunParameter("Auto Network", "Auto", "The auto network to use for travel times.")]
    public string AutoNetwork;

    [RunParameter("Transit Network", "Transit", "The transit network to use for travel times.")]
    public string TransitNetwork;

    [RunParameter("Time To Use", "7:00", typeof(Time), "The time of day to use for computing travel times.")]
    public Time TimeToUse;

    [DoNotAutomate]
    private INetworkCompleteData _autoNetwork;

    [DoNotAutomate]
    private ITripComponentCompleteData _transitNetwork;

    private float[] _autoData;

    private float[] _transitData;

    private float[][] _distances;

    private float[] _populationDensity;

    public void Load()
    {
        _random = new Random(RandomSeed);
        if (!Root.ZoneSystem.Loaded)
        {
            Root.ZoneSystem.LoadData();
        }
        _zones = Root.ZoneSystem.ZoneArray;
        _distances = Root.ZoneSystem.Distances.GetFlatData();
        _autoData = _autoNetwork.GetTimePeriodData(TimeToUse);
        _transitData = _transitNetwork.GetTimePeriodData(TimeToUse);
        _populationDensity = LoadPopulationDensity();
        if (_autoData is null)
        {
            throw new XTMFRuntimeException(this, $"The auto network does not have data for {TimeToUse}!");
        }
        if (_transitData is null)
        {
            throw new XTMFRuntimeException(this, $"The transit network does not have data for {TimeToUse}!");
        }
        for (int i = 0; i < Nodes.Length; i++)
        {
            Nodes[i].Load(_populationDensity.Length);
        }
    }

    private float[] LoadPopulationDensity()
    {
        var flatZones = _zones.GetFlatData();
        var ret = new float[flatZones.Length];
        for (int i = 0; i < ret.Length; i++)
        {
            ret[i] = MathF.Log(flatZones[i].Population + 1);
        }
        return ret;
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
        (float aivtt, float tptt, float distance) = GetAverageWorkSchool(flatHouseholdZone, persons);
        NodeData nodeData = new()
        {
            FlatTAZ = flatHouseholdZone,
            DriverLicenses = GetDriverLicenses(persons),
            FTWorkers = GetFTWorkers(persons),
            AverageWorkSchoolAIVTT = aivtt,
            AverageWorkSchoolTPTT = tptt,
            AverageWorkSchoolDistance = distance,
            IncomeClass = data.IncomeClass,
            PopulationDensity = _populationDensity[flatHouseholdZone],
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
        return Nodes[^1].NumberOfVehicles;
    }

    private (float aivtt, float tptt, float distance) GetAverageWorkSchool(int flatHomeZone, ITashaPerson[] persons)
    {
        var ret = (aivtt: 0.0f, tptt: 0.0f, distance: 0.0f);
        var normalize = 1.0f / (float)persons.Length;

        foreach (var person in persons)
        {
            var personValues = GetPersonWorkSchool(flatHomeZone, person);
            ret = (ret.aivtt + personValues.aivtt * normalize,
                ret.tptt + personValues.tptt * normalize,
                ret.distance + personValues.distance * normalize);
        }
        return ret;
    }

    private (float aivtt, float tptt, float distance) GetPersonWorkSchool(int flatHomeZone, ITashaPerson data)
    {
        return (data.EmploymentStatus, data.StudentStatus) switch
        {
            (TTSEmploymentStatus.FullTime or TTSEmploymentStatus.WorkAtHome_FullTime, _) =>
                GetToIfExists(flatHomeZone, data.EmploymentZone),
            (_, StudentStatus.FullTime) =>
                GetToIfExists(flatHomeZone, data.SchoolZone),
            (TTSEmploymentStatus.PartTime or TTSEmploymentStatus.WorkAtHome_PartTime, _) =>
                GetToIfExists(flatHomeZone, data.EmploymentZone),
            (_, StudentStatus.PartTime) =>
                GetToIfExists(flatHomeZone, data.SchoolZone),
            _ => (0.0f, 0.0f, 0.0f),
        };
    }

    private (float aivtt, float tptt, float distance) GetToIfExists(int flatHomeZone, IZone zone)
    {
        int flatDestZone;
        if (zone is null
            || (flatDestZone = _zones.GetFlatIndex(zone.ZoneNumber)) < 0)
        {
            return (0.0f, 0.0f, 0.0f);
        }
        var odOffset = ((_zones.Count * flatHomeZone) + flatDestZone);
        var autoIndex = odOffset * 2;
        var transitIndex = odOffset * 5;
        return (_autoData[autoIndex], _transitData[transitIndex + 4], _distances[flatHomeZone][flatDestZone]);
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
        for (int i = 0; i < Nodes.Length; i++)
        {
            Nodes[i].Unload();
        }
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
        _transitNetwork = Root.NetworkData.FirstOrDefault(net => net.NetworkType == TransitNetwork) as ITripComponentCompleteData;
        if (TransitNetwork == null)
        {
            error = (Root.NetworkData.FirstOrDefault(net => net.NetworkType == TransitNetwork) != null) ?
                $"The network specified {TransitNetwork} is not a valid transit network!" :
                $"There was no transit network with the name {TransitNetwork} found!";
            return false;
        }

        _autoNetwork = Root.NetworkData.FirstOrDefault(net => net.NetworkType == AutoNetwork) as INetworkCompleteData;
        if (TransitNetwork == null)
        {
            error = (Root.NetworkData.FirstOrDefault(net => net.NetworkType == AutoNetwork) != null) ?
                $"The network specified {AutoNetwork} is not a valid auto network!" :
                $"There was no auto network with the name {AutoNetwork} found!";
            return false;
        }
        return true;
    }
}
