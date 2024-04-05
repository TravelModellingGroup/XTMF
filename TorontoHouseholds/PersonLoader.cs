/*
    Copyright 2014-2019 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Threading.Tasks;
using Datastructure;
using Tasha.Common;
using TMG.Input;
using XTMF;

namespace TMG.Tasha;

[ModuleInformation(Description = "This module is designed to load person records given the household that the people live in from TTS like data.")]
public class PersonLoader : IDatachainLoader<ITashaHousehold, ITashaPerson>, IDisposable
{
    [RunParameter("FileName", "Households/People.csv", "The file name of the csv file that we will load people from.")]
    public string FileName;

    [RunParameter("Header", false, "Is there a header in the CSV file?")]
    public bool Header;

    [RunParameter("Age", 2, "The 0 indexed column that represents a person's age.")]
    public int PersonAgeCol;

    [RunParameter("DriversLicence", 4, "The 0 indexed column that represents if a person has a driver's license.")]
    public int PersonDriversLicenceCol;

    [RunParameter("EmploymentStatus", 6, "The 0 indexed column that represents a person's employment status.")]
    public int PersonEmploymentStatusCol;

    [RunParameter("EmploymentZone", 11, "The 0 indexed column that represents a person's work zone.")]
    public int PersonEmploymentZoneCol;

    [RunParameter("ExpansionFactor", -1, "The 0 indexed column that represents a person's expansion factor, set to -1 to use the household expansion factor.")]
    public int PersonExpansionFactorCol;

    [RunParameter("FreeParking", 8, "The 0 indexed column that represents a person receives free parking for work.")]
    public int PersonFreeParkingCol;

    [RunParameter("Gender", 3, "The 0 indexed column that represents a person's gender (M/F).")]
    public int PersonGenderCol;

    [RunParameter("Household ID", 0, "The 0 indexed column that represents a person's Household ID.")]
    public int PersonHouseholdID;

    [RunParameter("ID", 1, "The 0 indexed column that represents a person's ID.")]
    public int PersonIDCol;

    [RunParameter("Occupation", 7, "The 0 indexed column that represents a person's occupation.")]
    public int PersonOccupationCol;

    [RunParameter("Student", 9, "The 0 indexed column that represents a person is a student.")]
    public int PersonStudentCol;

    [RunParameter("StudentZone", 14, "The 0 indexed column that represents a person's school zone.")]
    public int PersonStudentZoneCol;

    [RunParameter("TransitPass", 5, "The 0 indexed column that represents a person has a transit pass.")]
    public int PersonTransitPassCol;

    [SubModelInformation(Required = false, Description = "Provides a linkage between home zone and school zone.")]
    public ICalculation<ITashaPerson, IZone> PlaceOfResidencePlaceOfSchool;

    [SubModelInformation(Required = false, Description = "Provides a linkage between home zone and work zone.")]
    public ICalculation<ITashaPerson, IZone> PlaceOfResidencePlaceOfWork;

    [RootModule]
    public ITashaRuntime TashaRuntime;

    [SubModelInformation(Description = "The loader for trips and their chains, only include this if you are not using a scheduler.", Required = false)]
    public IDatachainLoader<ITashaPerson, ITripChain> TripchainLoader;

    [RunParameter("Unknown Zone#", 9999, "The zone number representing a zone that we don't know about")]
    public int UnknownZoneNumber;

    [SubModelInformation(Required = false, Description = "A model alternative to compute if the person should use a driver's license.")]
    public ICalculation<ITashaPerson, bool> DriverLicenseModel;

    [SubModelInformation(Required = false, Description = "An alternative way of specifying the person file location.")]
    public FileLocation PersonFile;

    private bool ContainsData;

    private CsvReader Reader;

    ~PersonLoader()
    {
        Dispose(false);
    }

    public string Name
    {
        get;
        set;
    }

    public bool OutOfData
    {
        get { return false; }
    }

    public float Progress
    {
        get { return 0; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return new Tuple<byte, byte, byte>(100, 200, 100); }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool all)
    {
        Unload();
    }

    public void Unload()
    {
        Reader?.Close();
        Reader = null;
        PlaceOfResidencePlaceOfSchool?.Unload();
        PlaceOfResidencePlaceOfWork?.Unload();
        TripchainLoader?.Unload();
        DriverLicenseModel?.Unload();
        Person.ReleasePersonPool();
    }

    private void FinishHousehold(ITashaHousehold household, List<ITashaPerson> persons)
    {
        ITashaPerson[] personArray = household.Persons;
        if (personArray == null || personArray.Length != persons.Count)
        {
            ((Household)household).Persons = personArray = persons.ToArray();
        }
        else
        {
            for (int i = 0; i < personArray.Length; i++)
            {
                personArray[i] = persons[i];
            }
        }
        // first assign school zones
        if(PlaceOfResidencePlaceOfSchool != null)
        {
            for(int i = 0; i < personArray.Length; i++)
            {
                var p = (Person) personArray[i];
                if(p.StudentStatus == StudentStatus.FullTime | p.StudentStatus == StudentStatus.PartTime)
                {
                    p.SchoolZone = PlaceOfResidencePlaceOfSchool.ProduceResult(p);
                }
            }
        }
        // afterwards assign work zones
        if(PlaceOfResidencePlaceOfWork != null)
        {
            for(int i = 0; i < personArray.Length; i++)
            {
                var p = (Person) personArray[i];

                if((p.EmploymentStatus == TTSEmploymentStatus.FullTime | p.EmploymentStatus == TTSEmploymentStatus.PartTime))
                {
                    p.EmploymentZone = PlaceOfResidencePlaceOfWork.ProduceResult(p);
                }
            }
        }
        if (DriverLicenseModel != null)
        {
            foreach (var p in personArray)
            {
                ((Person)p).Licence = DriverLicenseModel.ProduceResult(p);
            }
        }
    }

    public bool Load(ITashaHousehold household)
    {
        if(Reader == null)
        {
            Parallel.Invoke(() => PlaceOfResidencePlaceOfSchool?.Load(),
                () => PlaceOfResidencePlaceOfWork?.Load(),
                () => DriverLicenseModel?.Load()
            );
            Reader = new CsvReader(PersonFile ?? System.IO.Path.Combine(TashaRuntime.InputBaseDirectory, FileName));
            if(Header)
            {
                Reader.LoadLine();
            }
        }
        if(!ContainsData)
        {
            if(Reader.LoadLine() == 0)
            {
                return false;
            }
        }
        List<ITashaPerson> persons = [];
        while (true)
        {
            Reader.Get(out int tempInt, PersonHouseholdID);
            if(tempInt != household.HouseholdId)
            {
                if(tempInt < household.HouseholdId)
                {
                    if(Reader.LoadLine() == 0)
                    {
                        return false;
                    }
                    continue;
                }
                ContainsData = true;
                FinishHousehold(household, persons);
                return true;
            }
            Person p = Person.GetPerson();
            p.Household = household;
            Reader.Get(out tempInt, PersonIDCol);
            p.Id = tempInt;
            Reader.Get(out tempInt, PersonAgeCol);
            p.Age = tempInt;
            Reader.Get(out char tempChar, PersonGenderCol);
            
            p.Female = (tempChar == 'F') | (tempChar == 'f');
            Reader.Get(out tempChar, PersonTransitPassCol);
            p.TransitPass = (TransitPass)tempChar;

            Reader.Get(out tempChar, PersonOccupationCol);
            p.Occupation = GetOccupation(tempChar);
            if(p.Occupation == Occupation.NotEmployed)
            {
                p.EmploymentStatus = TTSEmploymentStatus.NotEmployed;
            }
            else
            {
                Reader.Get(out tempChar, PersonEmploymentStatusCol);
                p.EmploymentStatus = GetEmploymentStatus(tempChar);
            }
            Reader.Get(out tempChar, PersonFreeParkingCol);
            p.FreeParking = tempChar == 'Y';
            Reader.Get(out tempChar, PersonStudentCol);
            p.StudentStatus = GetStudentStatus(tempChar);
            // check to see if we should load in the school zone directly or just call our PoRPoS model to give us a zone for this student
            if(p.StudentStatus == StudentStatus.FullTime | p.StudentStatus == StudentStatus.PartTime)
            {
                Reader.Get(out tempInt, PersonStudentZoneCol);
                p.SchoolZone = tempInt != 0 ? (tempInt == UnknownZoneNumber ? p.Household.HomeZone : TashaRuntime.ZoneSystem.ZoneArray[tempInt]) : null;
            }
            if((p.EmploymentStatus == TTSEmploymentStatus.FullTime | p.EmploymentStatus == TTSEmploymentStatus.PartTime))
            {
                Reader.Get(out tempInt, PersonEmploymentZoneCol);
                var employmentZone = tempInt != 0 ? (tempInt == UnknownZoneNumber ? p.Household.HomeZone : TashaRuntime.ZoneSystem.Get(tempInt)) : null;
                p.EmploymentZone = employmentZone;
            }
            else if(p.EmploymentStatus == TTSEmploymentStatus.WorkAtHome_FullTime | p.EmploymentStatus == TTSEmploymentStatus.WorkAtHome_PartTime)
            {
                p.EmploymentZone = household.HomeZone;
            }
            else
            {
                p.Occupation = Occupation.NotEmployed;
            }

            TripchainLoader?.Load(p);
            if(PersonExpansionFactorCol < 0)
            {
                p.ExpansionFactor = household.ExpansionFactor;
            }
            else
            {
                Reader.Get(out float tempFloat, PersonExpansionFactorCol);
                p.ExpansionFactor = tempFloat;
            }
            if (DriverLicenseModel == null)
            {
                Reader.Get(out tempChar, PersonDriversLicenceCol);
                p.Licence = (tempChar == 'Y');
            }
            persons.Add(p);
            if(Reader.LoadLine() == 0)
            {
                ContainsData = false;
                FinishHousehold(household, persons);
                return true;
            }
        }
    }

    public void Reset()
    {
        TripchainLoader?.Reset();
        if(DriverLicenseModel != null)
        {
            DriverLicenseModel.Unload();
            DriverLicenseModel.Load();
        }
        if (PlaceOfResidencePlaceOfSchool != null)
        {
            PlaceOfResidencePlaceOfSchool.Unload();
            PlaceOfResidencePlaceOfSchool.Load();

        }
        if(PlaceOfResidencePlaceOfWork != null)
        { 
            PlaceOfResidencePlaceOfWork.Unload();
            PlaceOfResidencePlaceOfWork.Load();
        }
        if(Reader != null)
        {
            ContainsData = false;
            Reader.Reset();
            if(Header)
            {
                Reader.LoadLine();
            }
            ContainsData = false;
        }
    }

    /// <summary>
    /// This is called before the start method as a way to pre-check that all of the parameters that are selected
    /// are in fact valid for this module.
    /// </summary>
    /// <param name="error">A string that should be assigned a detailed error</param>
    /// <returns>If the validation was successful or if there was a problem</returns>
    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    private static TTSEmploymentStatus GetEmploymentStatus(char status)
    {
        switch(status)
        {
            case 'O':
                return TTSEmploymentStatus.NotEmployed;
            case 'F':
                return TTSEmploymentStatus.FullTime;
            case 'P':
                return TTSEmploymentStatus.PartTime;
            case 'J':
                return TTSEmploymentStatus.WorkAtHome_PartTime;
            case 'H':
                return TTSEmploymentStatus.WorkAtHome_FullTime;
            default:
                return TTSEmploymentStatus.Unknown;
        }
    }

    private static Occupation GetOccupation(char occ)
    {
        switch(occ)
        {
            case 'G':
                return Occupation.Office;
            case 'M':
                return Occupation.Manufacturing;
            case 'P':
                return Occupation.Professional;
            case 'S':
                return Occupation.Retail;
            case 'O':
                return Occupation.NotEmployed;
        }
        return Occupation.Unknown;
    }

    private static StudentStatus GetStudentStatus(char status)
    {
        switch(status)
        {
            case 'O':
                return StudentStatus.NotStudent;
            case 'S':
            case 'F':
                return StudentStatus.FullTime;
            case 'P':
                return StudentStatus.PartTime;
            default:
                return StudentStatus.Unknown;
        }
    }
}