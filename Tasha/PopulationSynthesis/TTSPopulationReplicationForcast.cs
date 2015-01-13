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

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        internal class ExpandedHousehold(internal ITashaHousehold Household)
        {
            internal float ExpansionFactor = Household.ExpansionFactor;

            internal void ResetExpansion()
            {
                ExpansionFactor = Household.ExpansionFactor;
            }
        }

        internal class PDData
        {
            private int PD;
            internal List<ExpandedHousehold> Households = new List<ExpandedHousehold>(10);
            internal float TotalExpansionFactor;
            private SpinLock Lock = new SpinLock(false);

            public PDData(int pd)
            {
                PD = pd;
            }

            internal void Add(ITashaHousehold household)
            {
                var expansionFactor = household.ExpansionFactor;
                Lock.Enter(ref (bool taken = false));
                TotalExpansionFactor += expansionFactor;
                Households.Add(new ExpandedHousehold(household));
                if(taken) Lock.Exit(true);
            }

            internal List<KeyValuePair<int, int>> ProcessPD(int randomSeed, IZone[] zones, int[] zoneIndexes)
            {
                bool any;
                Random random = new Random(randomSeed * PD);
                var ret = new List<KeyValuePair<int, int>>();
                var remaining = zoneIndexes.Select((z) => zones[z].Population).ToArray();
                TotalExpansionFactor = Households.Sum(h => h.ExpansionFactor);
                do
                {
                    any = false;
                    for(int zone = 0; zone < zoneIndexes.Length; zone++)
                    {
                        if(remaining[zone] > 0)
                        {
                            any = true;
                            ret.Add(Pick(random, zoneIndexes[zone], ref remaining[zone]));
                        }
                    }
                } while(any);
                return ret;
            }

            private KeyValuePair<int, int> Pick(Random random, int zone, ref int remaining)
            {
                for(int unused = 0; unused < 2; unused++)
                {
                    var place = (float)random.NextDouble() * TotalExpansionFactor;
                    float current = 0.0f;
                    for(int i = 0; i < Households.Count; i++)
                    {
                        current += Households[i].ExpansionFactor;
                        if(current > place)
                        {
                            var numberOfPersons = Households[i].Household.Persons.Length;
                            // skip adding this particular household if it has too many persons
                            if(remaining < numberOfPersons)
                            {
                                continue;
                            }
                            Households[i].ExpansionFactor -= 1;
                            var remainder = 0.0f;
                            if(Households[i].ExpansionFactor <= 0)
                            {
                                remainder = -Households[i].ExpansionFactor;
                                Households[i].ExpansionFactor = 0;
                            }
                            TotalExpansionFactor -= 1 - remainder;
                            remaining -= numberOfPersons;
                            return new KeyValuePair<int, int>(zone, i);
                        }
                    }
                    // if we get here then it failed, we need to reset the probabilities again
                    TotalExpansionFactor = Households.Sum(h =>
                    {
                        h.ResetExpansion();
                        return h.ExpansionFactor;
                    });
                }
                throw new XTMFRuntimeException("We managed to be unable to assign any households to flat zone '" + zone.ToString() + "' in PD'" + PD.ToString() + "'!");
            }
        }

        SparseArray<PDData> HouseholdsByPD;

        public void IterationStarting(int iteration)
        {
            // initialize data structures
            HouseholdsByPD = ZoneSystemHelper.CreatePDArray<PDData>(Root.ZoneSystem.ZoneArray);
            var flat = HouseholdsByPD.GetFlatData();
            for(int i = 0; i < flat.Length; i++)
            {
                flat[i] = new PDData(HouseholdsByPD.GetSparseIndex(i));
            }
        }

        public void Execute(ITashaHousehold household, int iteration)
        {
            var zone = household.HomeZone;
            if(zone != null)
            {
                var pd = zone.PlanningDistrict;
                var record = HouseholdsByPD[pd];
                if(record == null)
                {
                    throw new XTMFRuntimeException("In '" + Name + "' we were unable to find a HouseholdByPD for PD#" + pd.ToString());
                }
                record.Add(household);
            }
        }

        public void IterationFinished(int iteration)
        {
            var flatPD = HouseholdsByPD.GetFlatData();
            var zoneArray = Root.ZoneSystem.ZoneArray;
            var zones = zoneArray.GetFlatData();
            List<KeyValuePair<int, int>>[] results = new List<KeyValuePair<int, int>>[flatPD.Length];
            Parallel.For(0, flatPD.Length, (int i) =>
            {
                var pd = HouseholdsByPD.GetSparseIndex(i);
                var zoneIndexes = (from zone in zones
                                   where zone.PlanningDistrict == pd
                                   select zoneArray.GetFlatIndex(zone.ZoneNumber)).ToArray();
                // make sure we don't generate persons for the external zones
                results[i] = HouseholdsByPD.GetSparseIndex(i) == 0 ? new List<KeyValuePair<int, int>>() : flatPD[i].ProcessPD(RandomSeed, zones, zoneIndexes);
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

        [SubModelInformation(Required = false, Description = "The location to save the employment information for PoRPoW.")]
        public FileLocation OccupationDataDirectory;

        private void Save(List<KeyValuePair<int, int>>[] results, PDData[] pds)
        {
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData().Select(z => z.ZoneNumber).ToArray();
            int totalPerson = 0;
            var householdID = SaveHouseholds(results, pds, zones);
            householdID = SavePersons(results, pds, ref totalPerson);
            SaveSummeryFile(totalPerson, householdID);
        }

        private string BuildFileName(Occupation occ, TTSEmploymentStatus empStat, FileLocation fileLocation)
        {
            var dirPath = fileLocation.GetFilePath();
            var info = new DirectoryInfo(dirPath);
            if(!info.Exists)
            {
                info.Create();
            }
            StringBuilder buildFileName = new StringBuilder();
            switch(occ)
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
            switch(empStat)
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
                for(int i = 0; i < workers.Length; i++)
                {
                    if(workers[i] > 0)
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
                for(int i = 0; i < workers.Length; i++)
                {
                    var factor = 1.0f / workers[i].Sum();
                    if(float.IsNaN(factor))
                    {
                        continue;
                    }
                    for(int cat = 0; cat < workers[i].Length; cat++)
                    {
                        workers[i][cat] *= factor;
                    }
                    for(int cat = 0; cat < workers[i].Length; cat++)
                    {
                        if(workers[i][cat] > 0)
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
            if(SummeryFile != null)
            {
                using (var writer = new StreamWriter(SummeryFile))
                {
                    writer.WriteLine("Type,Total");
                    writer.Write("Households,");
                    writer.WriteLine((householdID - 1));
                    writer.Write("Persons,");
                    writer.WriteLine(totalPerson);
                }
            }
        }

        private static int ClassifyHousehold(ITashaHousehold household)
        {
            var lics = 0;
            var numberOfVehicles = household.Vehicles.Length;
            if(numberOfVehicles == 0)
            {
                return 0;
            }
            var persons = household.Persons;
            for(int i = 0; i < persons.Length; i++)
            {
                if(persons[i].Licence)
                {
                    lics++;
                }
            }
            if(lics == 0) return 0;
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
            for(int i = 0; i < zones.Length; i++)
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
                writer.WriteLine("HouseholdID,PersonNumber,Age,Sex,License,TransitPass,EmploymentStatus,Occupation,FreeParking,StudentStatus,EmploymentZone,SchoolZone");
                for(int i = 0; i < results.Length; i++)
                {
                    var households = pds[i].Households;
                    foreach(var record in results[i])
                    {
                        var zone = record.Key;
                        var persons = households[record.Value].Household.Persons;
                        var workerCategory = ClassifyHousehold(households[record.Value].Household);
                        for(int j = 0; j < persons.Length; j++)
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
                            switch(persons[j].TransitPass)
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
                            switch(persons[j].EmploymentStatus)
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
                            switch(persons[j].Occupation)
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
                            if(!IsExternal(workZone))
                            {
                                switch(persons[j].EmploymentStatus)
                                {
                                    case TTSEmploymentStatus.FullTime:
                                        switch(persons[j].Occupation)
                                        {
                                            case Occupation.Professional:
                                                workersPF[zone] += 1;
                                                workersCatPF[zone][workerCategory] += 1;
                                                break;
                                            case Occupation.Office:
                                                workersGF[zone] += 1;
                                                workersCatGF[zone][workerCategory] += 1;
                                                break;
                                            case Occupation.Retail:
                                                workersSF[zone] += 1;
                                                workersCatSF[zone][workerCategory] += 1;
                                                break;
                                            case Occupation.Manufacturing:
                                                workersMF[zone] += 1;
                                                workersCatMF[zone][workerCategory] += 1;
                                                break;
                                        }
                                        break;
                                    case TTSEmploymentStatus.PartTime:
                                        switch(persons[j].Occupation)
                                        {
                                            case Occupation.Professional:
                                                workersPP[zone] += 1;
                                                workersCatPP[zone][workerCategory] += 1;
                                                break;
                                            case Occupation.Office:
                                                workersGP[zone] += 1;
                                                workersCatGP[zone][workerCategory] += 1;
                                                break;
                                            case Occupation.Retail:
                                                workersSP[zone] += 1;
                                                workersCatSP[zone][workerCategory] += 1;
                                                break;
                                            case Occupation.Manufacturing:
                                                workersMP[zone] += 1;
                                                workersCatMP[zone][workerCategory] += 1;
                                                break;
                                        }
                                        break;
                                }
                            }
                            writer.Write(persons[j].FreeParking ? "Y," : "N,");
                            switch(persons[j].StudentStatus)
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
                            if(IsExternal(workZone))
                            {
                                writer.Write(workZone.ZoneNumber);
                            }
                            else
                            {
                                writer.Write('0');
                            }
                            writer.Write(',');
                            if(IsExternal(schoolZone))
                            {
                                writer.Write(schoolZone.ZoneNumber);
                            }
                            else
                            {
                                writer.Write('0');
                            }
                            writer.WriteLine();
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

        private int SaveHouseholds(List<KeyValuePair<int, int>>[] results, PDData[] pds, int[] zones)
        {
            int householdID = 1;
            using (var writer = new StreamWriter(HouseholdFile))
            {
                writer.WriteLine("HouseholdID,Zone,ExpansionFactor,DwellingType,NumberOfPersons,NumberOfVehicles");
                for(int i = 0; i < results.Length; i++)
                {
                    var households = pds[i].Households;
                    foreach(var record in results[i])
                    {
                        var zone = record.Key;
                        var household = households[record.Value].Household;
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

        public void Load(int maxIterations)
        {

        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }


    }

}
