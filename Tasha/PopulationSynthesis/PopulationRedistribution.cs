/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tasha.Common;
using XTMF;
using Datastructure;
using TMG.Functions;
using TMG.Input;
using System.IO;
using TMG;

namespace Tasha.PopulationSynthesis
{
    [ModuleInformation(
        Description =
 @"This module is designed to allow for keeping a static generated population for a base scenario and then transforming that base to a separate forecast, with the same total population by region,
where you still want the same demographics."
        )]
    public class PopulationRedistribution : IPostHousehold
    {
        [RootModule]
        public ITashaRuntime Root;

        [RunParameter("Random Seed", "12345", typeof(int), "A base position to have a deterministic random processes.")]
        public int RandomSeed;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [SubModelInformation(Required = true, Description = "The population that we are going to need to transform to.")]
        public IDataSource<SparseArray<float>> ForecastPopulation;

        private SparseArray<List<ITashaHousehold>> HouseholdsByRegion;
        private SparseArray<List<ITashaHousehold>> HouseholdsByZone;

        public void IterationStarting(int iteration)
        {
            var zoneSystem = Root.ZoneSystem.ZoneArray;
            // initialize data structures
            HouseholdsByZone = zoneSystem.CreateSimilarArray<List<ITashaHousehold>>();
            HouseholdsByRegion = ZoneSystemHelper.CreateRegionArray<List<ITashaHousehold>>(zoneSystem);
            SetupSpatialListByElement(HouseholdsByZone.GetFlatData());
            SetupSpatialListByElement(HouseholdsByRegion.GetFlatData());
        }

        private static void SetupSpatialListByElement<T>(List<T>[] toInit)
        {
            for (int i = 0; i < toInit.Length; i++)
            {
                toInit[i] = [];
            }
        }

        public void Execute(ITashaHousehold household, int iteration)
        {
            lock (this)
            {
                var householdZone = household.HomeZone;
                HouseholdsByZone[householdZone.ZoneNumber].Add(household);
                HouseholdsByRegion[householdZone.RegionNumber].Add(household);
            }
        }

        public void IterationFinished(int iteration)
        {
            var flatRegion = HouseholdsByRegion.GetFlatData();
            var zoneArray = Root.ZoneSystem.ZoneArray;
            var zones = zoneArray.GetFlatData();
            RandomizeHouseholdOrder();
            var basePopulation = Root.ZoneSystem.ZoneArray.GetFlatData().Select(z => z.Population).ToArray();
            int[] zonalDifferences = BuildScenarioDifferencesByZone(basePopulation);
            List<KeyValuePair<int, int>>[] results = new List<KeyValuePair<int, int>>[flatRegion.Length];
            for (int i = 0; i < results.Length; i++)
            {
                results[i] = [];
            }
            // make a copy of the households by region so we can subtract out the households that have already been used
            // we can't just edit the households by region because we need them for indexing
            List<ITashaHousehold>[] remainingHouseholds = new List<ITashaHousehold>[HouseholdsByRegion.Count];
            List<int>[] lookupsForRegion = new List<int>[remainingHouseholds.Length];
            for (int i = 0; i < remainingHouseholds.Length; i++)
            {
                remainingHouseholds[i] = [];
                lookupsForRegion[i] = [];
            }
            //Step 1 fill up the zones with residences that will stay
            Pass1(zoneArray, zones, basePopulation, zonalDifferences, results, remainingHouseholds, lookupsForRegion);
            Pass2(zones, zonalDifferences, results, remainingHouseholds, lookupsForRegion);
            Console.WriteLine("Saving results");
            Save(results, flatRegion);
        }

        private void Pass2(IZone[] zones, int[] zonalDifferences, List<KeyValuePair<int, int>>[] results, List<ITashaHousehold>[] remainingHouseholds, List<int>[] lookupsForRegion)
        {
            foreach (var regionRemaining in lookupsForRegion)
            {
                Console.WriteLine(regionRemaining.Count);
            }
            Console.WriteLine("Starting Pass 2");
            //Step 2 do intra-regional redistribution
            Parallel.For(0, remainingHouseholds.Length, regionIndex =>
            {
                if (regionIndex != 0)
                {
                    var regionNumber = HouseholdsByRegion.GetSparseIndex(regionIndex);
                    int remainingIndex = 0;
                    var remainingList = remainingHouseholds[regionIndex];
                    var resultsForRegion = results[regionIndex];
                    if (remainingList.Count <= 0)
                    {
                        return;
                    }
                    var lookup = lookupsForRegion[regionIndex];
                    var zonesToProcess = zones.Where(z => z.RegionNumber == regionNumber).ToArray();
                    // for each zone in the set of zones that belong to the region
                    for (int zone = 0; zone < zonesToProcess.Length; zone++)
                    {
                        var zoneIndex = Array.IndexOf(zones, zonesToProcess[zone]);
                        for (int pop = 0; pop < zonalDifferences[zoneIndex];)
                        {
                            var hhld = remainingList[remainingIndex];
                            var newPop = hhld.Persons.Length + pop;
                            if (newPop <= zonalDifferences[zoneIndex])
                            {
                                pop = newPop;
                                resultsForRegion.Add(new KeyValuePair<int, int>(zoneIndex, lookup[remainingIndex]));
                            }
                            if (++remainingIndex >= remainingList.Count)
                            {
                                remainingIndex = 0;
                            }
                        }
                    }
                }
            });
        }

        private void Pass1(SparseArray<IZone> zoneArray, IZone[] zones, int[] basePopulation, int[] zonalDifferences, List<KeyValuePair<int, int>>[] results,
            List<ITashaHousehold>[] remainingHouseholds, List<int>[] lookupsForRegion)
        {
            SparseArray<List<int>> householdsByZoneIndexToRegion = zoneArray.CreateSimilarArray<List<int>>();
            SetupSpatialListByElement(householdsByZoneIndexToRegion.GetFlatData());
            Console.WriteLine("Preparing household Index");
            Parallel.For(0, HouseholdsByZone.Count, i =>
            {
                var list = householdsByZoneIndexToRegion.GetFlatData()[i];
                var total = HouseholdsByZone.GetFlatData()[i].Count;
                for (int j = 0; j < total; j++)
                {
                    list.Add(-1);
                }
            });
            Console.WriteLine("Building household index");
            Parallel.For(0, HouseholdsByRegion.Count, flatRegionIndex =>
            {
                if (flatRegionIndex == 0)
                {
                    return;
                }
                var region = HouseholdsByRegion.GetFlatData()[flatRegionIndex];
                var householdIndexArray = householdsByZoneIndexToRegion.GetFlatData();
                var householdArray = HouseholdsByZone.GetFlatData();
                for (int i = 0; i < region.Count; i++)
                {
                    var currentHousehold = region[i];
                    var flatZone = zoneArray.GetFlatIndex(currentHousehold.HomeZone.ZoneNumber);
                    var household = householdArray[flatZone];
                    var householdIndex = householdIndexArray[flatZone];
                    int index = household.IndexOf(currentHousehold);
                    householdIndex[index] = i;
                }
            });
            Console.WriteLine("Starting Pass 1");
            Parallel.For(0, HouseholdsByRegion.Count, flatRegionIndex =>
            {
                if (flatRegionIndex == 0)
                {
                    return;
                }
                var regionNumber = HouseholdsByRegion.GetSparseIndex(flatRegionIndex);
                var zonesToProcess = zones.Where(z => z.RegionNumber == regionNumber).ToArray();
                var remaining = remainingHouseholds[flatRegionIndex];
                var resultsForRegion = results[flatRegionIndex];
                for (int z = 0; z < zonesToProcess.Length; z++)
                {
                    int attempts = 0;
                    int flatZone = zoneArray.GetFlatIndex(zonesToProcess[z].ZoneNumber);
                    var householdIndex = householdsByZoneIndexToRegion.GetFlatData()[flatZone];
                    // the difference is negative so we need to add not subtract
                    int populationToAdd = zonalDifferences[flatZone] >= 0 ? basePopulation[flatZone] : basePopulation[flatZone] + zonalDifferences[flatZone];
                    var zonalHouseholds = HouseholdsByZone.GetFlatData()[flatZone];
                    int i = 0;
                    List<int> tossedHouseholds = [];
                    for (int pop = 0; pop < populationToAdd;)
                    {
                        if (attempts > 2)
                        {
                            throw new XTMFRuntimeException(this, "In '" + Name + "' we were unable to assign a base population for zone '" + zonesToProcess[z].ZoneNumber + "'!");
                        }
                        for (i = 0; i < zonalHouseholds.Count; i++)
                        {
                            var hhld = zonalHouseholds[i];
                            var newPop = pop + hhld.Persons.Length;
                            if (newPop <= populationToAdd)
                            {
                                pop = newPop;
                                resultsForRegion.Add(new KeyValuePair<int, int>(flatZone, householdIndex[i]));
                                if (pop >= populationToAdd)
                                {
                                    break;
                                }
                            }
                            else
                            {
                                tossedHouseholds.Add(i);
                            }
                        }
                        attempts++;
                    }
                    // if we didn't use everything add the rest of the households to the remaining list
                    var lookupIndex = lookupsForRegion[flatRegionIndex];
                    // first add all the households we excluded to finish the zone
                    foreach (var tossed in tossedHouseholds)
                    {
                        remaining.Add(zonalHouseholds[tossed]);
                        lookupIndex.Add(householdIndex[tossed]);
                    }
                    // then add the rest of the households we haven't looked at
                    if (attempts <= 1)
                    {
                        for (; i < zonalHouseholds.Count; i++)
                        {
                            remaining.Add(zonalHouseholds[i]);
                            lookupIndex.Add(householdIndex[i]);
                        }
                    }
                }
            });
        }

        private void RandomizeHouseholdOrder()
        {
            // First generate a random seed per region so this is deterministic
            var numberOfRegions = HouseholdsByRegion.Count;
            Random randomSeedGenerator = new Random(RandomSeed);
            var seeds = new int[numberOfRegions];
            for (int i = 0; i < seeds.Length; i++)
            {
                seeds[i] = randomSeedGenerator.Next();
            }
            //now that we have our seeds shuffle each position once for each region
            Parallel.For(0, numberOfRegions, flatRegionIndex =>
            {
                Random r = new Random(seeds[flatRegionIndex]);
                var list = HouseholdsByRegion.GetFlatData()[flatRegionIndex];
                for (int i = 0; i < list.Count; i++)
                {
                    Swap(list, i, r.Next(0, list.Count));
                }
            });
        }

        private static void Swap(List<ITashaHousehold> list, int i, int newPos)
        {
            var temp = list[i];
            list[i] = list[newPos];
            list[newPos] = temp;
        }

        private int[] BuildScenarioDifferencesByZone(int[] basePopulation)
        {
            ForecastPopulation.LoadData();
            var newScenarioZonePop = ForecastPopulation.GiveData().GetFlatData();
            ForecastPopulation.UnloadData();
            return basePopulation.Select((z, i) => (int)newScenarioZonePop[i] - z).ToArray();
        }

        [SubModelInformation(Required = true, Description = "The location to save the household file.")]
        public FileLocation HouseholdFile;

        [SubModelInformation(Required = true, Description = "The location to save the person file.")]
        public FileLocation PersonFile;

        [SubModelInformation(Required = false, Description = "Saves population/household totals.")]
        public FileLocation SummaryFile;

        [SubModelInformation(Required = true, Description = "The directory to store the worker force information.")]
        public FileLocation WorkerForceDirectory;

        [SubModelInformation(Required = true, Description = "The directory to store the worker category information to.")]
        public FileLocation WorkerCategoryDirectory;

        private void Save(List<KeyValuePair<int, int>>[] results, List<ITashaHousehold>[] pds)
        {
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData().Select(z => z.ZoneNumber).ToArray();
            int totalPerson = 0;
            SaveHouseholds(results, pds, zones);
            var householdID = SavePersons(results, pds, ref totalPerson);
            SaveSummeryFile(totalPerson, householdID);
        }


        private string BuildFileName(Occupation occ, TTSEmploymentStatus empStat, FileLocation fileLocation)
        {
            var dirPath = fileLocation.GetFilePath();
            var info = new DirectoryInfo(dirPath);
            if (!info.Exists)
            {
                info.Create();
            }
            StringBuilder buildFileName = new StringBuilder();
            switch (occ)
            {
                case Occupation.Professional:
                    buildFileName.Append("P");
                    break;
                case Occupation.Office:
                    buildFileName.Append("G");
                    break;
                case Occupation.Retail:
                    buildFileName.Append("S");
                    break;
                case Occupation.Manufacturing:
                    buildFileName.Append("M");
                    break;
            }
            switch (empStat)
            {
                case TTSEmploymentStatus.FullTime:
                    buildFileName.Append("F.csv");
                    break;
                case TTSEmploymentStatus.PartTime:
                    buildFileName.Append("P.csv");
                    break;
            }
            return Path.Combine(dirPath, buildFileName.ToString());
        }

        private void SaveWorkerData(IZone[] zones, Occupation occupation, TTSEmploymentStatus empStat, float[] workers)
        {
            using StreamWriter writer = new StreamWriter(BuildFileName(occupation, empStat, WorkerForceDirectory));
            writer.WriteLine("Zone,Persons");
            for (int i = 0; i < workers.Length; i++)
            {
                if (workers[i] > 0)
                {
                    writer.Write(zones[i].ZoneNumber);
                    writer.Write(',');
                    writer.WriteLine(workers[i]);
                }
            }
        }

        private void SaveWorkerCategoryData(IZone[] zones, Occupation occupation, TTSEmploymentStatus empStat, float[][] workers)
        {
            using StreamWriter writer = new StreamWriter(BuildFileName(occupation, empStat, WorkerCategoryDirectory));
            writer.WriteLine("Zone,WorkerCategory,Persons");
            for (int i = 0; i < workers.Length; i++)
            {
                var factor = 1.0f / workers[i].Sum();
                if (float.IsNaN(factor))
                {
                    continue;
                }
                for (int cat = 0; cat < workers[i].Length; cat++)
                {
                    workers[i][cat] *= factor;
                }
                for (int cat = 0; cat < workers[i].Length; cat++)
                {
                    if (workers[i][cat] > 0)
                    {
                        writer.Write(zones[i].ZoneNumber);
                        writer.Write(',');
                        writer.Write(cat + 1);
                        writer.Write(',');
                        writer.WriteLine(workers[i][cat]);
                    }
                }
            }
        }

        private void SaveSummeryFile(int totalPerson, int householdID)
        {
            if (SummaryFile != null)
            {
                using var writer = new StreamWriter(SummaryFile);
                writer.WriteLine("Type,Total");
                writer.Write("Households,");
                writer.WriteLine((householdID - 1));
                writer.Write("Persons,");
                writer.WriteLine(totalPerson);
            }
        }

        private static int ClassifyHousehold(ITashaHousehold household)
        {
            var lics = 0;
            var numberOfVehicles = household.Vehicles.Length;
            if (numberOfVehicles == 0)
            {
                return 0;
            }
            var persons = household.Persons;
            for (int i = 0; i < persons.Length; i++)
            {
                if (persons[i].Licence)
                {
                    lics++;
                }
            }
            if (lics == 0) return 0;
            return numberOfVehicles < lics ? 1 : 2;
        }

        private int SavePersons(List<KeyValuePair<int, int>>[] results, List<ITashaHousehold>[] householdRegions, ref int totalPerson)
        {
            int householdID;
            var zoneArray = Root.ZoneSystem.ZoneArray;
            var zones = zoneArray.GetFlatData();
            var workersPF = new float[zones.Length];
            var workersGF = new float[zones.Length];
            var workersSF = new float[zones.Length];
            var workersMF = new float[zones.Length];
            var workersPP = new float[zones.Length];
            var workersGP = new float[zones.Length];
            var workersSP = new float[zones.Length];
            var workersMP = new float[zones.Length];

            var workersCatPF = new float[zones.Length][];
            var workersCatGF = new float[zones.Length][];
            var workersCatSF = new float[zones.Length][];
            var workersCatMF = new float[zones.Length][];
            var workersCatPP = new float[zones.Length][];
            var workersCatGP = new float[zones.Length][];
            var workersCatSP = new float[zones.Length][];
            var workersCatMP = new float[zones.Length][];
            for (int i = 0; i < zones.Length; i++)
            {
                workersCatPF[i] = new float[3];
                workersCatGF[i] = new float[3];
                workersCatSF[i] = new float[3];
                workersCatMF[i] = new float[3];
                workersCatPP[i] = new float[3];
                workersCatGP[i] = new float[3];
                workersCatSP[i] = new float[3];
                workersCatMP[i] = new float[3];
            }
            using (var writer = new StreamWriter(PersonFile))
            {
                householdID = 1;
                writer.WriteLine("HouseholdID,PersonNumber,Age,Sex,License,TransitPass,EmploymentStatus,Occupation,FreeParking,StudentStatus,EmploymentZone,SchoolZone,ExpansionFactor");
                for (int i = 0; i < results.Length; i++)
                {
                    var households = householdRegions[i];
                    foreach (var record in results[i])
                    {
                        var zone = record.Key;
                        var persons = households[record.Value].Persons;
                        var workerCategory = ClassifyHousehold(households[record.Value]);
                        totalPerson += persons.Length;
                        for (int j = 0; j < persons.Length; j++)
                        {
                            writer.Write(householdID);
                            writer.Write(',');
                            writer.Write((j + 1));
                            writer.Write(',');
                            writer.Write(persons[j].Age);
                            writer.Write(',');
                            writer.Write(persons[j].Female ? "F," : "M,");
                            writer.Write(persons[j].Licence ? "Y," : "N,");
                            switch (persons[j].TransitPass)
                            {
                                case TransitPass.Metro:
                                    writer.Write("M,");
                                    break;
                                case TransitPass.Go:
                                    writer.Write("G,");
                                    break;
                                case TransitPass.Combination:
                                    writer.Write("C,");
                                    break;
                                default:
                                    writer.Write("N,");
                                    break;
                            }
                            switch (persons[j].EmploymentStatus)
                            {
                                case TTSEmploymentStatus.FullTime:
                                    writer.Write("F,");
                                    break;
                                case TTSEmploymentStatus.PartTime:
                                    writer.Write("P,");
                                    break;
                                default:
                                    writer.Write("O,");
                                    break;
                            }
                            switch (persons[j].Occupation)
                            {
                                case Occupation.Professional:
                                    writer.Write("P,");
                                    break;
                                case Occupation.Office:
                                    writer.Write("G,");
                                    break;
                                case Occupation.Retail:
                                    writer.Write("S,");
                                    break;
                                case Occupation.Manufacturing:
                                    writer.Write("M,");
                                    break;
                                default:
                                    writer.Write("O,");
                                    break;
                            }
                            var workZone = persons[j].EmploymentZone;
                            var schoolZone = persons[j].SchoolZone;
                            var personExpanded = persons[j].ExpansionFactor;
                            if (!IsExternal(workZone))
                            {
                                switch (persons[j].EmploymentStatus)
                                {
                                    case TTSEmploymentStatus.FullTime:
                                        switch (persons[j].Occupation)
                                        {
                                            case Occupation.Professional:
                                                workersPF[zone] += personExpanded;
                                                workersCatPF[zone][workerCategory] += personExpanded;
                                                break;
                                            case Occupation.Office:
                                                workersGF[zone] += personExpanded;
                                                workersCatGF[zone][workerCategory] += personExpanded;
                                                break;
                                            case Occupation.Retail:
                                                workersSF[zone] += personExpanded;
                                                workersCatSF[zone][workerCategory] += personExpanded;
                                                break;
                                            case Occupation.Manufacturing:
                                                workersMF[zone] += personExpanded;
                                                workersCatMF[zone][workerCategory] += personExpanded;
                                                break;
                                        }
                                        break;
                                    case TTSEmploymentStatus.PartTime:
                                        switch (persons[j].Occupation)
                                        {
                                            case Occupation.Professional:
                                                workersPP[zone] += personExpanded;
                                                workersCatPP[zone][workerCategory] += personExpanded;
                                                break;
                                            case Occupation.Office:
                                                workersGP[zone] += personExpanded;
                                                workersCatGP[zone][workerCategory] += personExpanded;
                                                break;
                                            case Occupation.Retail:
                                                workersSP[zone] += personExpanded;
                                                workersCatSP[zone][workerCategory] += personExpanded;
                                                break;
                                            case Occupation.Manufacturing:
                                                workersMP[zone] += personExpanded;
                                                workersCatMP[zone][workerCategory] += personExpanded;
                                                break;
                                        }
                                        break;
                                }
                            }
                            writer.Write(persons[j].FreeParking ? "Y," : "N,");
                            switch (persons[j].StudentStatus)
                            {
                                case StudentStatus.FullTime:
                                    writer.Write("F,");
                                    break;
                                case StudentStatus.PartTime:
                                    writer.Write("P,");
                                    break;
                                default:
                                    writer.Write("O,");
                                    break;
                            }

                            // we don't save employment or school zone
                            if (IsExternal(workZone))
                            {
                                writer.Write(workZone.ZoneNumber);
                            }
                            else
                            {
                                writer.Write('0');
                            }
                            writer.Write(',');
                            if (IsExternal(schoolZone))
                            {
                                writer.Write(schoolZone.ZoneNumber);
                            }
                            else
                            {
                                writer.Write('0');
                            }
                            writer.Write(',');
                            writer.WriteLine(persons[j].ExpansionFactor);
                        }
                        householdID++;
                    }
                }
            }
            SaveWorkerData(zones, Occupation.Professional, TTSEmploymentStatus.FullTime, workersPF);
            SaveWorkerData(zones, Occupation.Office, TTSEmploymentStatus.FullTime, workersGF);
            SaveWorkerData(zones, Occupation.Retail, TTSEmploymentStatus.FullTime, workersSF);
            SaveWorkerData(zones, Occupation.Manufacturing, TTSEmploymentStatus.FullTime, workersMF);
            SaveWorkerData(zones, Occupation.Professional, TTSEmploymentStatus.PartTime, workersPP);
            SaveWorkerData(zones, Occupation.Office, TTSEmploymentStatus.PartTime, workersGP);
            SaveWorkerData(zones, Occupation.Retail, TTSEmploymentStatus.PartTime, workersSP);
            SaveWorkerData(zones, Occupation.Manufacturing, TTSEmploymentStatus.PartTime, workersMP);

            SaveWorkerCategoryData(zones, Occupation.Professional, TTSEmploymentStatus.FullTime, workersCatPF);
            SaveWorkerCategoryData(zones, Occupation.Office, TTSEmploymentStatus.FullTime, workersCatGF);
            SaveWorkerCategoryData(zones, Occupation.Retail, TTSEmploymentStatus.FullTime, workersCatSF);
            SaveWorkerCategoryData(zones, Occupation.Manufacturing, TTSEmploymentStatus.FullTime, workersCatMF);
            SaveWorkerCategoryData(zones, Occupation.Professional, TTSEmploymentStatus.PartTime, workersCatPP);
            SaveWorkerCategoryData(zones, Occupation.Office, TTSEmploymentStatus.PartTime, workersCatGP);
            SaveWorkerCategoryData(zones, Occupation.Retail, TTSEmploymentStatus.PartTime, workersCatSP);
            SaveWorkerCategoryData(zones, Occupation.Manufacturing, TTSEmploymentStatus.PartTime, workersCatMP);

            return householdID;
        }


        [RunParameter("External Zone Ranges", "6000-6999", typeof(RangeSet), "The ranges that represent external zones.")]
        public RangeSet ExternalZones;

        private bool IsExternal(IZone employmentZone)
        {
            return employmentZone != null && ExternalZones.Contains(employmentZone.ZoneNumber);
        }

        private void SaveHouseholds(List<KeyValuePair<int, int>>[] results, List<ITashaHousehold>[] householdsByRegion, int[] zones)
        {
            int householdID = 1;
            using var writer = new StreamWriter(HouseholdFile);
            writer.WriteLine("HouseholdID,Zone,ExpansionFactor,DwellingType,NumberOfPersons,NumberOfVehicles");
            for (int i = 0; i < results.Length; i++)
            {
                var households = householdsByRegion[i];
                foreach (var record in results[i])
                {
                    var zone = record.Key;
                    var household = households[record.Value];
                    writer.Write(householdID);
                    writer.Write(',');
                    writer.Write(zones[zone]);
                    // the expansion factor is always 1
                    writer.Write(",1,");
                    writer.Write((int)household.DwellingType);
                    writer.Write(',');
                    writer.Write(household.Persons.Length);
                    writer.Write(',');
                    writer.WriteLine(household.Vehicles.Length);
                    householdID++;
                }
            }
        }

        public void Load(int maxIterations)
        {

        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

}
