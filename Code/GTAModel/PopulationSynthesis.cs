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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Datastructure;
using XTMF;
using Range = Datastructure.Range;

namespace TMG.GTAModel;

[ModuleInformation(Description =
    @"This model system template is designed for quickly generating a synthetic population for GTAModel.  
It requires an IZoneSystem, and IDemographicsData, and an IPopulation module."
    )]
public class PopulationSynthesis : ITravelDemandModel
{
    [SubModelInformation(Description = "The model used for getting the demographics information", Required = true)]
    public IDemographicsData Demographics;

    [SubModelInformation(Description = "The model used for saving the population", Required = true)]
    public IPopulation Population;

    [RunParameter("Random Seed", 12345, "The random seed to use for the choice of work and school.")]
    public int RandomSeed;

    [RunParameter("Unemployed Status", 0, "The index of the unemployed Employment Status")]
    public int UnemployedOccupation;

    private static Tuple<byte, byte, byte> Colour = new(100, 200, 100);

    private int[] ValidAges;

    [RunParameter("Input Directory", "../../Input", "The directory that stores the input for this model system.")]
    public string InputBaseDirectory
    {
        get;
        set;
    }

    public string Name
    {
        get;
        set;
    }

    [DoNotAutomate]
    public IList<INetworkData> NetworkData { get { return null; } }

    public string OutputBaseDirectory
    {
        get;
        set;
    }

    public float Progress
    {
        get { return Population.Progress; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return Colour; }
    }

    [SubModelInformation(Description = "The model used for handelling the zone system", Required = true)]
    public IZoneSystem ZoneSystem { get; set; }

    public bool ExitRequest()
    {
        return false;
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void Start()
    {
        ProducePopulation();
    }

    private static void AssignEmploymentStatus(Person[] people, int ageOffset, int employmentOffset, int validEmploymentStatusIndex, int numberOfEmploymentClassifiedPeople)
    {
        for (int p = 0; p < numberOfEmploymentClassifiedPeople; p++)
        {
            people[p + ageOffset + employmentOffset].EmploymentStatus = validEmploymentStatusIndex;
        }
    }

    private static void AssignSchools(Person[] people, int ageOffset, int employmentOffset, int studentPop)
    {
        for (int p = 0; p < studentPop; p++)
        {
            people[p + ageOffset + employmentOffset].StudentStatus = 1;
        }
    }

    private void AssignAge(Random rand, Person[] people, int ageOffset, int validAgeIndex, int numberOfPeople)
    {
        Range ageRange = Demographics.AgeCategories[validAgeIndex];
        for (int p = 0; p < numberOfPeople; p++)
        {
            people[p + ageOffset].Age = rand.Next(ageRange.Start, int.MaxValue == ageRange.Stop ? int.MaxValue : ageRange.Stop + 1);
            people[p + ageOffset].ExpansionFactor = 1;
        }
    }

    private void AssignCars(Person[] people, int[] indexes, SparseArray<int> split, Household[] households, int ageOffset, Random rand)
    {
        var numberOfPeople = indexes.Length;
        // randomly shuffle the indexes before we actually assign the households
        for (int i = 0; i < numberOfPeople; i++)
        {
            var selectedIndex = rand.Next(i, numberOfPeople);
            var temp = indexes[selectedIndex];
            indexes[selectedIndex] = indexes[i];
            indexes[i] = temp;
        }

        int typeOffset = 0;
        foreach (var carType in split.ValidIndexies())
        {
            var numberInType = split[carType];
            for (int i = 0; i < numberInType; i++)
            {
                people[indexes[i + typeOffset] + ageOffset].Household = households[carType];
            }
            typeOffset += numberInType;
        }
    }

    private void AssignOccupationAndLicenese(Person[] people, int ageOffset, int employmentOffset, SparseArray<int> occupationSplit, int numberOfEmploymentClassifiedPeople,
        int numberOfLicenses, Random r)
    {
        int[] personIndex = new int[numberOfEmploymentClassifiedPeople];
        for (int i = 0; i < numberOfEmploymentClassifiedPeople; i++)
        {
            personIndex[i] = i;
        }
        // Randomize the population for assignment (Card shuffle algorithm)
        for (int i = 0; i < numberOfEmploymentClassifiedPeople; i++)
        {
            var selectedIndex = r.Next(i, numberOfEmploymentClassifiedPeople);
            var temp = personIndex[selectedIndex];
            personIndex[selectedIndex] = personIndex[i];
            personIndex[i] = temp;
        }
        // assign the occupations
        int occOffset = 0;
        foreach (var occIndex in occupationSplit.ValidIndexies())
        {
            var occPop = occupationSplit[occIndex];
            for (int i = 0; i < occPop; i++)
            {
                people[personIndex[occOffset + i] + ageOffset + employmentOffset].Occupation = occIndex;
            }
            occOffset += occPop;
        }
        // Randomize the population for assignment (Card shuffle algorithm)
        for (int i = 0; i < numberOfEmploymentClassifiedPeople; i++)
        {
            var selectedIndex = r.Next(i, numberOfEmploymentClassifiedPeople);
            var temp = personIndex[selectedIndex];
            personIndex[selectedIndex] = personIndex[i];
            personIndex[i] = temp;
        }
        // assign the occupations
        for (int i = 0; i < numberOfLicenses; i++)
        {
            people[personIndex[i] + ageOffset + employmentOffset].DriversLicense = true;
        }
    }

    private void GenerateZone(SparseArray<IPerson[]> population, int zoneIndex, int pop, Random rand)
    {
        Household[] households = new Household[3];
        for (int i = 0; i < 3; i++)
        {
            households[i] = new Household { Zone = ZoneSystem.ZoneArray[zoneIndex], Cars = i };
        }
        Person[] people = new Person[pop];
        // ReSharper disable once CoVariantArrayConversion
        population[zoneIndex] = people;
        for (int k = 0; k < pop; k++)
        {
            people[k] = new Person();
        }
        /*
         * To generate the population follow these steps, and at each one clear to integers
         * Step 1) Split into Age Cat's
         * Step 2) Split into ( Unemployed, Full-time, Part-Time )
         * Step 3) Split into ( Non-Student, Student ) , ( Occupation )
         * Step 4) Split into Driver's License ( Not Have / Have )
         */
        SparseArray<int> agePop = SplitAges(zoneIndex, pop, rand);
        int ageOffset = 0;
        foreach (var validAgeIndex in ValidAges)
        {
            var numberOfPeople = agePop[validAgeIndex];
            var employmentPop = SplitEmployment(zoneIndex, validAgeIndex, numberOfPeople, rand);
            int employmentOffset = 0;
            foreach (var validEmploymentStatusIndex in employmentPop.ValidIndexies())
            {
                var numberOfEmploymentClassifiedPeople = employmentPop[validEmploymentStatusIndex];
                int studentPop = SplitStudents(zoneIndex, validAgeIndex, validEmploymentStatusIndex, numberOfEmploymentClassifiedPeople, rand);
                int numberOfDriversLicenses = SplitDrivers(zoneIndex, validAgeIndex, validEmploymentStatusIndex, numberOfEmploymentClassifiedPeople, rand);
                var occupationSplit = SplitOccupations(zoneIndex, validAgeIndex, validEmploymentStatusIndex,
                    numberOfEmploymentClassifiedPeople, rand);

                AssignEmploymentStatus(people, ageOffset, employmentOffset, validEmploymentStatusIndex, numberOfEmploymentClassifiedPeople);
                AssignSchools(people, ageOffset, employmentOffset, studentPop);
                AssignOccupationAndLicenese(people, ageOffset, employmentOffset, occupationSplit, numberOfEmploymentClassifiedPeople,
                    numberOfDriversLicenses, rand);
                employmentOffset += numberOfEmploymentClassifiedPeople;
            }

            AssignAge(rand, people, ageOffset, validAgeIndex, numberOfPeople);

            // Assign people to households depending on the number of cars they will have
            foreach (var validOccupationIndex in Demographics.OccupationCategories.ValidIndexies())
            {
                var haveLicense = SplitCars(people, zoneIndex, validAgeIndex, validOccupationIndex,
                    true, ageOffset, numberOfPeople, rand, out int[] haveLicenseIndexes);
                var doNotHaveLicense = SplitCars(people, zoneIndex, validAgeIndex, validOccupationIndex,
                    false, ageOffset, numberOfPeople, rand, out int[] doNotHaveLicenseIndexes);
                AssignCars(people, haveLicenseIndexes, haveLicense, households, ageOffset, rand);
                AssignCars(people, doNotHaveLicenseIndexes, doNotHaveLicense, households, ageOffset, rand);
            }
            ageOffset += numberOfPeople;
        }
    }

    private void Generation(SparseArray<IZone> zoneArray, int[] validZones, int numberOfZones, SparseArray<IPerson[]> population, int i)
    {
        Random rand = new(RandomSeed * i);
        for (int j = 0; j < 100 && j + i * 100 < numberOfZones; j++)
        {
            var zoneIndex = validZones[i * 100 + j];
            var zone = zoneArray[zoneIndex];
            var pop = zone.Population;
            if (pop == 0) continue;
            GenerateZone(population, zoneIndex, pop, rand);
        }
    }

    private void ProducePopulation()
    {
        using StreamWriter performance = new("Performance.txt");
        Stopwatch watch = new();
        watch.Start();
        ZoneSystem.LoadData();
        watch.Stop();
        performance.WriteLine("Loading Zones :" + watch.ElapsedMilliseconds + "ms");
        watch.Restart();
        Demographics.LoadData();
        ValidAges = Demographics.AgeCategories.ValidIndexies().ToArray();
        watch.Stop();
        performance.WriteLine("Loading Demographics :" + watch.ElapsedMilliseconds + "ms");
        var zoneArray = ZoneSystem.ZoneArray;
        var validZones = zoneArray.ValidIndexArray();
        var numberOfZones = validZones.Length;
        SparseArray<IPerson[]> population = zoneArray.CreateSimilarArray<IPerson[]>();
        watch.Restart();
        Parallel.For(0, (int)Math.Ceiling((float)numberOfZones / 100), delegate (int i)
        //for (int i = 0; i < (int)Math.Ceiling((float)numberOfZones / 100); i++)
        {
            Generation(zoneArray, validZones, numberOfZones, population, i);
        });
        watch.Stop();
        performance.WriteLine("Generation Time: " + watch.ElapsedMilliseconds + "ms");
        watch.Restart();
        Population.Population = population;
        Population.Save();
        watch.Stop();
        performance.WriteLine("Output Time: " + watch.ElapsedMilliseconds + "ms");
        Demographics.UnloadData();
        ZoneSystem.UnloadData();
    }

    private SparseArray<int> SplitAges(int zoneIndex, int pop, Random rand)
    {
        var ageSplit = Demographics.AgeCategories.CreateSimilarArray<float>();
        for (int i = 0; i < ValidAges.Length; i++)
        {
            ageSplit[ValidAges[i]] = Demographics.AgeRates[zoneIndex, ValidAges[i]];
        }
        return SplitAndClear(pop, ageSplit, rand);
    }

    private SparseArray<int> SplitAndClear(int pop, SparseArray<float> splitPercentages, Random rand)
    {
        var flatSplitPercentages = splitPercentages.GetFlatData();
        var length = flatSplitPercentages.Length;
        var ret = splitPercentages.CreateSimilarArray<int>();
        var flatRet = ret.GetFlatData();
        var flatRemainder = new float[length];
        float remainderTotal;
        int total = 0;
        for (int i = 0; i < length; i++)
        {
            float element = (flatSplitPercentages[i] * pop);
            total += (flatRet[i] = (int)Math.Floor(element));
            flatRemainder[i] = element - flatRet[i];
        }
        int notAssigned = pop - total;
        // Make sure that we do not over assign
        remainderTotal = notAssigned;
        for (int i = 0; i < notAssigned; i++)
        {
            var randPop = rand.NextDouble() * remainderTotal;
            float ammountToReduce;
            int j = 0;
            for (; j < length; j++)
            {
                randPop -= (ammountToReduce = flatRemainder[j]);
                if (randPop <= 0)
                {
                    remainderTotal -= ammountToReduce;
                    flatRemainder[j] = 0;
                    flatRet[j] += 1;
                    break;
                }
            }
            if (j == length)
            {
                for (j = 0; j < length; j++)
                {
                    if (flatRemainder[j] >= 0)
                    {
                        remainderTotal -= flatRemainder[j];
                        flatRemainder[j] = 0;
                        flatRet[j] += 1;
                        break;
                    }
                }
            }
        }
        return ret;
    }

    private SparseArray<int> SplitCars(Person[] people, int zoneIndex, int validAgeIndex, int validOccupationIndex, bool license, int ageOffset, int agePop, Random rand, out int[] indexes)
    {
        SparseArray<float> ret = new(new SparseIndexing { Indexes = [new SparseSet { Start = 0, Stop = 2 }] });
        // Because everything is random at this point we actually need to scan to see how many people we have
        List<int> indexesList = new(agePop);
        if (validOccupationIndex == UnemployedOccupation)
        {
            for (int i = 0; i < agePop; i++)
            {
                var person = people[i + ageOffset];
                if (person.DriversLicense == license)
                {
                    var range = Demographics.AgeCategories[validAgeIndex];
                    var age = person.Age;
                    if (age >= range.Start && age <= range.Stop)
                    {
                        indexesList.Add(i);
                    }
                }
            }
            var data = Demographics.NonWorkerVehicleRates[zoneIndex];
            foreach (var validCarsIndex in ret.ValidIndexies())
            {
                ret[validCarsIndex] = data[license ? 1 : 0, validAgeIndex, validCarsIndex];
            }
        }
        else
        {
            for (int i = 0; i < agePop; i++)
            {
                var person = people[i + ageOffset];
                if (person.DriversLicense == license
                    && person.Occupation == validOccupationIndex)
                {
                    indexesList.Add(i);
                }
            }
            var data = Demographics.WorkerVehicleRates[zoneIndex];
            foreach (var validCarsIndex in ret.ValidIndexies())
            {
                ret[validCarsIndex] = data[license ? 1 : 0, validOccupationIndex, validCarsIndex];
            }
        }
        indexes = indexesList.ToArray();

        return SplitAndClear(indexes.Length, ret, rand);
    }

    private int SplitDrivers(int zoneIndex, int ageCat, int employmentCat, int numberOfEmploymentClassifiedPeople, Random rand)
    {
        int numberOfDrivers;
        float probability = Demographics.DriversLicenseRates[zoneIndex][ageCat, employmentCat];
        numberOfDrivers = (int)(numberOfEmploymentClassifiedPeople * probability) +
            (rand.NextDouble() < ((probability * numberOfEmploymentClassifiedPeople) - (int)(probability * numberOfEmploymentClassifiedPeople)) ? 1 : 0);
        return numberOfDrivers;
    }

    private SparseArray<int> SplitEmployment(int zoneIndex, int ageCat, int popAge, Random rand)
    {
        var employmentSplit = Demographics.EmploymentStatus.CreateSimilarArray<float>();
        var zoneData = Demographics.EmploymentStatusRates[zoneIndex];
        foreach (var valid in employmentSplit.ValidIndexies())
        {
            employmentSplit[valid] = zoneData[ageCat, valid];
        }
        return SplitAndClear(popAge, employmentSplit, rand);
    }

    private SparseArray<int> SplitOccupations(int zoneIndex, int ageIndex, int employmentStatusIndex,
        int numberOfEmploymentClassifiedPeople, Random rand)
    {
        var occupationSplit = Demographics.OccupationCategories.CreateSimilarArray<float>();
        var zoneData = Demographics.OccupationRates[zoneIndex];
        foreach (var valid in occupationSplit.ValidIndexies())
        {
            occupationSplit[valid] = zoneData[ageIndex, employmentStatusIndex, valid];
        }
        return SplitAndClear(numberOfEmploymentClassifiedPeople, occupationSplit, rand);
    }

    private int SplitStudents(int zoneIndex, int ageCat, int employmentCat,
        int numberOfEmploymentClassifiedPeople, Random rand)
    {
        float probability = Demographics.SchoolRates[zoneIndex][ageCat, employmentCat];
        var numberOfStudents = (int)(numberOfEmploymentClassifiedPeople * probability) +
                               (rand.NextDouble() < ((probability * numberOfEmploymentClassifiedPeople) - (int)(probability * numberOfEmploymentClassifiedPeople)) ? 1 : 0);
        return numberOfStudents;
    }
}