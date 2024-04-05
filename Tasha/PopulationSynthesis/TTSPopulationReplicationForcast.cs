/*
    Copyright 2014-2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;
using Tasha.Common;
using TMG.Functions;
using Datastructure;
using System.Threading;
using TMG;
using System.Threading.Tasks;
using TMG.Input;
using System.IO;

namespace Tasha.PopulationSynthesis
{
    public class TTSPopulationReplicationForcast : IPostHousehold
    {
        [RootModule]
        public ITashaRuntime Root;

        [RunParameter("Random Seed", "12345", typeof(int), "A base position to have a deterministic random processes.")]
        public int RandomSeed;

        [RunParameter("Expansion Factor Scale", 1.0f, "Setting this to 2 would double the number of people sampled per zone (at half the expansion factor).")]
        public float HouseholdExpansionFactor;

        [RunParameter("External Zone Ranges", "6000-6999", typeof(RangeSet), "The ranges that represent external zones.")]
        public RangeSet ExternalZones;

        [RunParameter("Allow Work At Home Status", false, "Set this to true to allow work at home employment statuses.")]
        public bool WriteWorkAtHomeEmploymentStatus;

        private float _invHouseholdExpansion;

        private SparseArray<PDData> _householdsByPD;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private class ExpandedHousehold
        {
            internal ITashaHousehold Household;
            internal float ExpansionFactor;
            private float _originalExpansionFactor;

            public ExpandedHousehold(ITashaHousehold household)
            {
                Household = household;
                _originalExpansionFactor = ExpansionFactor = (household.Persons.Sum(p => p.ExpansionFactor) / household.Persons.Length);
                // Normalize person expansion factors
                var inv = 1.0f / ExpansionFactor;
                foreach (var person in household.Persons)
                {
                    person.ExpansionFactor *= inv;
                }
            }

            internal void ReAdjustOriginalExpansion(float scale)
            {
                _originalExpansionFactor *= scale;
                ExpansionFactor = _originalExpansionFactor;
            }

            internal void ResetExpansion() => ExpansionFactor = _originalExpansionFactor;
        }

        private class PDData
        {
            private readonly int _pd;
            internal readonly List<ExpandedHousehold> Households = new(10);
            internal float TotalExpansionFactor;
            private SpinLock _lock = new(false);

            public PDData(int pd)
            {
                _pd = pd;
            }

            internal void Add(ITashaHousehold household)
            {
                var expansionFactor = household.ExpansionFactor;
                var newHhld = new ExpandedHousehold(household);
                bool taken = false;
                _lock.Enter(ref taken);
                TotalExpansionFactor += expansionFactor;
                Households.Add(newHhld);
                if (taken) _lock.Exit(true);
            }

            internal List<KeyValuePair<int, int>> ProcessPD(int randomSeed, IZone[] zones, float householdExpansion, int[] zoneIndexes)
            {
                bool any;
                Random random = new(randomSeed * _pd);
                var rPerZone = zoneIndexes.Select(z => new Random(random.Next())).ToArray();
                var ret = new List<KeyValuePair<int, int>>();
                var remaining = zoneIndexes.Select((z) => (int)Math.Round(zones[z].Population * householdExpansion)).ToArray();
                TotalExpansionFactor = Households.Sum(h => h.ExpansionFactor);
                var populationScaleRatio = remaining.Sum() / TotalExpansionFactor;
                foreach (var hhld in Households)
                {
                    hhld.ReAdjustOriginalExpansion(populationScaleRatio);
                }
                do
                {
                    any = false;
                    for (int zone = 0; zone < zoneIndexes.Length; zone++)
                    {
                        if (remaining[zone] > 0)
                        {
                            any = true;
                            ret.Add(Pick(rPerZone[zone], zoneIndexes[zone], ref remaining[zone]));
                        }
                    }
                } while (any);
                return ret;
            }

            int _previouslyPicked = 0;
            int _previouslyPickedTimes = 0;

            private KeyValuePair<int, int> Pick(Random random, int zone, ref int remaining)
            {
                for (int unused = 0; unused < 3; unused++)
                {
                    var place = (float)random.NextDouble() * TotalExpansionFactor;
                    float current = 0.0f;
                    if (TotalExpansionFactor > 0)
                    {
                        for (int i = 0; i < Households.Count; i++)
                        {
                            current += Households[i].ExpansionFactor;
                            if (current > place)
                            {
                                var numberOfPersons = Households[i].Household.Persons.Length;
                                // skip adding this particular household if it has too many persons
                                if (remaining < numberOfPersons)
                                {
                                    continue;
                                }
                                Households[i].ExpansionFactor -= 1;
                                var remainder = 0.0f;
                                if (Households[i].ExpansionFactor <= 0)
                                {
                                    remainder = -Households[i].ExpansionFactor;
                                    Households[i].ExpansionFactor = 0;
                                }
                                TotalExpansionFactor -= 1 - remainder;
                                remaining -= numberOfPersons;
                                if (_previouslyPicked == i)
                                {
                                    if (++_previouslyPickedTimes > 10)
                                    {
                                        Console.WriteLine("The same household is being picked too often, rebuilding expansion factors!");
                                        TotalExpansionFactor = Households.Sum(h =>
                                        {
                                            h.ResetExpansion();
                                            return h.ExpansionFactor;
                                        });
                                    }
                                }
                                else
                                {
                                    _previouslyPicked = i;
                                    _previouslyPickedTimes = 1;
                                }
                                return new KeyValuePair<int, int>(zone, i);
                            }
                        }
                    }
                    // if we get here then it failed, we need to reset the probabilities again
                    TotalExpansionFactor = Households.Sum(h =>
                    {
                        h.ResetExpansion();
                        return h.ExpansionFactor;
                    });
                }
                throw new XTMFRuntimeException(null, "We managed to be unable to assign any households to flat zone '" + zone + "' in PD'" + _pd + "'!");
            }
        }

        public void IterationStarting(int iteration)
        {
            // initialize data structures
            _householdsByPD = ZoneSystemHelper.CreatePdArray<PDData>(Root.ZoneSystem.ZoneArray);
            var flat = _householdsByPD.GetFlatData();
            for (int i = 0; i < flat.Length; i++)
            {
                flat[i] = new PDData(_householdsByPD.GetSparseIndex(i));
            }
        }

        public void Execute(ITashaHousehold household, int iteration)
        {
            var zone = household.HomeZone;
            if (zone != null)
            {
                var pd = zone.PlanningDistrict;
                var record = _householdsByPD[pd];
                if (record == null)
                {
                    throw new XTMFRuntimeException(this, "In '" + Name + "' we were unable to find a HouseholdByPD for PD#" + pd);
                }
                record.Add(household);
            }
        }

        public void IterationFinished(int iteration)
        {
            var flatPD = _householdsByPD.GetFlatData();
            var zoneArray = Root.ZoneSystem.ZoneArray;
            var zones = zoneArray.GetFlatData();
            List<KeyValuePair<int, int>>[] results = new List<KeyValuePair<int, int>>[flatPD.Length];
            Parallel.For(0, flatPD.Length, i =>
            {
                var pd = _householdsByPD.GetSparseIndex(i);
                var zoneIndexes = (from zone in zones
                                   where zone.PlanningDistrict == pd
                                   select zoneArray.GetFlatIndex(zone.ZoneNumber)).ToArray();
                // make sure we don't generate persons for the external zones
                results[i] = _householdsByPD.GetSparseIndex(i) == 0 ? [] : flatPD[i].ProcessPD(RandomSeed, zones, HouseholdExpansionFactor, zoneIndexes);
            });
            Save(results, flatPD);
        }

        [SubModelInformation(Required = true, Description = "The location to save the household file.")]
        public FileLocation HouseholdFile;

        [SubModelInformation(Required = true, Description = "The location to save the person file.")]
        public FileLocation PersonFile;

        [SubModelInformation(Required = false, Description = "Saves population/household totals.")]
        public FileLocation SummeryFile;

        [SubModelInformation(Required = true, Description = "The directory to store the worker force information.")]
        public FileLocation WorkerForceDirectory;

        [SubModelInformation(Required = true, Description = "The directory to store the worker category information to.")]
        public FileLocation WorkerCategoryDirectory;

        private void Save(List<KeyValuePair<int, int>>[] results, PDData[] pds)
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
            StringBuilder buildFileName = new();
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
            using StreamWriter writer = new(BuildFileName(occupation, empStat, WorkerForceDirectory));
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
            using StreamWriter writer = new(BuildFileName(occupation, empStat, WorkerCategoryDirectory));
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
            if (SummeryFile != null)
            {
                using var writer = new StreamWriter(SummeryFile);
                writer.WriteLine("Type,Total");
                writer.Write("Households,");
                writer.WriteLine((householdID - 1));
                writer.Write("Persons,");
                writer.WriteLine(totalPerson);
            }
        }

        private static int ClassifyHousehold(ITashaHousehold household)
        {
            var numberOfVehicles = household.Vehicles.Length;
            if (numberOfVehicles == 0)
            {
                return 0;
            }
            var lics = 0;
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

        private int SavePersons(List<KeyValuePair<int, int>>[] results, PDData[] pds, ref int totalPerson)
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
                    var households = pds[i].Households;
                    foreach (var record in results[i])
                    {
                        var zone = record.Key;
                        var persons = households[record.Value].Household.Persons;
                        var workerCategory = ClassifyHousehold(households[record.Value].Household);
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
                                case TTSEmploymentStatus.WorkAtHome_FullTime:
                                    if(WriteWorkAtHomeEmploymentStatus)
                                    {
                                        writer.Write("H,");
                                    }
                                    else
                                    {
                                        writer.Write("O,");
                                    }
                                    break;
                                case TTSEmploymentStatus.WorkAtHome_PartTime:
                                    if (WriteWorkAtHomeEmploymentStatus)
                                    {
                                        writer.Write("J,");
                                    }
                                    else
                                    {
                                        writer.Write("O,");
                                    }
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
                            var personExpanded = persons[j].ExpansionFactor * _invHouseholdExpansion;
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
            Parallel.Invoke(
                () => SaveWorkerData(zones, Occupation.Professional, TTSEmploymentStatus.FullTime, workersPF),
                () => SaveWorkerData(zones, Occupation.Office, TTSEmploymentStatus.FullTime, workersGF),
                () => SaveWorkerData(zones, Occupation.Retail, TTSEmploymentStatus.FullTime, workersSF),
                () => SaveWorkerData(zones, Occupation.Manufacturing, TTSEmploymentStatus.FullTime, workersMF),
                () => SaveWorkerData(zones, Occupation.Professional, TTSEmploymentStatus.PartTime, workersPP),
                () => SaveWorkerData(zones, Occupation.Office, TTSEmploymentStatus.PartTime, workersGP),
                () => SaveWorkerData(zones, Occupation.Retail, TTSEmploymentStatus.PartTime, workersSP),
                () => SaveWorkerData(zones, Occupation.Manufacturing, TTSEmploymentStatus.PartTime, workersMP),

                () => SaveWorkerCategoryData(zones, Occupation.Professional, TTSEmploymentStatus.FullTime, workersCatPF),
                () => SaveWorkerCategoryData(zones, Occupation.Office, TTSEmploymentStatus.FullTime, workersCatGF),
                () => SaveWorkerCategoryData(zones, Occupation.Retail, TTSEmploymentStatus.FullTime, workersCatSF),
                () => SaveWorkerCategoryData(zones, Occupation.Manufacturing, TTSEmploymentStatus.FullTime, workersCatMF),
                () => SaveWorkerCategoryData(zones, Occupation.Professional, TTSEmploymentStatus.PartTime, workersCatPP),
                () => SaveWorkerCategoryData(zones, Occupation.Office, TTSEmploymentStatus.PartTime, workersCatGP),
                () => SaveWorkerCategoryData(zones, Occupation.Retail, TTSEmploymentStatus.PartTime, workersCatSP),
                () => SaveWorkerCategoryData(zones, Occupation.Manufacturing, TTSEmploymentStatus.PartTime, workersCatMP)
            );
            return householdID;
        }

        private bool IsExternal(IZone employmentZone)
        {
            return employmentZone != null && ExternalZones.Contains(employmentZone.ZoneNumber);
        }

        private void SaveHouseholds(List<KeyValuePair<int, int>>[] results, PDData[] pds, int[] zones)
        {
            int householdID = 1;
            using var writer = new StreamWriter(HouseholdFile);
            writer.WriteLine("HouseholdID,Zone,ExpansionFactor,DwellingType,NumberOfPersons,NumberOfVehicles,IncomeLevel");
            for (int i = 0; i < results.Length; i++)
            {
                var households = pds[i].Households;
                foreach (var record in results[i])
                {
                    var zone = record.Key;
                    var household = households[record.Value].Household;
                    writer.Write(householdID);
                    writer.Write(',');
                    writer.Write(zones[zone]);
                    writer.Write(",");
                    writer.Write(_invHouseholdExpansion);
                    writer.Write(",");
                    writer.Write((int)household.DwellingType);
                    writer.Write(',');
                    writer.Write(household.Persons.Length);
                    writer.Write(',');
                    writer.Write(household.Vehicles.Length);
                    writer.Write(',');
                    writer.WriteLine(household.IncomeClass);
                    householdID++;
                }
            }
        }

        public void Load(int maxIterations)
        {

        }

        public bool RuntimeValidation(ref string error)
        {
            _invHouseholdExpansion = 1.0f / HouseholdExpansionFactor;
            return true;
        }
    }
}
