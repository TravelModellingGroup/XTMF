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
using TMG.Input;
using XTMF;

namespace TMG.GTAModel
{
    [ModuleInformation(Description =
        @"This model system template is designed to validate the generation of a synthetic population 
against the percentage based data from the provided IDemographicsData module."
        )]
    public class PopulationValidation : ITravelDemandModel
    {
        [RunParameter("Age Zone Report", "AgeZoneReport.csv", "The location that the age report will be saved to.")]
        public string AgeReportFile;

        [SubModelInformation(Description = "The demographic information to compare to the synthetic population", Required = true)]
        public IDemographicsData Demographics;

        [RunParameter(" Drivers License Report", "DriversLicenseReport.csv", typeof(FileFromOutputDirectory), "The location that the driver's license report will be saved to.")]
        public FileFromOutputDirectory DriversLicenseReportFile;

        [RunParameter("Employment Status Zone Report", "EmploymentStatusZoneReport.csv", "The location that the employment report will be saved to.")]
        public string EmploymentStatusReportFile;

        [RunParameter("Occupation Zone Report", "OccupationZoneReport.csv", "The location that the occupation report will be saved to.")]
        public string OccupationReportFile;

        [SubModelInformation(Description = "The population type to use for reading in the synthetic population", Required = true)]
        public IPopulation Population;

        [RunParameter("Synthetic Population File", "SyntheticPopulation.csv", "The location and name of the file that contains the synthetic population")]
        public string SyntheticPopulationFile;

        [RunParameter("Unemployment Status", 0, "The number that co-responds with a person not being employed")]
        public int UnemployedEmploymentStatus;

        private static Tuple<byte, byte, byte> LoadingColour = new Tuple<byte, byte, byte>(100, 200, 100);

        private static Tuple<byte, byte, byte> ProcessingColour = new Tuple<byte, byte, byte>(50, 150, 250);

        [RunParameter("Input Directory", "../../Input", "The directory that our input is located in.")]
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
            get { return Population.Progress < 1 ? LoadingColour : ProcessingColour; }
        }

        [SubModelInformation(Description = "The zone system for the synthetic population", Required = true)]
        public IZoneSystem ZoneSystem
        {
            get;
            set;
        }

        public bool ExitRequest()
        {
            return false;
        }

        public bool RuntimeValidation(ref string error)
        {
            if (!File.Exists(SyntheticPopulationFile))
            {
                error = String.Format("The synthetic population file \"{0}\" doesn't exist!", SyntheticPopulationFile);
                return false;
            }
            return true;
        }

        public void Start()
        {
            ZoneSystem.LoadData();
            Demographics.LoadData();

            using (StreamWriter writer = new StreamWriter("Performance.txt"))
            {
                Stopwatch watch = new Stopwatch();
                watch.Start();
                Population.Load();
                watch.Stop();
                writer.WriteLine("Population Loading: " + watch.ElapsedMilliseconds + "ms");
                watch.Restart();
                ValidatePopulation();
                watch.Stop();
                writer.WriteLine("Processing Time: " + watch.ElapsedMilliseconds + "ms");
            }
            Population.Population = null;
            Demographics.UnloadData();
            ZoneSystem.UnloadData();
        }

        public void ValidateEmploymentStatus()
        {
            var zoneArray = ZoneSystem.ZoneArray;
            var validZones = zoneArray.ValidIndexies().ToArray();
            var employmentStatusDist = zoneArray.CreateSimilarArray<SparseTwinIndex<float>>();
            var validEmploymentStatus = Demographics.EmploymentStatus.ValidIndexies().ToArray();
            var numberEmploymentStatus = validEmploymentStatus.Length;
            var numberOfZones = validZones.Length;
            var validAges = Demographics.AgeCategories.ValidIndexies().ToArray();
            var agesLength = validAges.Length;
            //for(int i = 0; i < numberOfZones; i++)
            Parallel.For(0, numberOfZones,
               delegate (int i)
               {
                   var zoneNumber = validZones[i];
                   var pop = Population.Population[zoneNumber];
                   var popLength = pop.Length;
                   var dist = SparseTwinIndex<float>.CreateSimilarArray(Demographics.AgeCategories, Demographics.EmploymentStatus);
                   var ages = Demographics.AgeCategories;

                   for (int p = 0; p < popLength; p++)
                   {
                       var person = pop[p];
                       var personAge = person.Age;
                       for (int a = 0; a < agesLength; a++)
                       {
                           var range = ages[validAges[a]];
                           if (personAge >= range.Start && personAge <= range.Stop)
                           {
                               dist[validAges[a], person.EmploymentStatus] += person.ExpansionFactor;
                               break;
                           }
                       }
                   }
                   employmentStatusDist[zoneNumber] = dist;
               });
            using (StreamWriter writer = new StreamWriter(EmploymentStatusReportFile))
            {
                writer.Write("Zone,AgeCat");
                foreach (var emp in Demographics.EmploymentStatus.ValidIndexies())
                {
                    writer.Write(',');
                    writer.Write(Demographics.EmploymentStatus[emp]);
                }
                writer.WriteLine("Population");
                foreach (var zone in zoneArray.ValidIndexies())
                {
                    var data = employmentStatusDist[zone];
                    if (zoneArray[zone].Population <= 0) continue;
                    for (int ageCat = 0; ageCat < agesLength; ageCat++)
                    {
                        writer.Write(zone);
                        writer.Write(',');
                        writer.Write(validAges[ageCat]);
                        var total = 0f;
                        var population = zoneArray[zone].Population * Demographics.AgeRates[zone, validAges[ageCat]];
                        for (int e = 0; e < numberEmploymentStatus; e++)
                        {
                            total += data[validAges[ageCat], validEmploymentStatus[e]];
                        }

                        for (int e = 0; e < numberEmploymentStatus; e++)
                        {
                            var res = data[validAges[ageCat], validEmploymentStatus[e]] / total;
                            if (float.IsNaN(res))
                            {
                                res = 0;
                            }
                            res -= Demographics.EmploymentStatusRates[zone][validAges[ageCat], validEmploymentStatus[e]];
                            writer.Write(',');
                            writer.Write(res * population);
                        }
                        writer.Write(',');
                        writer.WriteLine(population);
                    }
                }
            }
        }

        private static void CalculateExpectedDLic(IZone[] zones, float[] expectedNumberOfLicenses, SparseTwinIndex<float>[] licenseData, float[][] ageData, Range[] ageCat, string[] empStatus, SparseTwinIndex<float>[] empData, int i)
        {
            var zoneLicenseData = licenseData[i];
            if (zoneLicenseData == null)
            {
                return;
            }
            var expectedTotal = 0.0f;
            var pop = zones[i].Population;
            var zoneLicenseProbability = licenseData[i].GetFlatData();
            var empProb = empData[i].GetFlatData();
            for (int a = 0; a < ageCat.Length; a++)
            {
                var inAge = ageData[i][a];
                for (int e = 0; e < empStatus.Length; e++)
                {
                    // add the probability of having the license by the ammount of people in that category
                    expectedTotal += zoneLicenseProbability[a][e] * inAge * empProb[a][e] * pop;
                }
            }
            expectedNumberOfLicenses[i] = expectedTotal;
        }

        private void GatherDLicData(IZone[] zones, float[] expectedNumberOfLicenses, float[] numberOfLicenses)
        {
            var licenseData = Demographics.DriversLicenseRates.GetFlatData();
            var ageData = Demographics.AgeRates.GetFlatData();
            var ageCat = Demographics.AgeCategories.GetFlatData();
            var empStatus = Demographics.EmploymentStatus.GetFlatData();
            var empData = Demographics.EmploymentStatusRates.GetFlatData();
            Parallel.For(0, zones.Length, delegate (int i)
           {
               CalculateExpectedDLic(zones, expectedNumberOfLicenses, licenseData, ageData, ageCat, empStatus, empData, i);
                    // learn how many we generated
                    var people = Population.Population.GetFlatData()[i];
               if (people == null) return;
               var generatedTotal = 0f;
               for (int p = 0; p < people.Length; p++)
               {
                   if (people[p].DriversLicense)
                   {
                       generatedTotal += people[p].ExpansionFactor;
                   }
               }
               numberOfLicenses[i] = generatedTotal;
           });
        }

        private void ValidateAgeRates()
        {
            var zoneArray = ZoneSystem.ZoneArray;
            var ageDistribution = zoneArray.CreateSimilarArray<SparseArray<float>>();
            var validZones = zoneArray.ValidIndexies().ToArray();
            var numberOfZones = validZones.Length;
            var validAges = Demographics.AgeCategories.ValidIndexies().ToArray();
            var agesLength = validAges.Length;
            //for(int i = 0; i < numberOfZones; i++)
            Parallel.For(0, numberOfZones,
               delegate (int i)
               {
                   var zoneNumber = validZones[i];
                   var pop = Population.Population[zoneNumber];
                   var popLength = pop.Length;
                   var dist = Demographics.AgeCategories.CreateSimilarArray<float>();
                   var ages = Demographics.AgeCategories;

                   for (int p = 0; p < popLength; p++)
                   {
                       var person = pop[p];
                       var personAge = person.Age;
                       for (int a = 0; a < agesLength; a++)
                       {
                           var range = ages[validAges[a]];
                           if (personAge >= range.Start && personAge <= range.Stop)
                           {
                               dist[validAges[a]] += person.ExpansionFactor;
                               break;
                           }
                       }
                   }
                   ageDistribution[zoneNumber] = dist;
               });
            using (StreamWriter writer = new StreamWriter(AgeReportFile))
            {
                writer.Write("Zone");
                foreach (var ageCat in Demographics.AgeCategories.ValidIndexies())
                {
                    writer.Write(',');
                    writer.Write(Demographics.AgeCategories[ageCat]);
                }
                writer.WriteLine(",Synthetic Population, Given Population");
                foreach (var zone in zoneArray.ValidIndexies())
                {
                    var total = 0f;
                    var population = zoneArray[zone].Population;
                    if (population <= 0) continue;
                    var zoneDist = ageDistribution[zone];
                    for (int a = 0; a < agesLength; a++)
                    {
                        total += zoneDist[validAges[a]];
                    }
                    writer.Write(zone);
                    for (int a = 0; a < agesLength; a++)
                    {
                        writer.Write(',');
                        writer.Write((zoneDist[validAges[a]]) - Demographics.AgeRates[zone, validAges[a]] * population);
                    }
                    writer.Write(',');
                    writer.Write(total);
                    writer.Write(',');
                    writer.WriteLine(zoneArray[zone].Population);
                }
            }
        }

        private void ValidateDriversLicense()
        {
            if (!DriversLicenseReportFile.ContainsFileName()) return;
            var zones = ZoneSystem.ZoneArray.GetFlatData();
            var numberOfLicenses = new float[zones.Length];
            var expectedNumberOfLicenses = new float[zones.Length];
            GatherDLicData(zones, expectedNumberOfLicenses, numberOfLicenses);
            using (StreamWriter writer = new StreamWriter(DriversLicenseReportFile.GetFileName()))
            {
                writer.WriteLine("Zones,ExpectedDLic,GeneratedDLic,Delta");
                for (int i = 0; i < zones.Length; i++)
                {
                    writer.Write(zones[i].ZoneNumber);
                    writer.Write(',');
                    writer.Write(expectedNumberOfLicenses[i]);
                    writer.Write(',');
                    writer.Write(numberOfLicenses[i]);
                    writer.Write(',');
                    writer.Write(expectedNumberOfLicenses[i] - numberOfLicenses[i]);
                    writer.WriteLine();
                }
            }
        }

        private void ValidateOccupations()
        {
            var zoneArray = ZoneSystem.ZoneArray;
            var occZoneDist = Demographics.OccupationRates.CreateSimilarArray<SparseTriIndex<float>>();
            var validOccupations = Demographics.OccupationCategories.ValidIndexies().ToArray();
            var occupationsLength = validOccupations.Length;
            var validZones = zoneArray.ValidIndexies().ToArray();
            var numberOfZones = validZones.Length;
            var validAges = Demographics.AgeCategories.ValidIndexies().ToArray();
            var agesLength = validAges.Length;
            Parallel.For(0, numberOfZones,
               delegate (int i)
               {
                   var zoneNumber = validZones[i];
                   var pop = Population.Population[zoneNumber];
                   var popLength = pop.Length;
                   var dist = SparseTriIndex<float>.CreateSimilarArray(Demographics.AgeCategories, Demographics.EmploymentStatus,
                       Demographics.OccupationCategories);
                   var ages = Demographics.AgeCategories;

                   for (int p = 0; p < popLength; p++)
                   {
                       var person = pop[p];
                       if (person.EmploymentStatus != UnemployedEmploymentStatus)
                       {
                           var personAge = person.Age;
                           for (int a = 0; a < agesLength; a++)
                           {
                               var range = ages[validAges[a]];
                               if (personAge >= range.Start && personAge <= range.Stop)
                               {
                                   dist[validAges[a], person.EmploymentStatus, person.Occupation] += person.ExpansionFactor;
                                   break;
                               }
                           }
                       }
                   }
                   occZoneDist[zoneNumber] = dist;
               });
            using (StreamWriter writer = new StreamWriter(OccupationReportFile))
            {
                writer.Write("Zone,Age Category,Employment Status,");
                foreach (var ageCat in Demographics.OccupationCategories.ValidIndexies())
                {
                    writer.Write(Demographics.OccupationCategories[ageCat]);
                    writer.Write(',');
                }
                writer.WriteLine("Population");
                foreach (var employmentStatus in Demographics.EmploymentStatus.ValidIndexies())
                {
                    if (employmentStatus == UnemployedEmploymentStatus) continue;
                    foreach (var zone in zoneArray.ValidIndexies())
                    {
                        var baseData = Demographics.OccupationRates[zone];
                        if (zoneArray[zone].Population <= 0) continue;
                        var zoneDist = occZoneDist[zone];
                        for (int a = 0; a < agesLength; a++)
                        {
                            var population = zoneArray[zone].Population * Demographics.AgeRates[zone, a] *
                                Demographics.EmploymentStatusRates[zone][a, employmentStatus];
                            var total = 0f;
                            for (int i = 0; i < occupationsLength; i++)
                            {
                                total += zoneDist[validAges[a], employmentStatus, validOccupations[i]];
                            }

                            for (int o = 0; o < occupationsLength; o++)
                            {
                                float result = (zoneDist[validAges[a], employmentStatus, validOccupations[o]] / total);
                                if (float.IsNaN(result))
                                {
                                    result = 0;
                                }
                                result -= baseData[validAges[a], employmentStatus, validOccupations[o]];
                                if (o == 0)
                                {
                                    writer.Write(zone);
                                    writer.Write(',');
                                    writer.Write(validAges[a]);
                                    writer.Write(',');
                                    writer.Write(employmentStatus);
                                    writer.Write(',');
                                    writer.Write(result * population);
                                }
                                else
                                {
                                    writer.Write(',');
                                    writer.Write(result * population);
                                }
                            }
                            writer.Write(',');
                            writer.WriteLine(population);
                        }
                    }
                }
            }
        }

        private void ValidatePopulation()
        {
            ValidateAgeRates();
            ValidateEmploymentStatus();
            ValidateOccupations();
            ValidateDriversLicense();
        }
    }
}