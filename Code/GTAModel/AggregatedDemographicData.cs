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
using XTMF;
using Range = Datastructure.Range;

namespace TMG.GTAModel;

[ModuleInformation(Description =
    @"This module loads a series of data in order to provide demographic data about a zone system. 
This one in particular takes the data for planning districts and then applies them across the zone system using 
the data loaded in from zone system module. The root module of the model system is required to be of 
type ITravelDemandModel.")]
public class AggregatedDemographicData : IDemographicsData
{
    [RunParameter("Age Categories", "0:0-9,1:10-13,2:14-15,3:16-17,4:18-25,5:26-30,6:31-55,7:56-65,8:66-100", "The age categories used defined in the RANGE format X:Start-EndInclusive.")]
    public string AgeCategoryString;

    [RunParameter("Age Distribution", "AgePD.csv", "The location of the age distribution file.")]
    public string AgeDistributionFile;

    [RunParameter("Age Distribution File Header", true, "If the csv file contains a header.")]
    public bool AgeDistributionFileHeader;

    [RunParameter("Driver's License Rate", "DriversLicense.csv", "The location of the driver's license distribution file.")]
    public string DriversLicenseRateFile;

    [RunParameter("Driver's License Rate File Header", true, "If the csv file contains a header.")]
    public bool DriversLicenseRateFileHeader;

    [RunParameter("Employment Categories", "Unemployed,Professional,General,Sale,Manufacturing", "Comma separated names for the different employment states")]
    public string EmploymentCategoryString;

    [RunParameter("Employment Distribution", "EmploymentPDAge.csv", "The location of the age distribution file.")]
    public string EmploymentDistributionFile;

    [RunParameter("Employment Distribution File Header", true, "If the csv file contains a header.")]
    public bool EmploymentDistributionFileHeader;

    [RunParameter("Employment State Categories", "Unemployed,Full-Time,Part-Time", "The age categories used defined in the RANGE format X:Start-EndInclusive.")]
    public string EmploymentStateString;

    [RunParameter("Job Employment Distribution", "JobEmpStat.csv", "The location of the job employment distribution file. (PD,Full-time,PartTime)")]
    public string JobEmploymentRateFile;

    [RunParameter("Job Employment Distribution File Header", true, "If the csv file contains a header.")]
    public bool JobEmploymentRateFileHeader;

    [RunParameter("Job Occupation Distribution", "JobOccupation.csv", "The location of the job occupation distribution file.")]
    public string JobOccupationRateFile;

    [RunParameter("Job Occupation Distribution File Header", true, "If the csv file contains a header.")]
    public bool JobOccupationRateFileHeader;

    [RunParameter("#Vehicles NonWorkers", "NonWorkerNumberOfVehicles.csv", "The location of the driver's license distribution file.")]
    public string NonWorkerVehicleRateFile;

    [RunParameter("#Vehicles NonWorkers Header", true, "If the csv file contains a header.")]
    public bool NonWorkerVehicleRateFileHeader;

    [RunParameter("Occupation Distribution", "OccupationPDAge.csv", "The location of the age distribution file.")]
    public string OccupationDistributionFile;

    [RunParameter("Occupation Distribution File Header", true, "If the csv file contains a header.")]
    public bool OccupationDistributionFileHeader;

    [RootModule]
    public ITravelDemandModel Root;

    [RunParameter("Save Data", true, "Should we change the data inside of the zone network?")]
    public bool SaveDataIntoZones;

    [RunParameter("Student Distribution", "StudentPDAge.csv", "The location of the age distribution file.")]
    public string StudentDistributionFile;

    [RunParameter("Student Distribution File Header", true, "If the csv file contains a header.")]
    public bool StudentDistributionFileHeader;

    [RunParameter("Unemployed Status", 0, "The index of the unemployed Employment Status")]
    public int UnemployedEmploymentStatus;

    [RunParameter("#Vehicles Workers", "WorkerNumberOfVehicles.csv", "The location of the driver's license distribution file.")]
    public string WorkerVehicleRateFile;

    [RunParameter("#Vehicles Workers Header", true, "If the csv file contains a header.")]
    public bool WorkerVehicleRateFileHeader;

    private Dictionary<int, List<int>> PdZoneMap;

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
        get { return 0; }
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
        LoadPdZoneMap();
        LoadCategoryInformation();
        LoadAgeDist();
        LoadEmploymentDist();
        LoadOccupationDist();
        LoadStudentDist();
        LoadJobOccupationDistribution();
        LoadJobTypeDisribution();
        LoadDriversLicenseDistribution();
        LoadNumberOfCarsDistribution();
        if(SaveDataIntoZones)
        {
            foreach(var zone in Root.ZoneSystem.ZoneArray.ValidIndexies())
            {
                var z = Root.ZoneSystem.ZoneArray[zone];
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
                if(occRates != null && empData != null)
                {
                    foreach(var age in AgeCategories.ValidIndexies())
                    {
                        var agePop = z.Population * AgeRates[zone, age];
                        foreach(var status in EmploymentStatus.ValidIndexies())
                        {
                            var statusPop = agePop * empData[age, status];
                            professionalworker += statusPop * occRates[age, status, 1];
                            generalworker += statusPop * occRates[age, status, 2];
                            salesWorker += statusPop * occRates[age, status, 3];
                            manufacturingworker += statusPop * occRates[age, status, 4];
                        }
                    }
                    foreach(var status in EmploymentStatus.ValidIndexies())
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
            }
        }
    }

    public bool RuntimeValidation(ref string error)
    {
        try
        {
            AgeCategories = Range.Parse(AgeCategoryString);
        }
        catch(ArgumentException e)
        {
            error = e.Message;
            return false;
        }
        if(CheckForOverlap(ref error, AgeCategories))
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
        for(int i = 0; i < flatData.Length; i++)
        {
            for(int j = i + 1; j < flatData.Length; j++)
            {
                if(flatData[i].Start < flatData[j].Start)
                {
                    if(flatData[i].Stop >= flatData[j].Start)
                    {
                        error = "In '" + Name + "' there is an overlap in age category '" + sparseArray.GetSparseIndex(i)
                            + "' and '" + sparseArray.GetSparseIndex(j);
                        return true;
                    }
                }
                else
                {
                    if(flatData[j].Stop >= flatData[i].Start)
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

    private string GetFullPath(string localPath)
    {
        var fullPath = localPath;
        if(!Path.IsPathRooted(fullPath))
        {
            fullPath = Path.Combine(Root.InputBaseDirectory, fullPath);
        }
        return fullPath;
    }

    private SparseArray<string> LinearStringComaSplit(string str)
    {
        string[] parts = str.Split(',');
        int[] place = new int[parts.Length];
        for(int i = 0; i < place.Length; i++)
        {
            place[i] = i;
        }
        return SparseArray<string>.CreateSparseArray(place, parts);
    }

    private void LoadAgeDist()
    {
        List<AgeDist> ageDistributions = [];
        var ageCategories = AgeCategories.Count;
        using(CsvReader reader = new(GetFullPath(AgeDistributionFile)))
        {
            int length;
            if(AgeDistributionFileHeader)
            {
                // burn the header
                reader.LoadLine();
            }
            while((length = reader.LoadLine()) > ageCategories)
            {
                float[] ageD = new float[ageCategories];
                reader.Get(out int zone, 0);
                for(int i = 1; i < length; i++)
                {
                    reader.Get(out ageD[i - 1], i);
                }
                ageDistributions.Add(new AgeDist { Zone = zone, Percentages = ageD });
            }
        }
        int numberOfSetZones = 0;
        foreach(var ageDist in ageDistributions)
        {
            if (PdZoneMap.TryGetValue(ageDist.Zone, out List<int> temp))
            {
                numberOfSetZones += temp.Count;
            }
        }

        var elements = ageDistributions.Count;
        var first = new int[numberOfSetZones * ageCategories];
        var second = new int[numberOfSetZones * ageCategories];
        var d = new float[numberOfSetZones * ageCategories];
        var validAgeCategory = AgeCategories.ValidIndexies().ToArray();
        int soFar = 0;
        for(int i = 0; i < elements; i++)
        {
            if (PdZoneMap.TryGetValue(ageDistributions[i].Zone, out List<int> zones))
            {
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
        }
        AgeRates = SparseTwinIndex<float>.CreateTwinIndex(first, second, d);
    }

    private void LoadCategoryInformation()
    {
        OccupationCategories = LinearStringComaSplit(EmploymentCategoryString);
        EmploymentStatus = LinearStringComaSplit(EmploymentStateString);
    }

    private void LoadDriversLicenseDistribution()
    {
        DriversLicenseRates = Root.ZoneSystem.ZoneArray.CreateSimilarArray<SparseTwinIndex<float>>();
        using CsvReader reader = new(GetFullPath(DriversLicenseRateFile));
        if (DriversLicenseRateFileHeader)
        {
            reader.LoadLine();
        }
        while (!reader.EndOfFile)
        {
            var length = reader.LoadLine();
            if (length >= 4)
            {
                reader.Get(out int pd, 0);
                reader.Get(out int ageCat, 1);
                reader.Get(out int empStat, 2);
                reader.Get(out float chance, 3);
                if (PdZoneMap.TryGetValue(pd, out List<int> zones))
                {
                    foreach (var zone in zones)
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
        List<int> zones;
        List<EmploymentDist> employment = [];
        using(CsvReader reader = new(GetFullPath(EmploymentDistributionFile)))
        {
            int length;
            float[] data = new float[5];
            if(EmploymentDistributionFileHeader)
            {
                // burn the header
                reader.LoadLine();
            }
            while(!reader.EndOfFile)
            {
                if((length = reader.LoadLine()) < 5) continue;
                for(int i = 0; i < data.Length && i < length; i++)
                {
                    reader.Get(out data[i], i);
                }
                employment.Add(new EmploymentDist { AgeCat = (int)data[1], Zone = (int)data[0], NonWork = data[2], FullTime = data[3], PartTime = data[4] });
            }
        }
        employment.Sort(delegate(EmploymentDist first, EmploymentDist second)
        {
            if(first.Zone > second.Zone)
            {
                return 1;
            }
            if(first.Zone == second.Zone)
            {
                if(first.AgeCat > second.AgeCat)
                {
                    return 1;
                }
                if(first.AgeCat == second.AgeCat)
                {
                    return 0;
                }
            }
            return -1;
        });
        EmploymentStatusRates = Root.ZoneSystem.ZoneArray.CreateSimilarArray<SparseTwinIndex<float>>();
        var start = 0;
        var stop = 0;
        var employmentLength = employment.Count;
        int[] firstIndex;
        int[] secondIndex;
        float[] d;
        int numberOfElements;
        for(int i = 1; i < employmentLength; i++)
        {
            if(employment[i].Zone == employment[i - 1].Zone)
            {
                stop = i;
            }
            else
            {
                numberOfElements = stop - start + 1;
                firstIndex = new int[numberOfElements * 3];
                secondIndex = new int[numberOfElements * 3];
                d = new float[numberOfElements * 3];
                for(int j = 0; j < numberOfElements; j++)
                {
                    var ageCat = employment[start + j].AgeCat;
                    for(int k = 0; k < 3; k++)
                    {
                        firstIndex[j * 3 + k] = ageCat;
                        secondIndex[j * 3 + k] = k;
                    }
                    d[j * 3] = employment[start + j].NonWork;
                    d[j * 3 + 1] = employment[start + j].FullTime;
                    d[j * 3 + 2] = employment[start + j].PartTime;
                }
                if(PdZoneMap.TryGetValue(employment[i - 1].Zone, out zones))
                {
                    foreach(var z in zones)
                    {
                        EmploymentStatusRates[z] = SparseTwinIndex<float>.CreateTwinIndex(firstIndex, secondIndex, d);
                    }
                }
                start = i;
            }
        }
        numberOfElements = stop - start + 1;
        firstIndex = new int[numberOfElements * 3];
        secondIndex = new int[numberOfElements * 3];
        d = new float[numberOfElements * 3];
        for(int j = 0; j < numberOfElements; j++)
        {
            for(int k = 0; k < 3; k++)
            {
                firstIndex[j * 3 + k] = employment[start + j].AgeCat;
                secondIndex[j * 3 + k] = k;
            }
            d[j * 3] = employment[start + j].NonWork;
            d[j * 3 + 1] = employment[start + j].FullTime;
            d[j * 3 + 2] = employment[start + j].PartTime;
        }
        if(PdZoneMap.TryGetValue(employment[employmentLength - 1].Zone, out zones))
        {
            foreach(var z in zones)
            {
                EmploymentStatusRates[z] = SparseTwinIndex<float>.CreateTwinIndex(firstIndex, secondIndex, d);
            }
        }
    }

    private void LoadJobOccupationDistribution()
    {
        JobOccupationRates = SparseTriIndex<float>.CreateSimilarArray(Root.ZoneSystem.ZoneArray, EmploymentStatus, OccupationCategories);
        var occupationIndexes = OccupationCategories.ValidIndexies().ToArray();
        using CsvReader reader = new(GetFullPath(JobOccupationRateFile));
        if (JobOccupationRateFileHeader)
        {
            reader.LoadLine();
        }
        while (!reader.EndOfFile)
        {
            var length = reader.LoadLine();
            if (length >= 5)
            {
                reader.Get(out int pd, 0);
                reader.Get(out int employmentStatus, 1);
                reader.Get(out float professional, 2);
                reader.Get(out float general, 3);
                reader.Get(out float sales, 4);
                reader.Get(out float manufacturing, 5);
                if (PdZoneMap.TryGetValue(pd, out List<int> zones))
                {
                    foreach (var zone in zones)
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
        using CsvReader reader = new(GetFullPath(JobEmploymentRateFile));
        if (JobEmploymentRateFileHeader)
        {
            reader.LoadLine();
        }
        while (!reader.EndOfFile)
        {
            var length = reader.LoadLine();
            if (length >= 3)
            {
                reader.Get(out int pd, 0);
                reader.Get(out float fulltime, 1);
                reader.Get(out float parttime, 2);
                if (PdZoneMap.TryGetValue(pd, out List<int> zones))
                {
                    foreach (var zone in zones)
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
            new(new SparseIndexing { Indexes = [new SparseSet { Start = 0, Stop = 2 }] });
        SparseArray<float> driversLicense =
            new(new SparseIndexing { Indexes = [new SparseSet { Start = 0, Stop = 1 }] });
        using CsvReader reader = new(GetFullPath(NonWorkerVehicleRateFile));
        if (NonWorkerVehicleRateFileHeader)
        {
            reader.LoadLine();
        }
        while (!reader.EndOfFile)
        {
            var length = reader.LoadLine();
            if (length >= 6)
            {
                reader.Get(out int pd, 0);
                reader.Get(out int driversLic, 1);
                reader.Get(out int ageCat, 2);
                reader.Get(out float chanceZero, 3);
                reader.Get(out float chanceOne, 4);
                reader.Get(out float chanceTwo, 5);
                if (PdZoneMap.TryGetValue(pd, out List<int> zones))
                {
                    foreach (var zone in zones)
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
        List<int> zones;
        if(SaveDataIntoZones)
        {
            foreach(var zone in Root.ZoneSystem.ZoneArray.ValidIndexies())
            {
                var z = Root.ZoneSystem.ZoneArray[zone];
                z.WorkGeneral = 0;
                z.WorkManufacturing = 0;
                z.WorkProfessional = 0;
                z.WorkRetail = 0;
            }
        }
        using (CsvReader reader = new(GetFullPath(OccupationDistributionFile)))
        {
            int length;
            float[] data = new float[7];
            if(OccupationDistributionFileHeader)
            {
                // burn the header
                reader.LoadLine();
            }
            while ((length = reader.LoadLine()) > 6 )
            {
                for ( int i = 0; i < data.Length && i < length; i++)
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
                } );
            }
        }
        occupation.Sort(delegate (OccupationDist first, OccupationDist second)
        {
            if(first.Zone > second.Zone)
            {
                return 1;
            }
            if(first.Zone == second.Zone)
            {
                if(first.AgeCat > second.AgeCat)
                {
                    return 1;
                }
                if(first.AgeCat == second.AgeCat)
                {
                    if(first.EmploymentStatus > second.EmploymentStatus)
                    {
                        return 1;
                    }
                    if(first.EmploymentStatus == second.EmploymentStatus)
                    {
                        return 0;
                    }
                }
            }
            return -1;
        } );
        OccupationRates = Root.ZoneSystem.ZoneArray.CreateSimilarArray<SparseTriIndex<float>>();
        var start = 0;
        var stop = 0;
        var employmentLength = occupation.Count;
        int[] firstIndex;
        int[] secondIndex;
        int[] thirdIndex;
        float[] d;
        int numberOfElements;
        for ( int i = 1; i < employmentLength; i++)
        {
            if(occupation[i].Zone == occupation[i - 1].Zone)
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
                for ( int j = 0; j < numberOfElements; j++)
                {
                    for ( int k = 0; k < 5; k++)
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
                if(PdZoneMap.TryGetValue(occupation[i - 1].Zone, out zones) )
                {
                    foreach(var z in zones)
                    {
                        OccupationRates[z] = SparseTriIndex<float>.CreateSparseTriIndex(firstIndex, secondIndex, thirdIndex, d);
                    }
                }
                start = i;
            }
        }
        numberOfElements = stop - start + 1;
        firstIndex = new int[numberOfElements * 5];
        secondIndex = new int[numberOfElements * 5];
        thirdIndex = new int[numberOfElements * 5];
        d = new float[numberOfElements * 5];
        for ( int j = 0; j < numberOfElements; j++)
        {
            for ( int k = 0; k < 5; k++)
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
        if(PdZoneMap.TryGetValue(occupation[employmentLength - 1].Zone, out zones) )
        {
            foreach(var z in zones)
            {
                OccupationRates[z] = SparseTriIndex<float>.CreateSparseTriIndex(firstIndex, secondIndex, thirdIndex, d);
            }
        }
    }

    private void LoadPdZoneMap()
    {
        PdZoneMap = [];
        var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
        for(int i = 0; i < zones.Length; i++)
        {
            var z = zones[i];
            if(PdZoneMap.ContainsKey(z.PlanningDistrict))
            {
                PdZoneMap[z.PlanningDistrict].Add(z.ZoneNumber);
            }
            else
            {
                List<int> l = [z.ZoneNumber];
                PdZoneMap[z.PlanningDistrict] = l;
            }
        }
    }

    private void LoadStudentDist()
    {
        List<StudentDist> studentData = [];
        List<int> zones;
        using (CsvReader reader = new(GetFullPath(StudentDistributionFile)))
        {
            int length;
            float[] data = new float[4];
            if(StudentDistributionFileHeader)
            {
                // burn the header
                reader.LoadLine();
            }
            while ((length = reader.LoadLine()) > 2 )
            {
                for ( int i = 0; i < data.Length && i < length; i++)
                {
                    reader.Get(out data[i], i);
                }
                studentData.Add(new StudentDist
                {
                    Zone = (int)data[0],
                    AgeCat = (int)data[1],
                    EmploymentStatus = (int)data[2],
                    Chance = data[3]
                } );
            }
        }
        studentData.Sort(delegate (StudentDist first, StudentDist second)
        {
            if(first.Zone > second.Zone)
            {
                return 1;
            }
            if(first.Zone == second.Zone)
            {
                if(first.AgeCat > second.AgeCat)
                {
                    return 1;
                }
                if(first.AgeCat == second.AgeCat)
                {
                    if(first.EmploymentStatus > second.EmploymentStatus)
                    {
                        return 1;
                    }
                    if(first.EmploymentStatus == second.EmploymentStatus)
                    {
                        return 0;
                    }
                }
            }
            return -1;
        } );
        // Employment is now sorted Zone,Age,EmploymentStatus
        SchoolRates = Root.ZoneSystem.ZoneArray.CreateSimilarArray<SparseTwinIndex<float>>();
        var start = 0;
        var stop = 0;
        var studentDataLength = studentData.Count;
        int[] firstIndex;
        int[] secondIndex;
        float[] d;
        int numberOfElements;
        for ( int i = 1; i < studentDataLength; i++)
        {
            if(studentData[i].Zone == studentData[i - 1].Zone)
            {
                stop = i;
            }
            else
            {
                numberOfElements = stop - start + 1;
                firstIndex = new int[numberOfElements];
                secondIndex = new int[numberOfElements];
                d = new float[numberOfElements];
                for ( int j = 0; j < numberOfElements; j++)
                {
                    var data = studentData[start + j];
                    firstIndex[j] = data.AgeCat;
                    secondIndex[j] = data.EmploymentStatus;
                    d[j] = data.Chance;
                }
                if(PdZoneMap.TryGetValue(studentData[i - 1].Zone, out zones) )
                {
                    foreach(var z in zones)
                    {
                        SchoolRates[z] = SparseTwinIndex<float>.CreateTwinIndex(firstIndex, secondIndex, d);
                    }
                }
                start = i;
            }
        }
        numberOfElements = stop - start + 1;
        firstIndex = new int[numberOfElements];
        secondIndex = new int[numberOfElements];
        d = new float[numberOfElements];
        for ( int j = 0; j < numberOfElements; j++)
        {
            firstIndex[j] = studentData[start + j].AgeCat;
            secondIndex[j] = studentData[start + j].EmploymentStatus;
            d[j] = studentData[start + j].Chance;
        }
        if(PdZoneMap.TryGetValue(studentData[studentDataLength - 1].Zone, out zones) )
        {
            foreach(var z in zones)
            {
                SchoolRates[z] = SparseTwinIndex<float>.CreateTwinIndex(firstIndex, secondIndex, d);
            }
        }
    }

    private void LoadWorkerCarDistribution()
    {
        WorkerVehicleRates = Root.ZoneSystem.ZoneArray.CreateSimilarArray<SparseTriIndex<float>>();
        SparseArray<float> numberOfVehicles =
            new(new SparseIndexing { Indexes = [new SparseSet { Start = 0, Stop = 2 }] } );
        SparseArray<float> driversLicense =
            new(new SparseIndexing { Indexes = [new SparseSet { Start = 0, Stop = 1 }] } );
        using CsvReader reader = new(GetFullPath(WorkerVehicleRateFile));
        if (WorkerVehicleRateFileHeader)
        {
            reader.LoadLine();
        }
        while (!reader.EndOfFile)
        {
            var length = reader.LoadLine();
            if (length >= 6)
            {
                reader.Get(out int pd, 0);
                reader.Get(out int driversLic, 1);
                reader.Get(out int occ, 2);
                reader.Get(out float chanceZero, 3);
                reader.Get(out float chanceOne, 4);
                reader.Get(out float chanceTwo, 5);
                if (PdZoneMap.TryGetValue(pd, out List<int> zones))
                {
                    foreach (var zone in zones)
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
            return string.Format("{0}:{1}:{2} -> {3},{4},{5},{6}", Zone, AgeCat, EmploymentStatus, Professional, General, Sales, Manufacturing);
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