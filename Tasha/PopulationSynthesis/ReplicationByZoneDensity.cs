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
using TMG.Input;
using XTMF;
using Datastructure;
using TMG.Functions;
using TMG;
using System.IO;

namespace Tasha.PopulationSynthesis
{

    public class ReplicationByZoneDensity : IPostHousehold
    {
        [RootModule]
        public ITashaRuntime Root;

        [RunParameter("Random Seed", "12345", typeof(int), "A base position to have a deterministic random processes.")]
        public int RandomSeed;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        internal class ExpandedHousehold
        {
            internal ITashaHousehold Household;
            internal float ExpansionFactor;
            private float OriginalExpansionFactor;

            public ExpandedHousehold(ITashaHousehold household)
            {
                Household = household;
                OriginalExpansionFactor = ExpansionFactor = household.ExpansionFactor;
                // Normalize person expansion factors
                var inv = 1.0f / ExpansionFactor;
                foreach (var person in household.Persons)
                {
                    person.ExpansionFactor *= inv;
                }
            }

            internal void ResetExpansion()
            {
                ExpansionFactor = OriginalExpansionFactor;
            }
        }


        public sealed class PopulationPool : XTMF.IModule
        {

            [RunParameter("Regions", "1-6", typeof(RangeSet), "The regions that are included in this pool.")]
            public RangeSet Regions;

            [RunParameter("Density Bins", "0+", typeof(RangeSet), "The set of zonal densities to separate the contained zones into.")]
            public RangeSet DensityBins;

            private List<ExpandedHousehold>[] DensityPool;
            private List<int>[] PoolToGlobalIndex;

            private int[] InitialZoneClassification;
            private int[] ForecastZoneClassification;

            private double TotalExpansionFactor;

            public void Initialize(int[] regionNumber, float[] originalZoneDensities, float[] forecastZoneDensities)
            {
                InitialZoneClassification = originalZoneDensities.Select((d, i) => Regions.Contains(regionNumber[i]) ? ClassifyZone(d) : -1).ToArray();
                ForecastZoneClassification = forecastZoneDensities.Select((d, i) => Regions.Contains(regionNumber[i]) ? ClassifyZone(d) : -1).ToArray();
                DensityPool = new List<ExpandedHousehold>[DensityBins.Count];
                PoolToGlobalIndex = new List<int>[DensityBins.Count];
                for (int i = 0; i < DensityPool.Length; i++)
                {
                    DensityPool[i] = new List<ExpandedHousehold>();
                    PoolToGlobalIndex[i] = new List<int>();
                }

            }

            private int ClassifyZone(float density)
            {
                var bin = DensityBins.IndexOf((int)density);
                if (bin < 0)
                {
                    bin = DensityBins.Count - 1;
                }
                return bin;
            }

            internal List<KeyValuePair<int, int>> Process(int randomSeed, IZone[] zones)
            {
                bool any;
                Random random = new Random(randomSeed);
                var ret = new List<KeyValuePair<int, int>>();
                var remaining = zones.Select(z => z.Population).ToArray();
                TotalExpansionFactor = DensityPool.Sum(pool => pool.Sum(h => h.ExpansionFactor));
                do
                {
                    any = false;
                    for (int zone = 0; zone < zones.Length; zone++)
                    {
                        if (ForecastZoneClassification[zone] >= 0 && remaining[zone] > 0)
                        {
                            any = true;
                            ret.Add(Pick(random, ForecastZoneClassification[zone], zone, ref remaining[zone]));
                        }
                    }
                } while (any);
                return ret;
            }

            private KeyValuePair<int, int> Pick(Random random, int densityCat, int zone, ref int remaining)
            {
                var pool = DensityPool[densityCat];
                var indexes = PoolToGlobalIndex[densityCat];
                for (int unused = 0; unused < 2; unused++)
                {
                    var place = (float)random.NextDouble() * TotalExpansionFactor;
                    float current = 0.0f;
                    for (int i = 0; i < pool.Count; i++)
                    {
                        current += pool[i].ExpansionFactor;
                        if (current > place)
                        {
                            var numberOfPersons = pool[i].Household.Persons.Length;
                            // skip adding this particular household if it has too many persons
                            if (remaining < numberOfPersons)
                            {
                                continue;
                            }
                            pool[i].ExpansionFactor -= 1;
                            var remainder = 0.0f;
                            if (pool[i].ExpansionFactor <= 0)
                            {
                                remainder = -pool[i].ExpansionFactor;
                                pool[i].ExpansionFactor = 0;
                            }
                            TotalExpansionFactor -= 1 - remainder;
                            remaining -= numberOfPersons;
                            return new KeyValuePair<int, int>(zone, indexes[i]);
                        }
                    }
                    // if we get here then it failed, we need to reset the probabilities again
                    TotalExpansionFactor = pool.Sum(h =>
                    {
                        h.ResetExpansion();
                        return h.ExpansionFactor;
                    });
                }
                throw new XTMFRuntimeException("We managed to be unable to assign any households to flat zone '" + zone.ToString() + "' in Pool'" + Name + "'!");
            }

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public bool RuntimeValidation(ref string error)
            {
                if (DensityBins.Count <= 0)
                {
                    error = "In '" + Name + "' at least one density bin needs to be defined!";
                    return false;
                }
                return true;
            }

            internal void AddIfContained(ITashaHousehold household, int homeZoneIndex, int globalIndex)
            {
                // if we record this zone
                int index;
                if ((index = InitialZoneClassification[homeZoneIndex]) >= 0)
                {

                    DensityPool[index].Add(new ExpandedHousehold(household));
                    PoolToGlobalIndex[index].Add(globalIndex);
                }
            }
        }

        [SubModelInformation(Required = true, Description = "The different segments of the area to generate a population for.")]
        public PopulationPool[] PopulationPools;

        [SubModelInformation(Required = true, Description = "The population from the base year.")]
        public IDataSource<SparseArray<float>> BaseYearPopulation;

        private List<ITashaHousehold> Households;

        public void Execute(ITashaHousehold household, int iteration)
        {
            lock (this)
            {
                var householdZone = ZoneSystem.GetFlatIndex(household.HomeZone.ZoneNumber);
                Households.Add(household);
                foreach (var pool in PopulationPools)
                {
                    pool.AddIfContained(household, householdZone, Households.Count - 1);
                }
            }
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

        private SparseArray<IZone> ZoneSystem;

        public void IterationFinished(int iteration)
        {
            var zoneArray = Root.ZoneSystem.ZoneArray;
            var zones = zoneArray.GetFlatData();
            var random = new Random(RandomSeed);
            var randomSeed = PopulationPools.Select(_ => random.Next()).ToArray();
            List<KeyValuePair<int, int>>[] results = new List<KeyValuePair<int, int>>[PopulationPools.Length];
            Parallel.For(0, PopulationPools.Length, (int i) =>
            {
                // make sure we don't generate persons for the external zones
                results[i] = PopulationPools[i].Process(randomSeed[i], zones);
            });
            Save(results);
        }

        private void Save(List<KeyValuePair<int, int>>[] results)
        {
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData().Select(z => z.ZoneNumber).ToArray();
            int totalPerson = 0;
            var householdID = SaveHouseholds(results, zones);
            householdID = SavePersons(results, ref totalPerson);
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
            using (StreamWriter writer = new StreamWriter(BuildFileName(occupation, empStat, WorkerForceDirectory)))
            {
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
        }

        private void SaveWorkerCategoryData(IZone[] zones, Occupation occupation, TTSEmploymentStatus empStat, float[][] workers)
        {
            using (StreamWriter writer = new StreamWriter(BuildFileName(occupation, empStat, WorkerCategoryDirectory)))
            {
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
        }

        private void SaveSummeryFile(int totalPerson, int householdID)
        {
            if (SummaryFile != null)
            {
                using (var writer = new StreamWriter(SummaryFile))
                {
                    writer.WriteLine("Type,Total");
                    writer.Write("Households,");
                    writer.WriteLine((householdID - 1));
                    writer.Write("Persons,");
                    writer.WriteLine(totalPerson);
                }
            }
        }

        private static int ClassifyHouseholdWorkerCategory(ITashaHousehold household)
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

        private int SavePersons(List<KeyValuePair<int, int>>[] results, ref int totalPerson)
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
                    foreach (var record in results[i])
                    {
                        var zone = record.Key;
                        var household = Households[record.Value];
                        var persons = household.Persons;
                        var workerCategory = ClassifyHouseholdWorkerCategory(household);
                        for (int j = 0; j < persons.Length; j++)
                        {
                            totalPerson++;
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

        private int SaveHouseholds(List<KeyValuePair<int, int>>[] results, int[] zones)
        {
            int householdID = 1;
            using (var writer = new StreamWriter(HouseholdFile))
            {
                writer.WriteLine("HouseholdID,Zone,ExpansionFactor,DwellingType,NumberOfPersons,NumberOfVehicles");
                for (int i = 0; i < results.Length; i++)
                {
                    foreach (var record in results[i])
                    {
                        var zone = record.Key;
                        var household = Households[record.Value];
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

            return householdID;
        }

        public void IterationStarting(int iteration)
        {
            Households = new List<ITashaHousehold>();
            ZoneSystem = Root.ZoneSystem.ZoneArray;
            var zones = ZoneSystem.GetFlatData();
            BaseYearPopulation.LoadData();
            var baseDensity = BaseYearPopulation.GiveData().GetFlatData().Clone() as float[];
            BaseYearPopulation.UnloadData();
            var area = zones.Select(z =>
            {
                // A = (6InternalDistance)^2
                // since the units are meters we can divide by 1000 to get to pop/km
                // 0.006 is 6/1000
                var dist = (z.InternalDistance * 0.006f);
                return dist * dist;
            }).ToArray();
            var forecastDensity = zones.Select(z => (float)z.Population).ToArray();
            VectorHelper.Divide(baseDensity, 0, baseDensity, 0, area, 0, baseDensity.Length);
            var regions = zones.Select(z => z.RegionNumber).ToArray();
            VectorHelper.Divide(forecastDensity, 0, forecastDensity, 0, area, 0, baseDensity.Length);
            foreach (var pool in PopulationPools)
            {
                pool.Initialize(regions, baseDensity, forecastDensity);
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
