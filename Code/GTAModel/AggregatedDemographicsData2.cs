/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Threading.Tasks;
using Datastructure;
using TMG.Input;
using XTMF;
using Range = Datastructure.Range;

// ReSharper disable UnassignedField.Global

namespace TMG.GTAModel
{
    [ModuleInformation(Description = @"Copy of 'Aggreagted Demographics Data'. This is module is a quick-and-dirty demographics module - a generic one will be implemented
                    in the near future. Currently as a result of constraints in the code for this module, <b>employment categories</b> and <b>employment status categories</b>
                    are hard-coded. All file formats remain the same with one exception: all files are assumed to have a single header line with column labels,
                    and lines beginning with '//' <em>will be skipped</em>.")]
    public class AggregatedDemographicsData2 : IDemographicsData
    {
        [Parameter("Age Categories", "0:0-9,1:10-13,2:14-15,3:16-17,4:18-25,5:26-30,6:31-55,7:56-65,8:66-100", "The age categories used defined in the RANGE format X:Start-EndInclusive.")]
        public string AgeCategoryString;

        [RunParameter("Age Distribution", "AgePD.csv", typeof(FileFromInputDirectory), "File describing age distribution in each planning district (PD), normalized to each PD. Age categories must " +
                "match those described in this module's 'Age Categories' parameter. Expected file format is TABLE: PD, [AGE_CAT 0], [AGE_CAT 1], ...")]
        public FileFromInputDirectory AgeDistributionFile;

        [RunParameter("Driver's License Rate", "DriversLicense.csv", typeof(FileFromInputDirectory), "File describing rate of driver's license ownership by employment status by age for each plan" +
                "ning district (PD). Expected file type is MIT: PD, EMP_STAT, [value]")]
        public FileFromInputDirectory DriversLicenseRateFile;

        [RunParameter("Employment Distribution", "EmploymentPDAge.csv", typeof(FileFromInputDirectory), "File describing age distribution by employment status type for each planning district (PD), " +
                "such that the sum across all employment statuses for each age for each PD = 1.0. Age categories and employment status types must match those described in this module's " +
                "'Age Categories' and 'Employment State Categories' parameters. Expected file type is MIT: PD, EMP_STAT, AGE, [value] ")]
        public FileFromInputDirectory EmploymentDistributionFile;

        [RunParameter("Job Employment Distribution", "JobEmpStat.csv", typeof(FileFromInputDirectory), "File describing employment status distribution for each PD such that the sum over " +
                "all employment statuses = 1.0. Expected file format is TABLE: PD, [EMP_STAT 0], [EMP_STAT 1], ...")]
        public FileFromInputDirectory JobEmploymentRateFile;

        [RunParameter("Job Occupation Distribution", "JobOccupation.csv", typeof(FileFromInputDirectory), "File describing occupation distribution by employment status for each planning district" +
                " (PD) such that the sum across all occupation categories for each employment status for each PD = 1.0. Expected file type is MIT: PD, EMP_STAT, OCC, [value]")]
        public FileFromInputDirectory JobOccupationRateFile;

        [RunParameter("#Vehicles NonWorkers", "NonWorkerNumberOfVehicles.csv", typeof(FileFromInputDirectory), "File describing distribution of number of vehicles for non-workers only by age" +
                " by license availability " +
                "for each planning district (PD) such that the sum over # of vehicles for each occupation for each license availability for each PD = 1.0. License availability is " +
                "either 0 or 1. Expected format is MIT: PD, LIC, AGE, #VEH, [value]")]
        public FileFromInputDirectory NonWorkerVehicleRateFile;

        [RunParameter("Occupation Distribution By PD", true, "Set to true if the Occupation Distribution information is stored by Planning District.  False if it is by zone.")]
        public bool OccupationByPD;

        [RunParameter("Occupation Distribution", "OccupationPDAge.csv", typeof(FileFromInputDirectory), "File describing occupation distribution by employment status by age for each planning district" +
                " (PD) such that the sum across all occupation categories for each employmnt status for each age for each PD = 1.0. Age categories, employment status types, and occupations must " +
                " match this module's respective parameter definitions. Expected file type is MIT: PD, AGE, EMP_STAT, OCC, [value]")]
        public FileFromInputDirectory OccupationDistributionFile;

        [RootModule]
        public ITravelDemandModel Root;

        /*
        [Parameter("Employment Categories", "Unemployed,Professional,General,Sale,Manufacturing", "Comma-separated list of occupation types. The order corresponds to the expected indexing " +
                "in files (e.g., by default '0' is 'Unemployed', '1' is 'Professional' etc.)")]
        public string OccupationCategoriesString;*/

        /*
        [RunParameter("Employment State Categories", "Unemployed,Full-Time,Part-Time", "Comma-separated list of employment statuses. The order corresponds to the expected indexing " +
                "in files (e.g., by default '0' is 'Unemployed', '1' is 'Full-Time' etc.)")]
        public string EmploymentStateString;*/

        [RunParameter("Save Data", true, "Option to save the data inside of the zone network.")]
        public bool SaveDataIntoZones;

        [RunParameter("Student Distribution", "StudentPDAge.csv", typeof(FileFromInputDirectory), "File describing rate of students by employment status type by age category for each planning " +
                "district (PD), where the rate is in the interval [0, 1]. Expected file type is MIT: PD, AGE, EMP_STAT, [value]")]
        public FileFromInputDirectory StudentDistributionFile;

        [Parameter("Unemployed Status", 0, "The index of the 'unemployed' Employment Status")]
        public int UnemployedEmploymentStatus;

        [RunParameter("#Vehicles Workers", "WorkerNumberOfVehicles.csv", typeof(FileFromInputDirectory), "File describing distribution of number of vehicles for workers only by occupation type by license availability " +
                "for each planning district (PD) such that the sum over # of vehicles for each occupation for each license availability for each PD = 1.0. License availability is " +
                "either 0 or 1. Expected format is MIT: PD, LIC, OCC, #VEH, [value]")]
        public FileFromInputDirectory WorkerVehicleRateFile;

        private Dictionary<int, List<int>> PDZoneMap;

        public SparseArray<Range> AgeCategories
        {
            get;
            set;
        }

        /// <summary>
        /// [ZONE,AGECat]
        /// </summary>
        public SparseTwinIndex<float> AgeRates { get; private set; }

        public SparseArray<SparseTwinIndex<float>> DriversLicenseRates { get; private set; }

        public SparseArray<string> EmploymentStatus
        {
            get;
            set;
        }

        public SparseArray<SparseTwinIndex<float>> EmploymentStatusRates { get; private set; }

        /// <summary>
        /// [Zone,EmploymentStatus,OccupationType]
        /// </summary>
        public SparseTriIndex<float> JobOccupationRates { get; private set; }

        /// <summary>
        /// [Zone, Job Type (unemployed, full-time, part-time)]
        /// </summary>
        public SparseTwinIndex<float> JobTypeRates { get; private set; }

        public string Name
        {
            get;
            set;
        }

        public SparseArray<SparseTriIndex<float>> NonWorkerVehicleRates { get; private set; }

        public SparseArray<string> OccupationCategories
        {
            get;
            set;
        }

        /// <summary>
        /// OccupationRate [AgeCategory, EmploymentStatus, OccupationType]
        /// </summary>
        public SparseArray<SparseTriIndex<float>> OccupationRates { get; private set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        /// <summary>
        /// [ZONE][AGECat,EmploymentCat]
        /// </summary>
        public SparseArray<SparseTwinIndex<float>> SchoolRates { get; private set; }

        public SparseArray<SparseTriIndex<float>> WorkerVehicleRates { get; private set; }

        public IDemographicsData GiveData()
        {
            return this;
        }

        public bool Loaded
        {
            get;
            private set;
        }

        public void LoadData()
        {
            Loaded = true;
            LoadPDZoneMap();
            LoadCategoryInformation();
            LoadAgeDist();
            LoadEmploymentDist();
            LoadOccupationDist();
            LoadStudentDist();
            LoadJobOccupationDistribution();
            LoadJobTypeDisribution();
            LoadDriversLicenseDistribution();
            LoadNumberOfCarsDistribution();
            if (SaveDataIntoZones)
            {
                var employmentStatusIndexes = EmploymentStatus.ValidIndexArray();
                var ageCategoryIndexes = AgeCategories.ValidIndexArray();
                var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
                Parallel.For(0, zones.Length, zone =>
               {
                   var z = zones[zone];
                   float generalworker = 0;
                   float manufacturingworker = 0;
                   float professionalworker = 0;
                   float salesWorker = 0;
                   float generalJob = 0;
                   float manufacturingJob = 0;
                   float professionalJob = 0;
                   float salesJob = 0;
                   var occRates = OccupationRates[zone];
                   var empData = EmploymentStatusRates[zone];
                   if (occRates != null && empData != null)
                   {
                       var pop = z.Population;
                       foreach (var age in ageCategoryIndexes)
                       {
                           var agePop = pop * AgeRates[zone, age];
                           foreach (var status in employmentStatusIndexes)
                           {
                               var statusPop = agePop * empData[age, status];
                               professionalworker += statusPop * occRates[age, status, 1];
                               generalworker += statusPop * occRates[age, status, 2];
                               salesWorker += statusPop * occRates[age, status, 3];
                               manufacturingworker += statusPop * occRates[age, status, 4];
                           }
                       }
                       foreach (var status in employmentStatusIndexes)
                       {
                           var statusJobPop = z.Employment * JobTypeRates[zone, status];
                           professionalJob += statusJobPop * JobOccupationRates[zone, status, 1];
                           generalJob += statusJobPop * JobOccupationRates[zone, status, 2];
                           salesJob += statusJobPop * JobOccupationRates[zone, status, 3];
                           manufacturingJob += statusJobPop * JobOccupationRates[zone, status, 4];
                       }
                   }
                   z.GeneralEmployment = generalJob;
                   z.ManufacturingEmployment = manufacturingJob;
                   z.ProfessionalEmployment = professionalJob;
                   z.RetailEmployment = salesJob;

                   z.WorkGeneral = generalworker;
                   z.WorkManufacturing = manufacturingworker;
                   z.WorkProfessional = professionalworker;
                   z.WorkRetail = salesWorker;
               });
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            try
            {
                AgeCategories = Range.Parse(AgeCategoryString);
            }
            catch (ArgumentException e)
            {
                error = e.Message;
                return false;
            }
            if (CheckForOverlap(ref error, AgeCategories))
            {
                return false;
            }
            return true;
        }

        public void UnloadData()
        {
            AgeRates = null;
            OccupationRates = null;
            SchoolRates = null;
            Loaded = false;
        }

        private bool CheckForOverlap(ref string error, SparseArray<Range> sparseArray)
        {
            var flatData = sparseArray.GetFlatData();
            for (int i = 0; i < flatData.Length; i++)
            {
                for (int j = i + 1; j < flatData.Length; j++)
                {
                    if (flatData[i].Start < flatData[j].Start)
                    {
                        if (flatData[i].Stop >= flatData[j].Start)
                        {
                            error = "In '" + Name + "' there is an overlap in age category '" + sparseArray.GetSparseIndex(i)
                                + "' and '" + sparseArray.GetSparseIndex(j);
                            return true;
                        }
                    }
                    else
                    {
                        if (flatData[j].Stop >= flatData[i].Start)
                        {
                            error = "In '" + Name + "' there is an overlap in age category '" + sparseArray.GetSparseIndex(i)
                                + "' and '" + sparseArray.GetSparseIndex(j);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private SparseArray<string> LinearStringComaSplit(string str)
        {
            string[] parts = str.Split(',');
            int[] place = new int[parts.Length];
            for (int i = 0; i < place.Length; i++)
            {
                place[i] = i;
            }
            return SparseArray<string>.CreateSparseArray(place, parts);
        }

        private void LoadAgeDist()
        {
            List<AgeDist> ageDistributions = [];
            var ageCategories = AgeCategories.Count;
            using (CommentedCsvReader reader = new CommentedCsvReader(AgeDistributionFile.GetFileName(Root.InputBaseDirectory)))
            {
                while (reader.NextLine())
                {
                    if (reader.NumberOfCurrentCells >= ageCategories + 1)
                    {
                        float[] ageD = new float[ageCategories];
                        reader.Get(out int zone, 0);
                        for (int i = 1; i < reader.NumberOfCurrentCells; i++)
                        {
                            reader.Get(out ageD[i - 1], i);
                        }
                        ageDistributions.Add(new AgeDist { Zone = zone, Percentages = ageD });
                    }
                }
            }
            int numberOfSetZones = 0;
            foreach (var ageDist in ageDistributions)
            {
                if (!PDZoneMap.TryGetValue(ageDist.Zone, out List<int> pd))
                {
                    throw new XTMFRuntimeException(this, "In " + Name + " we were unable to find a planning district for the zone number '" + ageDist.Zone + "' while loading the age distribution.");
                }
                numberOfSetZones += pd.Count;
            }

            var elements = ageDistributions.Count;
            var first = new int[numberOfSetZones * ageCategories];
            var second = new int[numberOfSetZones * ageCategories];
            var d = new float[numberOfSetZones * ageCategories];
            var validAgeCategory = AgeCategories.ValidIndexies().ToArray();
            int soFar = 0;
            for (int i = 0; i < elements; i++)
            {
                var zones = PDZoneMap[ageDistributions[i].Zone];
                foreach (var zone in zones)
                {
                    for (int j = 0; j < ageCategories; j++)
                    {
                        first[soFar] = zone;
                        second[soFar] = validAgeCategory[j];
                        d[soFar] = ageDistributions[i].Percentages[j];
                        soFar++;
                    }
                }
            }
            AgeRates = SparseTwinIndex<float>.CreateTwinIndex(first, second, d);
        }

        private void LoadCategoryInformation()
        {
            OccupationCategories = LinearStringComaSplit("Unemployed,Professional,General,Sale,Manufacturing");
            EmploymentStatus = LinearStringComaSplit("Unemployed,Full-Time,Part-Time");
        }

        private void LoadDriversLicenseDistribution()
        {
            DriversLicenseRates = Root.ZoneSystem.ZoneArray.CreateSimilarArray<SparseTwinIndex<float>>();
            if (!DriversLicenseRateFile.ContainsFileName())
            {
                return;
            }
            using (CommentedCsvReader reader = new CommentedCsvReader(DriversLicenseRateFile.GetFileName(Root.InputBaseDirectory)))
            {
                while (reader.NextLine())
                {
                    if (reader.NumberOfCurrentCells >= 4)
                    {
                        reader.Get(out int pd, 0);
                        reader.Get(out int ageCat, 1);
                        reader.Get(out int empStat, 2);
                        reader.Get(out float chance, 3);
                        foreach (var zone in PDZoneMap[pd])
                        {
                            var zoneData = DriversLicenseRates[zone];
                            if (zoneData == null)
                            {
                                zoneData = SparseTwinIndex<float>.CreateSimilarArray(AgeCategories, EmploymentStatus);
                                DriversLicenseRates[zone] = zoneData;
                            }
                            zoneData[ageCat, empStat] = chance;
                        }
                    }
                }
            }
        }

        private void LoadEmploymentDist()
        {
            List<EmploymentDist> employment = [];

            using (CommentedCsvReader reader = new CommentedCsvReader(EmploymentDistributionFile.GetFileName(Root.InputBaseDirectory)))
            {
                float[] data = new float[5];
                while (reader.NextLine())
                {
                    if (reader.NumberOfCurrentCells < 5)
                    {
                        continue;
                    }
                    for (int i = 0; i < data.Length && i < reader.NumberOfCurrentCells; i++)
                    {
                        reader.Get(out data[i], i);
                    }
                    employment.Add(new EmploymentDist { AgeCat = (int)data[1], Zone = (int)data[0], NonWork = data[2], FullTime = data[3], PartTime = data[4] });
                }
            }
            employment.Sort(delegate (EmploymentDist first, EmploymentDist second)
           {
               if (first.Zone > second.Zone)
               {
                   return 1;
               }
               if (first.Zone == second.Zone)
               {
                   if (first.AgeCat > second.AgeCat)
                   {
                       return 1;
                   }
                   if (first.AgeCat == second.AgeCat)
                   {
                       return 0;
                   }
               }
               return -1;
           });
            EmploymentStatusRates = Root.ZoneSystem.ZoneArray.CreateSimilarArray<SparseTwinIndex<float>>();
            int start = 0;
            int stop = 0;
            var employmentLength = employment.Count;
            int[] firstIndex;
            int[] secondIndex;
            float[] d;
            int numberOfElements;
            for (var i = 1; i < employmentLength; i++)
            {
                if (employment[i].Zone == employment[i - 1].Zone)
                {
                    stop = i;
                }
                else
                {
                    numberOfElements = stop - start + 1;
                    firstIndex = new int[numberOfElements * 3];
                    secondIndex = new int[numberOfElements * 3];
                    d = new float[numberOfElements * 3];
                    for (var j = 0; j < numberOfElements; j++)
                    {
                        var ageCat = employment[start + j].AgeCat;
                        for (int k = 0; k < 3; k++)
                        {
                            firstIndex[j * 3 + k] = ageCat;
                            secondIndex[j * 3 + k] = k;
                        }
                        d[j * 3] = employment[start + j].NonWork;
                        d[j * 3 + 1] = employment[start + j].FullTime;
                        d[j * 3 + 2] = employment[start + j].PartTime;
                    }
                    foreach (var z in PDZoneMap[employment[i - 1].Zone])
                    {
                        EmploymentStatusRates[z] = SparseTwinIndex<float>.CreateTwinIndex(firstIndex, secondIndex, d);
                    }
                    start = i;
                }
            }
            numberOfElements = stop - start + 1;
            firstIndex = new int[numberOfElements * 3];
            secondIndex = new int[numberOfElements * 3];
            d = new float[numberOfElements * 3];
            for (var j = 0; j < numberOfElements; j++)
            {
                for (var k = 0; k < 3; k++)
                {
                    firstIndex[j * 3 + k] = employment[start + j].AgeCat;
                    secondIndex[j * 3 + k] = k;
                }
                d[j * 3] = employment[start + j].NonWork;
                d[j * 3 + 1] = employment[start + j].FullTime;
                d[j * 3 + 2] = employment[start + j].PartTime;
            }
            foreach (var z in PDZoneMap[employment[employmentLength - 1].Zone])
            {
                EmploymentStatusRates[z] = SparseTwinIndex<float>.CreateTwinIndex(firstIndex, secondIndex, d);
            }
        }

        private void LoadJobOccupationDistribution()
        {
            JobOccupationRates = SparseTriIndex<float>.CreateSimilarArray(Root.ZoneSystem.ZoneArray, EmploymentStatus, OccupationCategories);
            var occupationIndexes = OccupationCategories.ValidIndexies().ToArray();
            using (CommentedCsvReader reader = new CommentedCsvReader(JobOccupationRateFile.GetFileName(Root.InputBaseDirectory)))
            {

                while (reader.NextLine())
                {
                    if (reader.NumberOfCurrentCells >= 5)
                    {
                        reader.Get(out int pd, 0);
                        reader.Get(out int employmentStatus, 1);
                        reader.Get(out float professional, 2);
                        reader.Get(out float general, 3);
                        reader.Get(out float sales, 4);
                        reader.Get(out float manufacturing, 5);
                        foreach (var zone in PDZoneMap[pd])
                        {
                            JobOccupationRates[zone, employmentStatus, occupationIndexes[0]] = 0;
                            JobOccupationRates[zone, employmentStatus, occupationIndexes[1]] = professional;
                            JobOccupationRates[zone, employmentStatus, occupationIndexes[2]] = general;
                            JobOccupationRates[zone, employmentStatus, occupationIndexes[3]] = sales;
                            JobOccupationRates[zone, employmentStatus, occupationIndexes[4]] = manufacturing;
                        }
                    }
                }
            }
        }

        private void LoadJobTypeDisribution()
        {
            JobTypeRates = SparseTwinIndex<float>.CreateSimilarArray(Root.ZoneSystem.ZoneArray, EmploymentStatus);
            var employmentIndexes = EmploymentStatus.ValidIndexies().ToArray();
            using (CommentedCsvReader reader = new CommentedCsvReader(JobEmploymentRateFile.GetFileName(Root.InputBaseDirectory)))
            {

                while (reader.NextLine())
                {
                    if (reader.NumberOfCurrentCells >= 3)
                    {
                        reader.Get(out int pd, 0);
                        reader.Get(out float fulltime, 1);
                        reader.Get(out float parttime, 2);
                        foreach (var zone in PDZoneMap[pd])
                        {
                            JobTypeRates[zone, employmentIndexes[1]] = fulltime;
                            JobTypeRates[zone, employmentIndexes[2]] = parttime;
                        }
                    }
                }
            }
        }

        private void LoadNonWorkerCarDistribution()
        {
            NonWorkerVehicleRates = Root.ZoneSystem.ZoneArray.CreateSimilarArray<SparseTriIndex<float>>();
            SparseArray<float> numberOfVehicles =
                new SparseArray<float>(new SparseIndexing { Indexes = new[] { new SparseSet { Start = 0, Stop = 2 } } });
            SparseArray<float> driversLicense =
                new SparseArray<float>(new SparseIndexing { Indexes = new[] { new SparseSet { Start = 0, Stop = 1 } } });
            if (!NonWorkerVehicleRateFile.ContainsFileName())
            {
                return;
            }
            using (CommentedCsvReader reader = new CommentedCsvReader(NonWorkerVehicleRateFile.GetFileName(Root.InputBaseDirectory)))
            {
                while (reader.NextLine())
                {
                    if (reader.NumberOfCurrentCells >= 6)
                    {
                        reader.Get(out int pd, 0);
                        reader.Get(out int driversLic, 1);
                        reader.Get(out int ageCat, 2);
                        reader.Get(out float chanceZero, 3);
                        reader.Get(out float chanceOne, 4);
                        reader.Get(out float chanceTwo, 5);
                        foreach (var zone in PDZoneMap[pd])
                        {
                            var zoneData = NonWorkerVehicleRates[zone];
                            if (zoneData == null)
                            {
                                zoneData = SparseTriIndex<float>.CreateSimilarArray(driversLicense, AgeCategories, numberOfVehicles);
                                NonWorkerVehicleRates[zone] = zoneData;
                            }
                            zoneData[driversLic, ageCat, 0] = chanceZero;
                            zoneData[driversLic, ageCat, 1] = chanceOne;
                            zoneData[driversLic, ageCat, 2] = chanceTwo;
                        }
                    }
                }
            }
        }

        private void LoadNumberOfCarsDistribution()
        {
            LoadWorkerCarDistribution();
            LoadNonWorkerCarDistribution();
        }

        private void LoadOccupationDist()
        {
            List<OccupationDist> occupation = [];
            if (SaveDataIntoZones)
            {
                foreach (var zone in Root.ZoneSystem.ZoneArray.ValidIndexies())
                {
                    var z = Root.ZoneSystem.ZoneArray[zone];
                    z.WorkGeneral = 0;
                    z.WorkManufacturing = 0;
                    z.WorkProfessional = 0;
                    z.WorkRetail = 0;
                }
            }
            using (CommentedCsvReader reader = new CommentedCsvReader(OccupationDistributionFile.GetFileName(Root.InputBaseDirectory)))
            {
                float[] data = new float[7];

                while (reader.NextLine())
                {
                    if (reader.NumberOfCurrentCells < 7)
                    {
                        continue;
                    }
                    for (int i = 0; i < data.Length && i < reader.NumberOfCurrentCells; i++)
                    {
                        reader.Get(out data[i], i);
                    }
                    occupation.Add(new OccupationDist
                    {
                        AgeCat = (int)data[1],
                        Zone = (int)data[0],
                        EmploymentStatus = (int)data[2],
                        Professional = data[3],
                        General = data[4],
                        Sales = data[5],
                        Manufacturing = data[6]
                    });
                }
            }
            occupation.Sort(delegate (OccupationDist first, OccupationDist second)
           {
               if (first.Zone > second.Zone)
               {
                   return 1;
               }
               if (first.Zone == second.Zone)
               {
                   if (first.AgeCat > second.AgeCat)
                   {
                       return 1;
                   }
                   if (first.AgeCat == second.AgeCat)
                   {
                       if (first.EmploymentStatus > second.EmploymentStatus)
                       {
                           return 1;
                       }
                       if (first.EmploymentStatus == second.EmploymentStatus)
                       {
                           return 0;
                       }
                   }
               }
               return -1;
           });
            OccupationRates = Root.ZoneSystem.ZoneArray.CreateSimilarArray<SparseTriIndex<float>>();
            var start = 0;
            var stop = 0;
            var employmentLength = occupation.Count;
            int[] firstIndex;
            int[] secondIndex;
            int[] thirdIndex;
            float[] d;
            int numberOfElements;
            for (int i = 1; i < employmentLength; i++)
            {
                if (occupation[i].Zone == occupation[i - 1].Zone)
                {
                    stop = i;
                }
                else
                {
                    numberOfElements = stop - start + 1;
                    firstIndex = new int[numberOfElements * 5];
                    secondIndex = new int[numberOfElements * 5];
                    thirdIndex = new int[numberOfElements * 5];
                    d = new float[numberOfElements * 5];
                    for (int j = 0; j < numberOfElements; j++)
                    {
                        for (int k = 0; k < 5; k++)
                        {
                            firstIndex[j * 5 + k] = occupation[start + j].AgeCat;
                            secondIndex[j * 5 + k] = occupation[start + j].EmploymentStatus;
                            thirdIndex[j * 5 + k] = k;
                        }
                        d[j * 5 + 1] = occupation[start + j].Professional;
                        d[j * 5 + 2] = occupation[start + j].General;
                        d[j * 5 + 3] = occupation[start + j].Sales;
                        d[j * 5 + 4] = occupation[start + j].Manufacturing;
                    }
                    if (OccupationByPD)
                    {
                        foreach (var z in PDZoneMap[occupation[i - 1].Zone])
                        {
                            OccupationRates[z] = SparseTriIndex<float>.CreateSparseTriIndex(firstIndex, secondIndex, thirdIndex, d);
                        }
                    }
                    else
                    {
                        OccupationRates[occupation[i - 1].Zone] = SparseTriIndex<float>.CreateSparseTriIndex(firstIndex, secondIndex, thirdIndex, d);
                    }
                    start = i;
                }
            }
            numberOfElements = stop - start + 1;
            firstIndex = new int[numberOfElements * 5];
            secondIndex = new int[numberOfElements * 5];
            thirdIndex = new int[numberOfElements * 5];
            d = new float[numberOfElements * 5];
            for (int j = 0; j < numberOfElements; j++)
            {
                for (int k = 0; k < 5; k++)
                {
                    firstIndex[j * 5 + k] = occupation[start + j].AgeCat;
                    secondIndex[j * 5 + k] = occupation[start + j].EmploymentStatus;
                    thirdIndex[j * 5 + k] = k;
                }

                d[j * 5 + 1] = occupation[start + j].Professional;
                d[j * 5 + 2] = occupation[start + j].General;
                d[j * 5 + 3] = occupation[start + j].Sales;
                d[j * 5 + 4] = occupation[start + j].Manufacturing;
            }
            if (OccupationByPD)
            {
                foreach (var z in PDZoneMap[occupation[employmentLength - 1].Zone])
                {
                    OccupationRates[z] = SparseTriIndex<float>.CreateSparseTriIndex(firstIndex, secondIndex, thirdIndex, d);
                }
            }
            else
            {
                OccupationRates[occupation[employmentLength - 1].Zone] = SparseTriIndex<float>.CreateSparseTriIndex(firstIndex, secondIndex, thirdIndex, d);
            }
        }

        private void LoadPDZoneMap()
        {
            PDZoneMap = [];
            var zoneArray = Root.ZoneSystem.ZoneArray;
            foreach (var valid in zoneArray.ValidIndexies())
            {
                var z = zoneArray[valid];
                if (PDZoneMap.ContainsKey(z.PlanningDistrict))
                {
                    PDZoneMap[z.PlanningDistrict].Add(z.ZoneNumber);
                }
                else
                {
                    List<int> l =
                    [
                        z.ZoneNumber
                    ];
                    PDZoneMap[z.PlanningDistrict] = l;
                }
            }
        }

        private void LoadStudentDist()
        {
            List<StudentDist> studentData = [];
            using (CommentedCsvReader reader = new CommentedCsvReader(StudentDistributionFile.GetFileName(Root.InputBaseDirectory)))
            {
                float[] data = new float[4];

                while (reader.NextLine())
                {
                    for (int i = 0; i < data.Length && i < reader.NumberOfCurrentCells; i++)
                    {
                        reader.Get(out data[i], i);
                    }
                    studentData.Add(new StudentDist
                    {
                        Zone = (int)data[0],
                        AgeCat = (int)data[1],
                        EmploymentStatus = (int)data[2],
                        Chance = data[3]
                    });
                }
            }
            studentData.Sort(delegate (StudentDist first, StudentDist second)
           {
               if (first.Zone > second.Zone)
               {
                   return 1;
               }
               if (first.Zone == second.Zone)
               {
                   if (first.AgeCat > second.AgeCat)
                   {
                       return 1;
                   }
                   if (first.AgeCat == second.AgeCat)
                   {
                       if (first.EmploymentStatus > second.EmploymentStatus)
                       {
                           return 1;
                       }
                       if (first.EmploymentStatus == second.EmploymentStatus)
                       {
                           return 0;
                       }
                   }
               }
               return -1;
           });
            // Employment is now sorted Zone,Age,EmploymentStatus
            SchoolRates = Root.ZoneSystem.ZoneArray.CreateSimilarArray<SparseTwinIndex<float>>();
            var start = 0;
            var stop = 0;
            var studentDataLength = studentData.Count;
            int[] firstIndex;
            int[] secondIndex;
            float[] d;
            int numberOfElements;
            for (int i = 1; i < studentDataLength; i++)
            {
                if (studentData[i].Zone == studentData[i - 1].Zone)
                {
                    stop = i;
                }
                else
                {
                    numberOfElements = stop - start + 1;
                    firstIndex = new int[numberOfElements];
                    secondIndex = new int[numberOfElements];
                    d = new float[numberOfElements];
                    for (int j = 0; j < numberOfElements; j++)
                    {
                        var data = studentData[start + j];
                        firstIndex[j] = data.AgeCat;
                        secondIndex[j] = data.EmploymentStatus;
                        d[j] = data.Chance;
                    }
                    foreach (var z in PDZoneMap[studentData[i - 1].Zone])
                    {
                        SchoolRates[z] = SparseTwinIndex<float>.CreateTwinIndex(firstIndex, secondIndex, d);
                    }
                    start = i;
                }
            }
            numberOfElements = stop - start + 1;
            firstIndex = new int[numberOfElements];
            secondIndex = new int[numberOfElements];
            d = new float[numberOfElements];
            for (int j = 0; j < numberOfElements; j++)
            {
                firstIndex[j] = studentData[start + j].AgeCat;
                secondIndex[j] = studentData[start + j].EmploymentStatus;
                d[j] = studentData[start + j].Chance;
            }
            foreach (var z in PDZoneMap[studentData[studentDataLength - 1].Zone])
            {
                SchoolRates[z] = SparseTwinIndex<float>.CreateTwinIndex(firstIndex, secondIndex, d);
            }
        }

        private void LoadWorkerCarDistribution()
        {
            WorkerVehicleRates = Root.ZoneSystem.ZoneArray.CreateSimilarArray<SparseTriIndex<float>>();
            SparseArray<float> numberOfVehicles =
                new SparseArray<float>(new SparseIndexing { Indexes = new[] { new SparseSet { Start = 0, Stop = 2 } } });
            SparseArray<float> driversLicense =
                new SparseArray<float>(new SparseIndexing { Indexes = new[] { new SparseSet { Start = 0, Stop = 1 } } });
            if (!WorkerVehicleRateFile.ContainsFileName())
            {
                return;
            }
            using (CommentedCsvReader reader = new CommentedCsvReader(WorkerVehicleRateFile.GetFileName(Root.InputBaseDirectory)))
            {
                while (reader.NextLine())
                {
                    if (reader.NumberOfCurrentCells >= 6) //Only read if the number of columns in the row matches.
                    {
                        reader.Get(out int pd, 0);
                        reader.Get(out int driversLic, 1);
                        reader.Get(out int occ, 2);
                        reader.Get(out float chanceZero, 3);
                        reader.Get(out float chanceOne, 4);
                        reader.Get(out float chanceTwo, 5);
                        foreach (var zone in PDZoneMap[pd])
                        {
                            var zoneData = WorkerVehicleRates[zone];
                            if (zoneData == null)
                            {
                                zoneData = SparseTriIndex<float>.CreateSimilarArray(driversLicense, OccupationCategories, numberOfVehicles);
                                WorkerVehicleRates[zone] = zoneData;
                            }
                            zoneData[driversLic, occ, 0] = chanceZero;
                            zoneData[driversLic, occ, 1] = chanceOne;
                            zoneData[driversLic, occ, 2] = chanceTwo;
                        }
                    }
                }
            }
        }

        private struct AgeDist
        {
            public float[] Percentages;
            public int Zone;
        }

        private struct EmploymentDist
        {
            public int AgeCat;
            public float FullTime;
            public float NonWork;
            public float PartTime;
            public int Zone;
        }

        private struct OccupationDist
        {
            public int AgeCat;
            public int EmploymentStatus;
            public float General;
            public float Manufacturing;
            public float Professional;
            public float Sales;
            public int Zone;

            public override string ToString()
            {
                return String.Format("{0}:{1}:{2} -> {3},{4},{5},{6}", Zone, AgeCat, EmploymentStatus, Professional, General, Sales, Manufacturing);
            }
        }

        private struct StudentDist
        {
            public int AgeCat;
            public float Chance;
            public int EmploymentStatus;
            public int Zone;
        }
    }
}