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
using System.Collections.Generic;
using XTMF;
using TMG;
using Tasha.Common;
using Datastructure;
using TMG.Input;
using TMG.Functions;
using System.Linq;
using System.Threading.Tasks;

namespace Tasha.PopulationSynthesis;

[ModuleInformation(Description =
    @"This module is designed to take in the PoRPoS rates from a resource and distribute them so that each person
 in the modal that wants a school zone receives one.")]
public sealed class AssignSchoolZonesFromResourceProbabilities : ICalculation<ITashaPerson, IZone>
{
    [SubModelInformation(Required = true, Description = "The resource to gather the elementary school probabilities from.")]
    public IResource ElementarySchoolProbabilitiesResource;

    [SubModelInformation(Required = true, Description = "The resource to gather the high school probabilities from.")]
    public IResource HighschoolProbabilitiesResource;

    [SubModelInformation(Required = true, Description = "The resource to gather the university probabilities from.")]
    public IResource UniversityProbabilitiesResource;

    [RunParameter("Elementary School Ages", "0-11", typeof(RangeSet), "The valid ages to use for Elementary school.")]
    public RangeSet ElementryRange;

    [RunParameter("High School Ages", "0-11", typeof(RangeSet), "The valid ages to use for High school.")]
    public RangeSet HighschoolRange;

    [RunParameter("External Zones", "", typeof(RangeSet), "Exclude persons who already have a place of school in this range.")]
    public RangeSet ExternalZones;


    private SparseTwinIndex<float> ElementarySchoolProbabilities;
    private SparseTwinIndex<float> HighSchoolProbabilities;
    private SparseTwinIndex<float> UniversityProbabilities;

    private SparseTwinIndex<float> CurrentElementarySchoolProbabilities;
    private SparseTwinIndex<float> CurrentHighSchoolProbabilities;
    private SparseTwinIndex<float> CurrentUniversityProbabilities;

    [RootModule]
    public ITravelDemandModel Root;

    [RunParameter("Random Seed", 314268, "A constant factor to apply to the creation of our random set.")]
    public int RandomSeed;

    private SparseArray<IZone> Zones;
    [SubModelInformation(Required = false, Description = "The location to save the remainders for the final iteration.")]
    public FileLocation SaveElementrySchoolProbabilities;

    [SubModelInformation(Required = false, Description = "The location to save the remainders for the final iteration.")]
    public FileLocation SaveHighSchoolProbabilities;

    [SubModelInformation(Required = false, Description = "The location to save the remainders for the final iteration.")]
    public FileLocation SaveUniversitySchoolProbabilities;

    private Random _random;

    public void Load()
    {
        _random = new Random(RandomSeed);
        // Get our resources
        Parallel.Invoke(
        () =>
        {
            ElementarySchoolProbabilities = ElementarySchoolProbabilitiesResource.AcquireResource<SparseTwinIndex<float>>();
            _elementryProbabilities = GetRowTotals(ElementarySchoolProbabilities);
        }, () =>
        {
            HighSchoolProbabilities = HighschoolProbabilitiesResource.AcquireResource<SparseTwinIndex<float>>();
            _highschoolProbabilities = GetRowTotals(HighSchoolProbabilities);
        }, () =>
        {
            UniversityProbabilities = UniversityProbabilitiesResource.AcquireResource<SparseTwinIndex<float>>();
            _universityProperties = GetRowTotals(UniversityProbabilities);
        });
        // create replicated versions for our per iteration needs
        if (!WithReplacement)
        {
            CurrentElementarySchoolProbabilities = Replicate(ElementarySchoolProbabilities);
            CurrentHighSchoolProbabilities = Replicate(HighSchoolProbabilities);
            CurrentUniversityProbabilities = Replicate(UniversityProbabilities);
        }
        // Gather the zone system for use from the root module.
        Zones = Root.ZoneSystem.ZoneArray;
    }

    private static float[] GetRowTotals(SparseTwinIndex<float> matrix)
    {
        var flat = matrix.GetFlatData();
        return flat.Select(a => VectorHelper.Sum(a, 0, a.Length)).ToArray();
    }

    /// <summary>
    /// Replicate a SparseTwinIndex
    /// </summary>
    /// <typeparam name="T">The type of data</typeparam>
    /// <param name="baseData">The data source to replicate</param>
    /// <returns>An exact copy of the sparse twin index.</returns>
    private static SparseTwinIndex<T> Replicate<T>(SparseTwinIndex<T> baseData)
    {
        var ret = baseData.CreateSimilarArray<T>();
        var flatRet = ret.GetFlatData();
        var flatBase = baseData.GetFlatData();
        for (int i = 0; i < flatBase.Length; i++)
        {
            Array.Copy(flatBase[i], flatRet[i], flatRet.Length);
        }
        return ret;
    }

    [RunParameter("With Replacement", false, "Should the distributions be sampled with, or without replacement.")]
    public bool WithReplacement;
    private float[] _elementryProbabilities;
    private float[] _highschoolProbabilities;
    private float[] _universityProperties;

    [RunParameter("New Random Per Person", true, "Should we regenerate the random seed per person or per iteration?")]
    public bool NewRandomSeedPerPerson;

    public IZone ProduceResult(ITashaPerson person)
    {
        if (AlreadyHasExternalZone(person))
        {
            return person.SchoolZone;
        }
        // Gather the base data and create our random generator
        var household = person.Household;
        if (NewRandomSeedPerPerson)
        {
            _random = new Random(RandomSeed * household.HouseholdId);
        }
        var probabilities = GetDataForAge(person.Age);
        var householdZone = household.HomeZone.ZoneNumber;
        if (Root.ZoneSystem.ZoneArray.GetFlatIndex(householdZone) < 0)
        {
            throw new XTMFRuntimeException(this, "In '" + Name + "' we found a person with an invalid household zone! ['" + householdZone + "']");
        }
        if (!WithReplacement)
        {
            IZone ret = PickSchoolZone(person, householdZone, probabilities, _random, person.ExpansionFactor);
            // If a zone is successfully found, return it
            if (ret != null) return ret;
        }
        // If we couldn't find a zone we need to use our backup plan and just generate out of the original distribution
        return GenerateFromOriginalDistribution(person, _random, householdZone);
    }

    private bool AlreadyHasExternalZone(ITashaPerson person)
    {
        var zone = person.SchoolZone;
        return zone != null && ExternalZones.Contains(zone.ZoneNumber);
    }

    /// <summary>
    /// This algorithm is used if we failed to generate a school zone from the modified distribution.
    /// </summary>
    /// <param name="person">The person to search for.</param>
    /// <param name="random">The random number source.</param>
    /// <param name="householdZone"></param>
    /// <param name="deapSearch">Should we continue to search other zone in the planning district?</param>
    /// <returns>The school zone for the person.</returns>
    private IZone GenerateFromOriginalDistribution(ITashaPerson person, Random random, int householdZone, bool deapSearch = true)
    {
        (var probabilities, var totals, var source) = GetOriginalDataForAge(person.Age);
        (var data, var total) = GetHouseholdRow(householdZone, (probabilities, totals));
        if (data == null)
        {
            return null;
        }
        if (total <= 0)
        {
            // If there still isn't anything for this zone
            return deapSearch ? GetProbabilitiesFromAnotherZoneInPD(person, random, householdZone, source) : null;
        }
        var count = random.NextDouble() * total;
        for (int i = 0; i < data.Length; i++)
        {
            count -= data[i];
            if (count <= 0)
            {
                return Zones.GetFlatData()[i];
            }
        }
        // In case of rounding error issues just find the first non zero data point
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] > 0)
            {
                return Zones.GetFlatData()[i];
            }
        }
        throw new XTMFRuntimeException(this, "In '" + Name + "' we ended up in a case where there was a probability to generate a school zone however there were no zones with any probability!" +
            $" HouseholdZone = {person.Household.HomeZone.ZoneNumber}, Age={person.Age}");
    }

    /// <summary>
    /// Generate the probabilities from another randomly chosen zone's base probability distribution
    /// from the same planning district.
    /// </summary>
    /// <param name="person">The person to generate the </param>
    /// <param name="random">The random number generator</param>
    /// <param name="householdZone">The household zone number for the person</param>
    /// <returns>The zone to use, it will throw an exception if no zone is possible.</returns>
    private IZone GetProbabilitiesFromAnotherZoneInPD(ITashaPerson person, Random random, int householdZone, IModule source)
    {
        var pd = Zones[householdZone].PlanningDistrict;
        var zones = Zones.GetFlatData();
        List<int> possibleZone = new(15);
        for (int i = 0; i < zones.Length; i++)
        {
            // if the zone belongs to the same planning district
            if (zones[i].PlanningDistrict == pd & zones[i].ZoneNumber != householdZone)
            {
                possibleZone.Add(i);
            }
        }
        // while there are still zones to search through randomly try to find them
        while (possibleZone.Count > 0)
        {
            var index = (int)(random.NextDouble() * possibleZone.Count);
            var ret = GenerateFromOriginalDistribution(person, random, zones[possibleZone[index]].ZoneNumber, false);
            if (ret != null)
            {
                return ret;
            }
            possibleZone.RemoveAt(index);
        }
        // if we have exhausted all options fail with an exception.
        throw new XTMFRuntimeException(source, "In '" + Name + "' we were unable to generate a school zone because there are no zones in the planning district " + pd + " that have school data!");
    }

    /// <summary>
    /// Given the probabilities, select and updates the probability table
    /// to reflect the selected changes
    /// </summary>
    /// <param name="householdZone">The sparse space zone number of the household</param>
    /// <param name="probabilities">The probabilities to select from</param>
    /// <param name="random">The random number generator to use.</param>
    /// <param name="personExpansionFactor"></param>
    /// <exception cref="XTMFRuntimeException">If the household zone does not have data defined for it.</exception>
    /// <returns>A zone to use for school, null if no options exist</returns>
    private IZone PickSchoolZone(ITashaPerson person, int householdZone, SparseTwinIndex<float> probabilities, Random random, float personExpansionFactor)
    {
        (float[] data, float total) = GetHouseholdRow(householdZone, (probabilities, null));
        if (data == null)
        {
            throw new XTMFRuntimeException(this, "In '" + Name + "' we were unable to find school choice data for household zone number " + householdZone + " !");
        }
        var count = (float)random.NextDouble() * total;
        int countIndex = -1;
        // first pass randomly pick a point 
        for (int i = 0; i < data.Length; i++)
        {
            count -= data[i];
            if (count <= 0)
            {
                if (personExpansionFactor < data[i])
                {
                    data[i] -= personExpansionFactor;
                    return Zones.GetFlatData()[i];
                }
                if (countIndex == -1)
                {
                    countIndex = i;
                }
            }
        }
        // check places before that point if nothing after it has enough utility
        for (int i = 0; i < countIndex; i++)
        {
            if (personExpansionFactor <= data[i])
            {
                data[i] -= personExpansionFactor;
                return Zones.GetFlatData()[i];
            }
        }
        // In case of rounding error issues just find the first non zero data point
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] > 0)
            {
                data[i] = 0;
                return Zones.GetFlatData()[i];
            }
        }
        // If there was not enough space, grab from the original distribution
        return null;
    }

    /// <summary>
    /// Gets the row of data from the probabilities for the given household
    /// </summary>
    /// <param name="householdZone">The sparse space to retrieve</param>
    /// <param name="probabilities">The probability table</param>
    /// <returns>The row of data, null if it doesn't exist</returns>
    private (float[] probabilities, float total) GetHouseholdRow(int householdZone, (SparseTwinIndex<float> probabilities, float[] rowTotals) data)
    {
        var index = data.probabilities.GetFlatIndex(householdZone);
        if (index < 0)
        {
            throw new XTMFRuntimeException(this, "In '" + Name + "' we were unable to find any data for a zone numbered '" + householdZone
                + "'.  Please make sure that this zone actually exists.");
        }
        var row = data.probabilities.GetFlatData()[index];
        return (row,
            data.rowTotals is not null ? data.rowTotals[index] : VectorHelper.Sum(row, 0, row.Length));
    }

    /// <summary>
    /// Get the current probabilities for a given person's age
    /// </summary>
    /// <param name="age">The age of the person to lookup for.</param>
    /// <returns>The probability distribution for the age.</returns>
    private SparseTwinIndex<float> GetDataForAge(int age)
    {
        if (ElementryRange.Contains(age))
        {
            return CurrentElementarySchoolProbabilities;
        }
        if (HighschoolRange.Contains(age))
        {
            return CurrentHighSchoolProbabilities;
        }
        return CurrentUniversityProbabilities;
    }

    /// <summary>
    /// Get the original (non modified) probabilities for a given person's age
    /// </summary>
    /// <param name="age">The age of the person to lookup for.</param>
    /// <returns>The probability distribution for the age.</returns>
    private (SparseTwinIndex<float> Probabilities, float[] Totals, IModule probabilitySource) GetOriginalDataForAge(int age)
    {
        if (ElementryRange.Contains(age))
        {
            return (ElementarySchoolProbabilities, _elementryProbabilities, ElementarySchoolProbabilitiesResource);
        }
        if (HighschoolRange.Contains(age))
        {
            return (HighSchoolProbabilities, _highschoolProbabilities, HighschoolProbabilitiesResource);
        }
        return (UniversityProbabilities, _universityProperties, UniversityProbabilitiesResource);
    }


    public void Unload()
    {

        SaveIfFileExists(CurrentElementarySchoolProbabilities, SaveElementrySchoolProbabilities);
        SaveIfFileExists(CurrentHighSchoolProbabilities, SaveHighSchoolProbabilities);
        SaveIfFileExists(CurrentUniversityProbabilities, SaveUniversitySchoolProbabilities);

        ElementarySchoolProbabilities = null;
        HighSchoolProbabilities = null;
        UniversityProbabilities = null;

        CurrentElementarySchoolProbabilities = null;
        CurrentHighSchoolProbabilities = null;
        CurrentUniversityProbabilities = null;
    }

    private static void SaveIfFileExists(SparseTwinIndex<float> matrix, FileLocation file)
    {
        if (file != null && matrix != null)
        {
            SaveData.SaveMatrix(matrix, file);
        }
    }

    public string Name { get; set; }

    public float Progress
    {
        get { return 0f; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return null; }
    }

    public bool RuntimeValidation(ref string error)
    {
        if (!ElementarySchoolProbabilitiesResource.CheckResourceType<SparseTwinIndex<float>>())
        {
            error = "In '" + Name + "' ";
            return false;
        }
        return true;
    }
}
