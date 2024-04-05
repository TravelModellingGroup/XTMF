/*
    Copyright 2014-2017 James Vaughan for integration into XTMF.

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
using System.Text;
using Tasha.Common;
using XTMF;
using Datastructure;
using TMG;
using TMG.Input;
using System.IO;
using System.Linq;

namespace Tasha.PopulationSynthesis;


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

    private float[][] WorkersCatPF;
    private float[][] WorkersCatGF;
    private float[][] WorkersCatSF;
    private float[][] WorkersCatMF;
    private float[][] WorkersCatPP;
    private float[][] WorkersCatGP;
    private float[][] WorkersCatSP;
    private float[][] WorkersCatMP;


    private StreamWriter Writer;

    [SubModelInformation(Required = true, Description = "The directory to store the worker force information.")]
    public FileLocation WorkerForceDirectory;

    public void Execute(ITashaHousehold household, int iteration)
    {
        int householdZoneNumber = household.HomeZone.ZoneNumber;
        int flatZone = Root.ZoneSystem.ZoneArray.GetFlatIndex(householdZoneNumber);
        var expansionMultiplier = ExpansionModiferByZone[flatZone];
        var householdWorkerClassification = ClassifyHousehold(household);
        for (int i = 0; i < household.Persons.Length; i++)
        {
            var person = household.Persons[i];
            person.ExpansionFactor *= expansionMultiplier;
            WritePersonData(household, person, householdWorkerClassification);
        }
    }

    private void WritePersonData(ITashaHousehold household, ITashaPerson person, int workerCategory)
    {
        var zoneArray = Root.ZoneSystem.ZoneArray;
        var zone = zoneArray.GetFlatIndex(household.HomeZone.ZoneNumber);
        Writer.Write(household.HouseholdId);
        Writer.Write(',');
        Writer.Write(person.Id);
        Writer.Write(',');
        Writer.Write(person.Age);
        Writer.Write(',');
        Writer.Write(person.Female ? "F," : "M,");
        Writer.Write(person.Licence ? "Y," : "N,");
        switch (person.TransitPass)
        {
            case TransitPass.Metro:
                Writer.Write("M,");
                break;
            case TransitPass.Go:
                Writer.Write("G,");
                break;
            case TransitPass.Combination:
                Writer.Write("C,");
                break;
            default:
                Writer.Write("N,");
                break;
        }
        switch (person.EmploymentStatus)
        {
            case TTSEmploymentStatus.FullTime:
                Writer.Write("F,");
                break;
            case TTSEmploymentStatus.PartTime:
                Writer.Write("P,");
                break;
            default:
                Writer.Write("O,");
                break;
        }
        switch (person.Occupation)
        {
            case Occupation.Professional:
                Writer.Write("P,");
                break;
            case Occupation.Office:
                Writer.Write("G,");
                break;
            case Occupation.Retail:
                Writer.Write("S,");
                break;
            case Occupation.Manufacturing:
                Writer.Write("M,");
                break;
            default:
                Writer.Write("O,");
                break;
        }
        var workZone = person.EmploymentZone;
        var schoolZone = person.SchoolZone;
        var personExpanded = person.ExpansionFactor;
        if (!IsExternal(workZone))
        {
            switch (person.EmploymentStatus)
            {
                case TTSEmploymentStatus.FullTime:
                    switch (person.Occupation)
                    {
                        case Occupation.Professional:
                            WorkersPF[zone] += personExpanded;
                            WorkersCatPF[zone][workerCategory] += personExpanded;
                            break;
                        case Occupation.Office:
                            WorkersGF[zone] += personExpanded;
                            WorkersCatGF[zone][workerCategory] += personExpanded;
                            break;
                        case Occupation.Retail:
                            WorkersSF[zone] += personExpanded;
                            WorkersCatSF[zone][workerCategory] += personExpanded;
                            break;
                        case Occupation.Manufacturing:
                            WorkersMF[zone] += personExpanded;
                            WorkersCatMF[zone][workerCategory] += personExpanded;
                            break;
                    }
                    break;
                case TTSEmploymentStatus.PartTime:
                    switch (person.Occupation)
                    {
                        case Occupation.Professional:
                            WorkersPP[zone] += personExpanded;
                            WorkersCatPP[zone][workerCategory] += personExpanded;
                            break;
                        case Occupation.Office:
                            WorkersGP[zone] += personExpanded;
                            WorkersCatGP[zone][workerCategory] += personExpanded;
                            break;
                        case Occupation.Retail:
                            WorkersSP[zone] += personExpanded;
                            WorkersCatSP[zone][workerCategory] += personExpanded;
                            break;
                        case Occupation.Manufacturing:
                            WorkersMP[zone] += personExpanded;
                            WorkersCatMP[zone][workerCategory] += personExpanded;
                            break;
                    }
                    break;
            }
        }
        Writer.Write(person.FreeParking ? "Y," : "N,");
        switch (person.StudentStatus)
        {
            case StudentStatus.FullTime:
                Writer.Write("F,");
                break;
            case StudentStatus.PartTime:
                Writer.Write("P,");
                break;
            default:
                Writer.Write("O,");
                break;
        }

        // we don't save employment or school zone
        if (IsExternal(workZone))
        {
            Writer.Write(workZone.ZoneNumber);
        }
        else
        {
            Writer.Write('0');
        }
        Writer.Write(',');
        if (IsExternal(schoolZone))
        {
            Writer.Write(schoolZone.ZoneNumber);
        }
        else
        {
            Writer.Write('0');
        }
        Writer.Write(',');
        Writer.WriteLine(person.ExpansionFactor);
    }

    private string BuildFileName(Occupation occ, TTSEmploymentStatus empStat)
    {
        var dirPath = WorkerForceDirectory.GetFilePath();
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
        using StreamWriter writer = new(BuildFileName(occupation, empStat));
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

        SaveWorkerCategoryData(zones, Occupation.Professional, TTSEmploymentStatus.FullTime, WorkersCatPF);
        SaveWorkerCategoryData(zones, Occupation.Office, TTSEmploymentStatus.FullTime, WorkersCatGF);
        SaveWorkerCategoryData(zones, Occupation.Retail, TTSEmploymentStatus.FullTime, WorkersCatSF);
        SaveWorkerCategoryData(zones, Occupation.Manufacturing, TTSEmploymentStatus.FullTime, WorkersCatMF);
        SaveWorkerCategoryData(zones, Occupation.Professional, TTSEmploymentStatus.PartTime, WorkersCatPP);
        SaveWorkerCategoryData(zones, Occupation.Office, TTSEmploymentStatus.PartTime, WorkersCatGP);
        SaveWorkerCategoryData(zones, Occupation.Retail, TTSEmploymentStatus.PartTime, WorkersCatSP);
        SaveWorkerCategoryData(zones, Occupation.Manufacturing, TTSEmploymentStatus.PartTime, WorkersCatMP);
    }

    [SubModelInformation(Required = true, Description = "The directory to store the worker category information to.")]
    public FileLocation WorkerCategoryDirectory;

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

    [SubModelInformation(Required = true, Description = "CSV (Zone,FutureYearPopulation)")]
    public FileLocation FutureYearPopulationByZone;

    [SubModelInformation(Required = true, Description = "The location to save the updated Person's file.")]
    public FileLocation NewPersonsFiles;

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
        WorkersCatPF = new float[zones.Length][];
        WorkersCatGF = new float[zones.Length][];
        WorkersCatSF = new float[zones.Length][];
        WorkersCatMF = new float[zones.Length][];
        WorkersCatPP = new float[zones.Length][];
        WorkersCatGP = new float[zones.Length][];
        WorkersCatSP = new float[zones.Length][];
        WorkersCatMP = new float[zones.Length][];
        for (int i = 0; i < zones.Length; i++)
        {
            WorkersCatPF[i] = new float[3];
            WorkersCatGF[i] = new float[3];
            WorkersCatSF[i] = new float[3];
            WorkersCatMF[i] = new float[3];
            WorkersCatPP[i] = new float[3];
            WorkersCatGP[i] = new float[3];
            WorkersCatSP[i] = new float[3];
            WorkersCatMP[i] = new float[3];
        }
        // Create our writer
        Writer = new StreamWriter(NewPersonsFiles);
        //HouseholdID	Zone	ExpansionFactor	DwellingType	NumberOfPersons	NumberOfVehicles
        Writer.WriteLine("HouseholdID,PersonNumber,Age,Sex,License,TransitPass,EmploymentStatus,Occupation,FreeParking,StudentStatus,EmploymentZone,SchoolZone,ExpansionFactor");
        // update the population forecasts by zone
        using CsvReader reader = new(FutureYearPopulationByZone);
        reader.LoadLine();
        while (reader.LoadLine(out int columns))
        {
            if (columns >= 2)
            {
                reader.Get(out int sparseZone, 0);
                int zone = zoneSystem.GetFlatIndex(sparseZone);
                if (zone >= 0)
                {
                    reader.Get(out float futurePopulation, 1);
                    ExpansionModiferByZone[zone] = futurePopulation / zones[zone].Population;
                    if (zones[zone].Population > 0)
                    {
                        if (float.IsNaN(ExpansionModiferByZone[zone]) | float.IsInfinity(ExpansionModiferByZone[zone]))
                        {
                            throw new XTMFRuntimeException(this, "Zone " + sparseZone + " ended up with an invalid (infinite) population in the future year forecast!");
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
        if (Root.Parallel)
        {
            error = "Parallel must be off in order to do a population update!";
            return false;
        }
        return true;
    }

    public void Dispose()
    {
        if (Writer != null)
        {
            Writer.Dispose();
            Writer = null;
        }
    }
}
