/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Threading.Tasks;
using Tasha.Common;
using TMG.Input;
using System.IO;
using System.Threading;
using XTMF;
using Datastructure;

namespace Tasha.Validation.TripExtraction
{
    [ModuleInformation(
        Description =
        @"This module is designed to export trip records where for persons within the selected household zones and then further restricted by employment zones."
        )]
    public class ExtractSelectedDemographicTripRecords : IPostHouseholdIteration, IDisposable
    {
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

        private SparseTwinIndex<float> ZoneDistances;

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
                            var empZone = person.EmploymentZone;
                            var employmentZone = empZone == null ? 0 : empZone.ZoneNumber;
                            if (SelectedEmploymentZones.Contains(employmentZone))
                            {
                                foreach (var tripChain in person.TripChains)
                                {
                                    var tripNumber = 1;
                                    var numberOfWorkTrips = tripChain.Trips.Count(trip => trip.Purpose == Activity.PrimaryWork || trip.Purpose == Activity.SecondaryWork || trip.Purpose == Activity.WorkBasedBusiness);
                                    var numberOfTripInTour = tripChain.Trips.Count;
                                    foreach (var trip in tripChain.Trips)
                                    {
                                        SaveTrip(trip, householdNumber, personNumber, tripNumber, numberOfWorkTrips, numberOfTripInTour);
                                        tripNumber++;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {

        }


        private void WriteHeader()
        {
            var allModes = Root.AllModes;
            Writer.Write("HouseholdID,PersonID,TripNumber,OriginZone,DestinationZone,Purpose,TripStartTime,ActivityStartTime,Distance,NumberOfWorkTrips,NumberOfTripsInTour,");
            Writer.WriteLine(string.Join(",", allModes.Select(m => m.ModeName)));
        }

        private void SaveTrip(ITrip trip, int householdNumber, int personNumber, int tripNumber, int numberOfWorkTrips, int numberOfTripsInTour)
        {
            var writer = Writer;
            writer.Write(householdNumber);
            writer.Write(',');
            writer.Write(personNumber);
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
            writer.Write(',');
            writer.Write(GetTripDistance(trip));
            writer.Write(',');
            writer.Write(numberOfWorkTrips);
            writer.Write(',');
            writer.Write(numberOfTripsInTour);
            var modesChosen = trip.ModesChosen;
            for (int i = 0; i < AllModes.Length; i++)
            {
                writer.Write(',');
                writer.Write(modesChosen.Count(m => m == AllModes[i]));
            }
            writer.WriteLine();
        }

        private float GetTripDistance(ITrip trip)
        {
            var origin = trip.OriginalZone.ZoneNumber;
            var dest = trip.DestinationZone.ZoneNumber;
            return ZoneDistances[origin, dest];
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
                ZoneDistances = Root.ZoneSystem.Distances;
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

}
