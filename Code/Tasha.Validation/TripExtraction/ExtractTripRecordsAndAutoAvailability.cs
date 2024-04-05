/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using Tasha.Common;
using TMG.Input;
using System.IO;
using XTMF;
using Datastructure;
using Tasha.XTMFModeChoice;

namespace Tasha.Validation.TripExtraction;


public sealed class ExtractTripRecordsAndAutoAvailability : IPostHouseholdIteration, IDisposable
{
    // Attachment 
    private const string AttachmentName = "TripChainNonVehiclePrefered";

    [RootModule]
    public ITashaRuntime Root;

    [SubModelInformation(Required = true, Description = "The location to save to.")]
    public FileLocation SaveTo;

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    [RunParameter("Employment Zones", "0", typeof(RangeSet), "The employment zones that should be selected for.")]
    public RangeSet SelectedEmploymentZones;

    [RunParameter("Household Zones", "0", typeof(RangeSet), "The household zones that should be selected for.")]
    public RangeSet SelectedHouseholdZones;

    StreamWriter Writer;
    private bool WriteThisIteration;

    public void HouseholdComplete(ITashaHousehold household, bool success)
    {
        if (WriteThisIteration)
        {
            if (SelectedHouseholdZones.Contains(household.HomeZone.ZoneNumber))
            {
                var householdNumber = household.HouseholdId;
                lock (this)
                {
                    int personNumber = 0;
                    foreach (var person in household.Persons)
                    {
                        personNumber++;
                        var preferedNonVehicle = (int[]) person[AttachmentName];
                        var empZone = person.EmploymentZone;
                        var employmentZone = empZone == null ? 0 : empZone.ZoneNumber;
                        if (SelectedEmploymentZones.Contains(employmentZone))
                        {
                            var tripNumber = 1;
                            var tripChainNumber = 1;
                            foreach (var tripChain in person.TripChains)
                            {
                                foreach (var trip in tripChain.Trips)
                                {
                                    SaveTrip(trip, householdNumber, personNumber, tripChainNumber, tripNumber++, preferedNonVehicle[tripChainNumber - 1]);
                                }
                                tripChainNumber++;
                            }
                        }
                    }
                }
            }
        }
    }

    public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
    {
        if (WriteThisIteration)
        {
            var householdData = (ModeChoiceHouseholdData) household["ModeChoiceData"];
            var people = household.Persons;
            for (int personNumber = 0; personNumber < people.Length; personNumber++)
            {
                var personData = householdData.PersonData[personNumber];
                var person = people[personNumber];
                var empZone = person.EmploymentZone;
                var employmentZone = empZone == null ? 0 : empZone.ZoneNumber;
                if (SelectedEmploymentZones.Contains(employmentZone))
                {
                    var tripChains = person.TripChains;
                    int[] amount;
                    if (hhldIteration == 0)
                    {
                        person.Attach(AttachmentName, (amount = new int[tripChains.Count]));
                    }
                    else
                    {
                        amount = (int[]) person[AttachmentName];
                    }
                    for (int i = 0; i < tripChains.Count; i++)
                    {
                        var tripChainData = personData.TripChainData[i];
                        var noVehicle = tripChainData.BestPossibleAssignmentForVehicleType[0];
                        var withVehicle = tripChainData.BestPossibleAssignmentForVehicleType[1];
                        if (withVehicle == null || noVehicle.U > withVehicle.U)
                        {
                            amount[i]++;
                        }
                    }
                }
            }
        }
    }


    private void WriteHeader()
    {
        var allModes = Root.AllModes;
        Writer.Write("HouseholdID,PersonID,TripChain,TripNumber,OriginZone,DestinationZone,Purpose,TripStartTime,ActivityStartTime,");
        Writer.Write(string.Join(",", allModes.Select(m => m.ModeName)));
        Writer.WriteLine(",PreferedNonVehicle");
    }

    private void SaveTrip(ITrip trip, int householdNumber, int personNumber, int tripChainNumber, int tripNumber, int preferedNonVehicle)
    {
        var writer = Writer;
        writer.Write(householdNumber);
        writer.Write(',');
        writer.Write(personNumber);
        writer.Write(',');
        writer.Write(tripChainNumber);
        writer.Write(',');
        writer.Write(tripNumber);
        writer.Write(',');
        writer.Write(trip.OriginalZone.ZoneNumber);
        writer.Write(',');
        writer.Write(trip.DestinationZone.ZoneNumber);
        writer.Write(',');
        writer.Write(GetPurposeName(trip.Purpose));
        writer.Write(',');
        writer.Write(trip.TripStartTime);
        writer.Write(',');
        writer.Write(trip.ActivityStartTime);
        var modesChosen = trip.ModesChosen;
        for (int i = 0; i < AllModes.Length; i++)
        {
            writer.Write(',');
            writer.Write(modesChosen.Count(m => m == AllModes[i]));
        }
        writer.Write(',');
        writer.Write(preferedNonVehicle);
        writer.WriteLine();
    }

    private string GetPurposeName(Activity purpose)
    {
        return Enum.GetName(typeof(Activity), purpose);
    }

    public void HouseholdStart(ITashaHousehold household, int householdIterations)
    {

    }

    public void IterationFinished(int iteration, int totalIterations)
    {
        if (WriteThisIteration)
        {
            Writer.Close();
            Writer = null;
        }
    }

    private ITashaMode[] AllModes;

    public void IterationStarting(int iteration, int totalIterations)
    {
        WriteThisIteration = iteration == totalIterations - 1;
        if (WriteThisIteration)
        {
            AllModes = Root.AllModes.ToArray();
            Writer = new StreamWriter(SaveTo);
            WriteHeader();
        }
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void Dispose()
    {
        if (Writer != null)
        {
            Writer.Close();
            Writer = null;
        }
    }
}
