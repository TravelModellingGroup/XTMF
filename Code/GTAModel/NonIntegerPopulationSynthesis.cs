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

namespace TMG.GTAModel;

public class NonIntegerPopulationSynthesis : ITravelDemandModel
{
    [SubModelInformation(Description = "The model used for getting the demographics information", Required = true)]
    public IDemographicsData Demographics;

    [SubModelInformation(Description = "The model used for saving the population", Required = true)]
    public IPopulation Population;

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

    private void BuildPeople(int zoneIndex, int pop, int numberOfCarCategories, Household[] households, int numberOfAgeCategories,
        int driversLicenceCatrogies, int numberOfEmploymentCategories, int numberOfOccupations, float[][] employmentData, int numberOfStudentCategories, Person[] people)
    {
        int personNumber = 0;
        var ageNumberData = Demographics.AgeCategories.GetFlatData();
        var ageData = Demographics.AgeRates.GetFlatData()[zoneIndex];
        var occData = Demographics.OccupationRates.GetFlatData()[zoneIndex].GetFlatData();
        var dlicData = Demographics.DriversLicenseRates.GetFlatData()[zoneIndex].GetFlatData();
        var workerVehicleRates = Demographics.WorkerVehicleRates.GetFlatData()[zoneIndex].GetFlatData();
        var nonworkerVehicleRates = Demographics.NonWorkerVehicleRates.GetFlatData()[zoneIndex].GetFlatData();
        var schoolData = Demographics.SchoolRates.GetFlatData()[zoneIndex].GetFlatData();
        // do age == 0 here
        for (int age = 0; age < numberOfAgeCategories; age++)
        {
            var ageProbability = ageData[age];
            for (int emp = 0; emp < numberOfEmploymentCategories; emp++)
            {
                var employmentProbability = employmentData[age][emp];
                var dlicProbability = dlicData[age][emp];
                for (int occ = 0; occ < numberOfOccupations; occ++)
                {
                    var occupationProbability = (emp == 0 ? (occ == UnemployedOccupation ? 1f : 0f) : occData[age][emp - 1][occ]);
                    for (int dlic = 0; dlic < driversLicenceCatrogies; dlic++)
                    {
                        for (int cars = 0; cars < numberOfCarCategories; cars++)
                        {
                            var carsProbability =
                                 (emp == 0 ?
                                    nonworkerVehicleRates[dlic][(age == 0 ? 1 : age)][cars]
                                 : workerVehicleRates[dlic][occ][cars]
                                 );
                            for (int student = 0; student < numberOfStudentCategories; student++)
                            {
                                var probabilityStudent = schoolData[age][emp];
                                people[personNumber].Age = ageNumberData[age].Stop;
                                people[personNumber].DriversLicense = dlic > 0;
                                people[personNumber].EmploymentStatus = emp;
                                people[personNumber].Occupation = occ;
                                people[personNumber].Household = households[cars];
                                people[personNumber].StudentStatus = student;

                                people[personNumber].ExpansionFactor =
                                        pop * ageProbability * employmentProbability *
                                        occupationProbability * (dlic == 0 ? (1 - dlicProbability) : dlicProbability) *
                                        carsProbability * (student == 0 ? 1 - probabilityStudent : probabilityStudent);

                                personNumber++;
                            }
                        }
                    }
                }
            }
        }
    }

    private void GenerateZone(SparseArray<IPerson[]> population, int zoneIndex, int pop)
    {
        var numberOfCarCategories = 3;
        Household[] households = new Household[numberOfCarCategories];
        for (int i = 0; i < numberOfCarCategories; i++)
        {
            households[i] = new Household { Zone = ZoneSystem.ZoneArray[zoneIndex], Cars = i };
        }
        var numberOfAgeCategories = ValidAges.Length;
        var driversLicenceCatrogies = 2;
        var numberOfEmploymentCategories = Demographics.EmploymentStatus.GetFlatData().Length;
        var numberOfOccupations = Demographics.OccupationCategories.GetFlatData().Length;
        var employmentData = Demographics.EmploymentStatusRates.GetFlatData()[zoneIndex].GetFlatData();
        var numberOfStudentCategories = 2;
        var numberOfCategories = numberOfAgeCategories * numberOfOccupations * numberOfEmploymentCategories
            * driversLicenceCatrogies * numberOfCarCategories * numberOfStudentCategories;
        Person[] people = new Person[numberOfCategories];
        for (int k = 0; k < numberOfCategories; k++)
        {
            people[k] = new Person();
        }
        BuildPeople(zoneIndex, pop, numberOfCarCategories, households, numberOfAgeCategories,
            driversLicenceCatrogies, numberOfEmploymentCategories, numberOfOccupations,
            employmentData, numberOfStudentCategories, people);
        List<Person> nonZeroPeople = new(numberOfCategories);
        for (int i = 0; i < people.Length; i++)
        {
            if (people[i].ExpansionFactor > 0)
            {
                nonZeroPeople.Add(people[i]);
            }
        }
        population.GetFlatData()[zoneIndex] = [.. nonZeroPeople];
    }

    private void Generation(SparseArray<IZone> zoneArray, int numberOfZones, SparseArray<IPerson[]> population, int i)
    {
        for (int j = 0; j < 100 && j + i * 100 < numberOfZones; j++)
        {
            var zoneIndex = i * 100 + j;
            var zone = zoneArray.GetFlatData()[zoneIndex];
            var pop = zone.Population;
            if (pop == 0) continue;
            GenerateZone(population, zoneIndex, pop);
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
        //for ( int i = 0; i < (int)Math.Ceiling( (float)numberOfZones / 100 ); i++ )
        {
            Generation(zoneArray, numberOfZones, population, i);
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
}