/*
    Copyright 2014 James Vaughan for integration into XTMF.

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
using Tasha.Common;
using XTMF;
using Datastructure;
using TMG;
using TMG.Input;
using System.IO;

namespace Tasha.PopulationSynthesis
{

    public sealed class UpdatePopulationRatesByZone : IPostHousehold, IDisposable
    {

        [RootModule]
        public ITashaRuntime Root;

        public string Name { get; set; }

        public float Progress { get; set; }

        [RunParameter("External Zone Ranges", "6000-6999", typeof(RangeSet), "The ranges that represent external zones.")]
        public RangeSet ExternalZones;

        private bool IsExternal(IZone employmentZone)
        {
            return employmentZone != null && ExternalZones.Contains(employmentZone.ZoneNumber);
        }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private float[] ExpansionModiferByZone;
        private float[] WorkersPF;
        private float[] WorkersGF;
        private float[] WorkersSF;
        private float[] WorkersMF;
        private float[] WorkersPP;
        private float[] WorkersGP;
        private float[] WorkersSP;
        private float[] WorkersMP;


        private StreamWriter Writer;

        [SubModelInformation(Required = true, Description = "The directory to store the worker force information.")]
        public FileLocation WorkerForceDirectory;

        public void Execute(ITashaHousehold household, int iteration)
        {
            int householdZoneNumber = household.HomeZone.ZoneNumber;
            int flatZone = Root.ZoneSystem.ZoneArray.GetFlatIndex(householdZoneNumber);
            var expansionMultiplier = ExpansionModiferByZone[flatZone];
            float newExpansionFactor = expansionMultiplier * household.ExpansionFactor;
            Writer.Write(household.HouseholdId);
            Writer.Write(',');
            Writer.Write(householdZoneNumber);
            Writer.Write(',');
            Writer.Write(newExpansionFactor);
            Writer.Write(',');
            Writer.Write('0');
            Writer.Write(',');
            Writer.Write(household.Persons.Length);
            Writer.Write(',');
            Writer.WriteLine(household.Vehicles.Length);
            foreach(var person in household.Persons)
            {
                if((var employmentZone = person.EmploymentZone) == null || !IsExternal(employmentZone))
                {
                    switch(person.EmploymentStatus)
                    {
                        case TTSEmploymentStatus.FullTime:
                            switch(person.Occupation)
                            {
                                case Occupation.Professional:
                                    WorkersPF[flatZone] += newExpansionFactor;
                                    break;
                                case Occupation.Office:
                                    WorkersGF[flatZone] += newExpansionFactor;
                                    break;
                                case Occupation.Retail:
                                    WorkersSF[flatZone] += newExpansionFactor;
                                    break;
                                case Occupation.Manufacturing:
                                    WorkersMF[flatZone] += newExpansionFactor;
                                    break;
                            }
                            break;
                        case TTSEmploymentStatus.PartTime:
                            switch(person.Occupation)
                            {
                                case Occupation.Professional:
                                    WorkersPP[flatZone] += newExpansionFactor;
                                    break;
                                case Occupation.Office:
                                    WorkersGP[flatZone] += newExpansionFactor;
                                    break;
                                case Occupation.Retail:
                                    WorkersSP[flatZone] += newExpansionFactor;
                                    break;
                                case Occupation.Manufacturing:
                                    WorkersMP[flatZone] += newExpansionFactor;
                                    break;
                            }
                            break;
                    }
                }
            }
        }

        private string BuildFileName(Occupation occ, TTSEmploymentStatus empStat)
        {
            var dirPath = WorkerForceDirectory.GetFilePath();
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
            using (StreamWriter writer = new StreamWriter(BuildFileName(occupation, empStat)))
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

        public void IterationFinished(int iteration)
        {
            Writer.Close();
            Writer = null;
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            SaveWorkerData(zones, Occupation.Professional, TTSEmploymentStatus.FullTime, WorkersPF);
            SaveWorkerData(zones, Occupation.Office, TTSEmploymentStatus.FullTime, WorkersGF);
            SaveWorkerData(zones, Occupation.Retail, TTSEmploymentStatus.FullTime, WorkersSF);
            SaveWorkerData(zones, Occupation.Manufacturing, TTSEmploymentStatus.FullTime, WorkersMF);
            SaveWorkerData(zones, Occupation.Professional, TTSEmploymentStatus.PartTime, WorkersPP);
            SaveWorkerData(zones, Occupation.Office, TTSEmploymentStatus.PartTime, WorkersGP);
            SaveWorkerData(zones, Occupation.Retail, TTSEmploymentStatus.PartTime, WorkersSP);
            SaveWorkerData(zones, Occupation.Manufacturing, TTSEmploymentStatus.PartTime, WorkersMP);
        }

        [SubModelInformation(Required = true, Description = "CSV (Zone,FutureYearPopulation)")]
        public FileLocation FutureYearPopulationByZone;

        [SubModelInformation(Required = true, Description = "The location to save the updated Household's file.")]
        public FileLocation NewHouseholdFile;

        public void IterationStarting(int iteration)
        {
            var zoneSystem = Root.ZoneSystem.ZoneArray;
            var zones = zoneSystem.GetFlatData();
            ExpansionModiferByZone = new float[zones.Length];
            WorkersPF = new float[zones.Length];
            WorkersGF = new float[zones.Length];
            WorkersSF = new float[zones.Length];
            WorkersMF = new float[zones.Length];
            WorkersPP = new float[zones.Length];
            WorkersGP = new float[zones.Length];
            WorkersSP = new float[zones.Length];
            WorkersMP = new float[zones.Length];
            // Create our writer
            Writer = new StreamWriter(NewHouseholdFile);
            //HouseholdID	Zone	ExpansionFactor	DwellingType	NumberOfPersons	NumberOfVehicles
            Writer.WriteLine("HouseholdID,Zone,ExpansionFactor,DwellingType,NumberOfPersons,NumberOfVehicles");
            // update the population forecasts by zone
            using (CsvReader reader = new CsvReader(FutureYearPopulationByZone))
            {
                reader.LoadLine();
                while(reader.LoadLine(out int columns))
                {
                    if(columns >= 2)
                    {
                        reader.Get(out int sparseZone, 0);
                        int zone = zoneSystem.GetFlatIndex(sparseZone);
                        if(zone >= 0)
                        {
                            reader.Get(out float futurePopulation, 1);
                            ExpansionModiferByZone[zone] = futurePopulation / (float)zones[zone].Population;
                            if(zones[zone].Population > 0)
                            {
                                if(float.IsNaN(ExpansionModiferByZone[zone]) | float.IsInfinity(ExpansionModiferByZone[zone]))
                                {
                                    throw new XTMFRuntimeException("Zone " + sparseZone.ToString() + " ended up with an invalid (infinite) population in the future year forecast!");
                                }
                            }
                        }
                    }
                }
            }
        }

        public void Load(int maxIterations)
        {

        }

        public bool RuntimeValidation(ref string error)
        {
            if(Root.Parallel)
            {
                error = "Parallel must be off in order to do a population update!";
                return false;
            }
            return true;
        }

        public void Dispose()
        {
            if(Writer != null)
            {
                Writer.Dispose();
                Writer = null;
            }
        }
    }

}
