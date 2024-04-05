/*
    Copyright 2021 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections.Generic;
using System.IO;
using XTMF;
using TMG.Input;
using Datastructure;

namespace TMG.Tasha.MicrosimLoader;

/// <summary>
/// A record storing the results of the Person data from Microsim
/// </summary>
internal sealed class MicrosimPerson
{
    /// <summary>
    /// The unique identifier for the household
    /// </summary>
    internal readonly int HouseholdID;
    /// <summary>
    /// The unique identifier for the person within the household
    /// </summary>
    internal readonly int PersonID;
    /// <summary>
    /// The age of the person
    /// </summary>
    internal readonly int Age;
    /// <summary>
    /// The sex of the person, M/F
    /// </summary>
    internal readonly char Sex;
    /// <summary>
    /// True if the person has a driver's license
    /// </summary>
    internal readonly bool License;
    /// <summary>
    /// True if the person has a transit pass, False otherwise
    /// </summary>
    internal readonly bool TransitPass;
    /// <summary>
    /// The employment status as a character based on TTS
    /// </summary>
    internal readonly char EmploymentStatus;
    /// <summary>
    /// The occupation code as a character based on TTS
    /// </summary>
    internal readonly char Occupation;
    /// <summary>
    /// True if the person has free parking at work
    /// </summary>
    internal readonly bool FreeParking;
    /// <summary>
    /// The student status based on TTS
    /// </summary>
    internal readonly char StudentStatus;
    /// <summary>
    /// The primary work zone of the person, 0 if none.
    /// </summary>
    internal readonly int WorkZone;
    /// <summary>
    /// The school zone of the person, 0 if none.
    /// </summary>
    internal readonly int SchoolZone;
    /// <summary>
    /// The expansion factor of the record
    /// </summary>
    internal readonly float Weight;

    private MicrosimPerson(int householdID, int personID, int age, char sex, bool license, bool transitPass, char employmentStatus, char occupation, bool freeParking, char studentStatus, int workZone, int schoolZone, float weight)
    {
        HouseholdID = householdID;
        PersonID = personID;
        Age = age;
        Sex = sex;
        License = license;
        TransitPass = transitPass;
        EmploymentStatus = employmentStatus;
        Occupation = occupation;
        FreeParking = freeParking;
        StudentStatus = studentStatus;
        WorkZone = workZone;
        SchoolZone = schoolZone;
        Weight = weight;
    }

    /// <summary>
    /// Read in a dictionary of person records from the given Microsim file
    /// </summary>
    /// <param name="callingModule">The module invoking the call</param>
    /// <param name="personsFile">The location of the persons file to load.</param>
    /// <returns>A dictionary of all of the loaded persons indexed by the combination of the household and person ids.</returns>
    internal static Dictionary<int, List<MicrosimPerson>> LoadPersons(IModule callingModule, FileLocation personsFile)
    {
        var fileInfo = new FileInfo(personsFile.GetFilePath());
        if (!fileInfo.Exists)
        {
            throw new XTMFRuntimeException(callingModule, $"The file \"{fileInfo.FullName}\" does not exist!");
        }
        var ret = new Dictionary<int, List<MicrosimPerson>>(100000);
        using(var reader = new CsvReader(fileInfo))
        {
            // burn header
            reader.LoadLine();
            while(reader.LoadLine(out int columns))
            {
                if (columns >= 13)
                {
                    reader.Get(out int householdID, 0);
                    reader.Get(out int personID, 1);
                    reader.Get(out int age, 2);
                    reader.Get(out char sex, 3);
                    reader.Get(out bool license, 4);
                    reader.Get(out bool transitPass, 5);
                    reader.Get(out char employmentStatus, 6);
                    reader.Get(out char occupation, 7);
                    reader.Get(out bool freeParking, 8);
                    reader.Get(out char studentStatus, 9);
                    reader.Get(out int workZone, 10);
                    reader.Get(out int schoolZone, 11);
                    reader.Get(out float weight, 12);
                    if (!ret.TryGetValue(householdID, out var persons))
                    {
                        ret[householdID] = persons = new List<MicrosimPerson>(4);
                    }
                    persons.Add(new MicrosimPerson(householdID, personID, age, sex, license, transitPass, employmentStatus, occupation,
                        freeParking, studentStatus, workZone, schoolZone, weight));
                }
            }
        }
        return ret;
    }
}
