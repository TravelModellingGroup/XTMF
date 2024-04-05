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
using System.IO;
using System.Linq;
using Datastructure;
using Tasha.Common;
using TMG;
using TMG.Functions;
using TMG.Input;
using XTMF;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace Tasha.PopulationSynthesis
{
    [ModuleInformation(Description =
    @"This module is designed to take in the PoRPoW rates from a resource and distribute them so that each person
 in the modal that wants a school zone receives one.")]
    public class AssignWorkZonesFromResource : ICalculation<ITashaPerson, IZone>
    {

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }


        public sealed class OccupationData : IModule
        {
            public sealed class EmploymentStatus : IModule
            {
                [RootModule]
                public ITravelDemandModel Root;

                [SubModelInformation(Required = true, Description = "SparseTriIndex<float>")]
                public IResource Linkages;

                [SubModelInformation(Required = false, Description = "Save the worker probabilities to file, skipped of not filled in.")]
                public FileLocation SaveProbabilities;

                [SubModelInformation(Required = false, Description = "Save the worker categories to file, skipped of not filled in.")]
                public FileLocation SaveWorkerCategory;

                private SparseTriIndex<float> Probabilities;

                private IZone[] Zones;
                private SparseArray<IZone> ZoneSystem;
                private float[][] WorkerResults;
                public void Load()
                {
                    ZoneSystem = Root.ZoneSystem.ZoneArray;
                    Zones = ZoneSystem.GetFlatData();
                    if (SaveWorkerCategory != null)
                    {
                        WorkerResults = new float[3][];
                        for (int i = 0; i < WorkerResults.Length; i++)
                        {
                            WorkerResults[i] = new float[Zones.Length];
                        }
                    }
                    Probabilities = Linkages.AcquireResource<SparseTriIndex<float>>();
                    ConvertToProbabilities(Probabilities.GetFlatData());
                    Linkages.ReleaseResource();
                }

                private void ConvertToProbabilities(float[][][] data)
                {
                    if (SaveProbabilities != null)
                    {
                        SaveProbabilitiesToFile(data);
                    }
                    var pds = Zones.Select(z => z.PlanningDistrict).ToArray();
                    for (int categoryIndex = 0; categoryIndex < data.Length; categoryIndex++)
                    {
                        var category = data[categoryIndex];
                        for (int originIndex = 0; originIndex < category.Length; originIndex++)
                        {
                            var total = VectorHelper.Sum(category[originIndex], 0, category[originIndex].Length);
                            // we do not greater than in case total is NaN, this will pass
                            if (!(total > 0))
                            {
                                Array.Clear(category[originIndex], 0, category[originIndex].Length);
                                continue;
                            }
                            // convert everything to pdf
                            var row = category[originIndex];
                            VectorHelper.Multiply(row, 0, row, 0, 1.0f / total, row.Length);
                            // now that we have pdf we can now build the cdf's
                            for (int i = 1; i < category[originIndex].Length; i++)
                            {
                                row[i] = row[i - 1] + row[i];
                            }
                        }
                    }
                }

                private void SaveProbabilitiesToFile(float[][][] data)
                {
                    SparseArray<IZone> zoneSystem = Root.ZoneSystem.ZoneArray;
                    var zones = zoneSystem.GetFlatData();
                    var saveData = new float[zones.Length][];
                    for (int i = 0; i < saveData.Length; i++)
                    {
                        saveData[i] = new float[zones.Length];
                        for (int j = 0; j < saveData[i].Length; j++)
                        {
                            float total = 0.0f;
                            for (int k = 0; k < data.Length; k++)
                            {
                                total += data[k][i][j];
                            }
                            saveData[i][j] = total;
                        }
                    }
                    SaveData.SaveMatrix(zones, saveData, SaveProbabilities);
                }

                public void Unload()
                {
                    Probabilities = null;
                    SaveHouseholdCategoryRecords();
                }

                public string Name { get; set; }

                public float Progress { get; set; }

                public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

                public bool RuntimeValidation(ref string error)
                {
                    if (!Linkages.CheckResourceType<SparseTriIndex<float>>())
                    {
                        error = "In '" + Name + "' we were unable to load linkages because it is not of type SparseTriIndex<float>.  Please contact your model system provider.";
                        return false;
                    }
                    return true;
                }

                internal IZone ProduceResult(Random random, ITashaHousehold household)
                {
                    var type = ClassifyHousehold(household);
                    var homeZoneIndex = ZoneSystem.GetFlatIndex(household.HomeZone.ZoneNumber);
                    var row = Probabilities.GetFlatData()[type][homeZoneIndex];
                    var pop = (float)random.NextDouble();
                    var index = FindFirstClosestIndex(pop, row);
                    // Detect if there is no actual data in this row and we are drawing from it anyways
                    if (index == 0 && row[index] <= 0.0f)
                    {
                        // check to make sure that there is no probability in the row.
                        if (VectorHelper.Sum(row, 0, row.Length) <= 0.0f)
                        {
                            throw new XTMFRuntimeException(this, $"A person living at zone {household.HomeZone.ZoneNumber} with worker category" +
                                $" {type + 1} tried to find an employment zone.  There was no aggregate data for any workers of this class however.  Please" +
                                $" update your worker categories and zonal residence files for this scenario!\r\n" +
                                $"HHLD#: {household.HouseholdId}");
                        }
                    }
                    return Zones[index];
                }

                private int FindFirstClosestIndex(float pop, float[] row)
                {
                    int max = row.Length;
                    int min = 0;
                    while (min < max)
                    {
                        int mid = (max + min) >> 1;
                        if (row[mid] < pop)
                        {
                            min = mid + 1;
                        }
                        else
                        {
                            max = mid;
                        }
                    }
                    if (min >= row.Length)
                    {
                        min = row.Length - 1;
                    }
                    for (; min > 0; min--)
                    {
                        if (row[min - 1] != row[min])
                        {
                            break;
                        }
                    }
                    return min;
                }

                private int ClassifyHousehold(ITashaHousehold household)
                {
                    var numberOfLicenses = 0;
                    var numberOfVehicles = household.Vehicles.Length;
                    if (numberOfVehicles > 0)
                    {
                        var persons = household.Persons;
                        for (int i = 0; i < persons.Length; i++)
                        {
                            if (persons[i].Licence)
                            {
                                numberOfLicenses++;
                            }
                        }
                    }
                    int category = numberOfLicenses == 0 ? 0 : (numberOfVehicles < numberOfLicenses ? 1 : 2);
                    if (SaveWorkerCategory != null)
                    {
                        RecordHouseholdCategory(category, household.HomeZone.ZoneNumber, household.ExpansionFactor);
                    }
                    return category;
                }

                private void RecordHouseholdCategory(int category, int zoneNumber, float expansionFactor)
                {
                    var flatZoneIndex = Root.ZoneSystem.ZoneArray.GetFlatIndex(zoneNumber);
                    if (flatZoneIndex >= 0)
                    {
                        // this code is never executed in parallel
                        WorkerResults[category][flatZoneIndex] += expansionFactor;
                    }
                }

                private void SaveHouseholdCategoryRecords()
                {
                    if (SaveWorkerCategory != null && WorkerResults != null)
                    {
                        var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
                        using var writer = new StreamWriter(SaveWorkerCategory);
                        writer.WriteLine("Zone,Category,Total");
                        for (int i = 0; i < zones.Length; i++)
                        {
                            var zoneNumber = zones[i].ZoneNumber;
                            for (int cat = 0; cat < WorkerResults.Length; cat++)
                            {
                                writer.Write(zoneNumber);
                                writer.Write(',');
                                writer.Write(cat + 1);
                                writer.Write(',');
                                writer.WriteLine(WorkerResults[cat][i]);
                            }
                        }
                    }
                }
            }

            [SubModelInformation(Required = true)]
            public EmploymentStatus FullTime;

            [SubModelInformation(Required = true)]
            public EmploymentStatus PartTime;


            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }


            public void Load()
            {
                Console.WriteLine("Full-Time...");
                FullTime.Load();
                Console.WriteLine("Part-Time...");
                PartTime.Load();
            }

            public void Unload()
            {
                PartTime.Unload();
                FullTime.Unload();
            }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }

            internal IZone ProduceResult(Random random, ITashaPerson person, ITashaHousehold household)
            {
                switch (person.EmploymentStatus)
                {
                    case TTSEmploymentStatus.FullTime:
                        return FullTime.ProduceResult(random, household);
                    default:
                        return PartTime.ProduceResult(random, household);
                }
            }
        }
        [SubModelInformation(Required = true)]
        public OccupationData Professional;

        [SubModelInformation(Required = true)]
        public OccupationData General;

        [SubModelInformation(Required = true)]
        public OccupationData Sales;

        [SubModelInformation(Required = true)]
        public OccupationData Manufacturing;

        [RunParameter("Random Seed", 154321, "A seed used to generate random numbers.")]
        public int RandomSeed;

        private IZone _roamingZone;

        [RootModule]
        public ITravelDemandModel Root;

        public void Load()
        {
            _roamingZone = Root.ZoneSystem.Get(Root.ZoneSystem.RoamingZoneNumber);
            Console.WriteLine("Loading PoRPoW...");
            Console.WriteLine("Professional...");
            Professional.Load();
            Console.WriteLine("General...");
            General.Load();
            Console.WriteLine("Sales...");
            Sales.Load();
            Console.WriteLine("Manufacturing...");
            Manufacturing.Load();
            Console.WriteLine("Finished Loading PoRPoW");
        }

        [RunParameter("External Zone Ranges", "6000-9999", typeof(RangeSet), "The ranges that represent external zones.")]
        public RangeSet ExternalZones;

        private bool IsExternal(IZone employmentZone)
        {
            return employmentZone != null &&
                (employmentZone == _roamingZone || ExternalZones.Contains(employmentZone.ZoneNumber));
        }

        public IZone ProduceResult(ITashaPerson person)
        {
            // Gather the base data and create our random generator
            IZone empZone;
            if (IsExternal(empZone = person.EmploymentZone))
            {
                return empZone;
            }
            var household = person.Household;
            var random = new Random(RandomSeed * household.HouseholdId);
            switch (person.Occupation)
            {
                case Occupation.Office:
                    return General.ProduceResult(random, person, household);
                case Occupation.Retail:
                    return Sales.ProduceResult(random, person, household);
                case Occupation.Manufacturing:
                    return Manufacturing.ProduceResult(random, person, household);
                default:
                    return Professional.ProduceResult(random, person, household);
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Unload()
        {
            Professional.Unload();
            General.Unload();
            Sales.Unload();
            Manufacturing.Unload();
        }
    }

}
