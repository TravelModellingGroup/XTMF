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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Tasha.Common;
using XTMF;
using TMG.Input;
using System.Collections;
using Datastructure;
using System.Runtime.CompilerServices;

namespace TMG.Tasha.MicrosimLoader
{
    [ModuleInformation(Description = "This module is designed to load the Households, Persons, and Trips from Microsim and pass them through the model.")]
    public class LoadHouseholdsFromMicrosim : IDataLoader<ITashaHousehold>, IDisposable
    {
        [RootModule]
        public ITashaRuntime Root;

        public bool OutOfData => false;

        public int Count { get; }

        public object SyncRoot => null;

        public bool IsSynchronized => false;

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new(5, 150, 50);

        public void CopyTo(ITashaHousehold[] array, int index)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {

        }

        public IEnumerator<ITashaHousehold> GetEnumerator()
        {
            return LoadMicrosim();
        }

        public void LoadData()
        {
            throw new NotImplementedException();
        }

        public void Reset()
        {
            throw new NotImplementedException();
        }

        public ITashaHousehold[] ToArray()
        {
            throw new NotImplementedException();
        }

        public bool TryAdd(ITashaHousehold item)
        {
            throw new NotImplementedException();
        }

        public bool TryTake(out ITashaHousehold item)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return LoadMicrosim();
        }

        [SubModelInformation(Required = true, Description = "The location of the Microsim households file.")]
        public FileLocation HouseholdFile;
        [SubModelInformation(Required = true, Description = "The location of the Microsim Persons file.")]
        public FileLocation PersonFile;
        [SubModelInformation(Required = false, Description = "The location of the Microsim Trips file.")]
        public FileLocation TripFile;
        [SubModelInformation(Required = false, Description = "The location of the Microsim Modes file.")]
        public FileLocation ModeFile;

        private IEnumerator<ITashaHousehold> LoadMicrosim()
        {
            var zoneSystem = SetupZoneSystem();
            HashSet<MicrosimHousehold> householdRecords = null;
            Dictionary<int, List<MicrosimPerson>> personsRecords = null;
            Dictionary<(int householdID, int personID), List<MicrosimTrip>> tripRecords = null;
            Dictionary<(int householdID, int personID, int tripID), MicrosimTripMode> modeRecords = null;
            // Load the microsim in parallel
            Parallel.Invoke(
            () => householdRecords = MicrosimHousehold.LoadHouseholds(this, HouseholdFile),
            () => personsRecords = MicrosimPerson.LoadPersons(this, PersonFile),
            () =>
            {
                if (TripFile != null)
                {
                    tripRecords = MicrosimTrip.LoadTrips(this, TripFile);
                }
            },
            () =>
            {
                if (ModeFile != null)
                {
                    modeRecords = MicrosimTripMode.LoadModes(this, ModeFile);
                }
            });

            // Load in the households, in order, and send them off for processing
            foreach (var household in householdRecords.OrderBy(h => h.HouseholdID))
            {
                if (!personsRecords.TryGetValue(household.HouseholdID, out var persons))
                {
                    throw new XTMFRuntimeException(this, $"Unable to find any person records for household {household.HouseholdID}!");
                }
                var ret = ConstructHousehold(household, persons.Count, zoneSystem);
                for (int i = 0; i < persons.Count; i++)
                {
                    var person = ConstructPerson(persons[i], ret, zoneSystem);
                    ret.Persons[i] = person;
                    if (tripRecords != null)
                    {
                        // if there are trip records for this person, process them
                        if (tripRecords.TryGetValue((persons[i].HouseholdID, persons[i].PersonID), out var trips))
                        {
                            TripChain tc = null;
                            foreach(var trip in trips.OrderBy(t => t.TripID))
                            {
                                // If there is another trip and we don't have an active trip chain, create a new one.
                                if(tc == null)
                                {
                                    person.TripChains.Add(tc = TripChain.MakeChain(person));
                                }
                                if(!modeRecords.TryGetValue((trip.HouseholdID, trip.PersonID, trip.TripID), out var tripModeData))
                                {
                                    throw new XTMFRuntimeException(this, $"Unable to find mode data for the trip record {trip.HouseholdID}:{trip.PersonID}:{trip.TripID}!");
                                }
                                tc.Trips.Add(ConstructTrip(trip, tc, zoneSystem, tripModeData));
                                // If we are going back to the house then terminate the trip chain.
                                if(IsHouseholdActivityPurpose(trip.DestinationPurpose))
                                {
                                    tc = null;
                                }
                            }
                        }
                        else
                        {

                        }
                    }
                }
                RebuildJointTours(ret);
                ValdiateJointTours(ret);
                yield return ret;
            }
        }

        private void ValdiateJointTours(Household ret)
        {
            foreach(var p in ret.Persons)
            {
                foreach(var tc in p.TripChains)
                {
                    if(tc.JointTrip && !tc.JointTripRep && tc.GetRepTripChain == null)
                    {
                        throw new XTMFRuntimeException(this, "Found an invalid joint trip chain!");
                    }
                }
            }
        }

        private SparseArray<IZone> SetupZoneSystem()
        {
            IZoneSystem zoneSystem = Root.ZoneSystem;
            var ret = zoneSystem.ZoneArray;
            _roamingZoneNumber = zoneSystem.RoamingZoneNumber;
            _roamingZone = zoneSystem.Get(_roamingZoneNumber);
            return ret;
        }

        [RunParameter("Household Iterations", 10, "Set this to the same number of iterations that the mode choice algorithm will use.")]
        public int HouseholdIterations;

        private ITrip ConstructTrip(MicrosimTrip trip, TripChain tc, SparseArray<IZone> zoneSystem, MicrosimTripMode modeData)
        {
            IZone origin = GetZone(zoneSystem, trip.OriginZone, "origin");
            IZone destination = GetZone(zoneSystem, trip.DestinationZone, "destination");
            Activity purpose = GetTripPurpose(trip.DestinationPurpose);
            if(IsHouseholdActivityPurpose(trip.DestinationPurpose))
            {
                Time startTime = Time.FromMinutes(modeData.DepartureTime);
                var ret = HouseholdPurposeTrip.GetTrip(HouseholdIterations);
                ret.OriginalZone = origin;
                ret.DestinationZone = destination;
                ret.Purpose = purpose;
                ret.TripStartTime = startTime;
                ret.TripChain = tc;
                ret.TripNumber = trip.TripID;
                return ret;
            }
            else
            {
                Time startTime = Time.FromMinutes(modeData.ArrivalTime);
                var ret = ActivityPurposeTrip.GetTrip(HouseholdIterations);
                ret.OriginalZone = origin;
                ret.DestinationZone = destination;
                ret.Purpose = purpose;
                ret.ActivityStartTime = startTime;
                ret.TripChain = tc;
                ret.TripNumber = trip.TripID;
                return ret;
            }
        }

        private Activity GetTripPurpose(string purpose)
        {
            switch(purpose)
            {
                case "Home":
                    return Activity.Home;
                case "IndividualOther":
                    return Activity.IndividualOther;
                case "JointMarket":
                    return Activity.JointMarket;
                case "JointOther":
                    return Activity.JointOther;
                case "Market":
                    return Activity.Market;
                case "PrimaryWork":
                    return Activity.PrimaryWork;
                case "ReturnFromWork":
                    return Activity.ReturnFromWork;
                case "School":
                    return Activity.School;
                case "SecondaryWork":
                    return Activity.SecondaryWork;
                case "WorkBasedBusiness":
                    return Activity.WorkBasedBusiness;
                default:
                    throw new XTMFRuntimeException(this, $"Unknown activity purpose {purpose}!");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IZone GetZone(SparseArray<IZone> zoneSystem, int zoneNumber, string zoneType)
        {
            var flatIndex = zoneSystem.GetFlatIndex(zoneNumber);
            if(flatIndex < 0)
            {
                Throw();
            }
            return zoneSystem.GetFlatData()[flatIndex];
            // We move this out to a different method in order to allow the hot-path to inline.
            void Throw()
            {
                throw new XTMFRuntimeException(this, $"Unable to find a zone number {zoneNumber} for the trip's {zoneType}!");
            }
        }

        private IZone _roamingZone;
        private int _roamingZoneNumber;

        private Person ConstructPerson(MicrosimPerson person, Household household, SparseArray<IZone> zoneSystem)
        {
            var ret = new Person(household, person.PersonID, person.Age,
                                    ConvertOccupation(person.Occupation), ConvertEmploymentStatus(person.EmploymentStatus),
                                    ConvertStudentStatus(person.StudentStatus), person.License, IsFemale(person.Sex));
            ret.ExpansionFactor = person.Weight;
            ret.TransitPass = person.TransitPass ? TransitPass.Combination : TransitPass.None;
            if (person.WorkZone != 0)
            {
                if (person.WorkZone != _roamingZoneNumber)
                {
                    var index = zoneSystem.GetFlatIndex(person.WorkZone);
                    if (index < 0)
                    {
                        throw new XTMFRuntimeException(this, $"Unable to find a work zone of {person.WorkZone} in the zone system for the household {household.HouseholdId}'s person number {person.PersonID}!");
                    }
                    ret.EmploymentZone = zoneSystem.GetFlatData()[index];
                }
                else
                {
                    ret.EmploymentZone = _roamingZone;
                }
            }
            if (person.SchoolZone != 0)
            {
                var index = zoneSystem.GetFlatIndex(person.SchoolZone);
                if (index < 0)
                {
                    throw new XTMFRuntimeException(this, $"Unable to find a school zone of {person.SchoolZone} in the zone system for the household {household.HouseholdId}'s person number {person.PersonID}!");
                }
                ret.SchoolZone = zoneSystem.GetFlatData()[index];
            }
            return ret;
        }

        private Occupation ConvertOccupation(char occupation)
        {
            switch (occupation)
            {
                case 'G':
                case 'g':
                    return Occupation.Office;
                case 'M':
                case 'm':
                    return Occupation.Manufacturing;
                case 'P':
                case 'p':
                    return Occupation.Professional;
                case 'S':
                case 's':
                    return Occupation.Retail;
                default:
                    return Occupation.NotEmployed;
            }
        }

        private TTSEmploymentStatus ConvertEmploymentStatus(char employmentStatus)
        {
            switch (employmentStatus)
            {
                case 'F':
                case 'f':
                    return TTSEmploymentStatus.FullTime;
                case 'P':
                case 'p':
                    return TTSEmploymentStatus.PartTime;
                case 'H':
                case 'h':
                    return TTSEmploymentStatus.WorkAtHome_FullTime;
                case 'J':
                case 'j':
                    return TTSEmploymentStatus.WorkAtHome_PartTime;
                default:
                    return TTSEmploymentStatus.NotEmployed;
            }
        }

        private StudentStatus ConvertStudentStatus(char studentStatus)
        {
            switch (studentStatus)
            {
                case 'F':
                case 'f':
                    return StudentStatus.FullTime;
                case 'P':
                case 'p':
                    return StudentStatus.PartTime;
                default:
                    return StudentStatus.NotStudent;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFemale(char sex)
        {
            return char.ToLowerInvariant(sex) != 'm';
        }

        private Household ConstructHousehold(MicrosimHousehold household, int numberOfPersons, SparseArray<IZone> zoneSystem)
        {
            var homeZoneIndex = zoneSystem.GetFlatIndex(household.HomeZone);
            if(homeZoneIndex < 0)
            {
                throw new XTMFRuntimeException(this, $"Unable to find a home zone of {household.HomeZone} in the zone system while reading in household record {household.HouseholdID}!");
            }
            var vehicles = new IVehicle[household.Vehicles];
            for (int i = 0; i < vehicles.Length; i++)
            {
                vehicles[i] = Vehicle.MakeVehicle(Root.AutoType);
            }
            return new Household(household.HouseholdID, new ITashaPerson[numberOfPersons], vehicles, household.Weight, zoneSystem.GetFlatData()[homeZoneIndex])
            {
                DwellingType = (DwellingType)(household.DwellingType),
                IncomeClass = household.IncomeClass
            };
        }

        private static bool IsHouseholdActivityPurpose(string purpose)
        {
            return purpose == "Home" || purpose == "ReturnFromWork";
        }

        private void RebuildJointTours(Household ret)
        {
            var persons = ret.Persons;
            int jointTourNumber = 1;
            for (int personIndex = 0; personIndex < persons.Length - 1; personIndex++)
            {
                for(int tcIndex = 0; tcIndex < persons[personIndex].TripChains.Count; tcIndex++)
                {
                    var chain = persons[personIndex].TripChains[tcIndex];
                    if(chain.JointTrip)
                    {
                        continue;
                    }
                    for (int otherIndex = personIndex + 1; otherIndex < persons.Length; otherIndex++)
                    {
                        for (int otherTcIndex = 0; otherTcIndex < persons[otherIndex].TripChains.Count; otherTcIndex++)
                        {
                            var otherChain = persons[otherIndex].TripChains[otherTcIndex];
                            if(AreTogether(chain, otherChain))
                            {
                                var tourNum = jointTourNumber;
                                if(!chain.JointTrip)
                                {
                                    ((TripChain)chain).JointTripID = ((TripChain)otherChain).JointTripID = tourNum;
                                    ((TripChain)chain).JointTripRep = true;
                                    jointTourNumber++;
                                }
                                ((TripChain)otherChain).JointTripID = chain.JointTripID;
                                ((TripChain)otherChain).GetRepTripChain = chain;
                                ((TripChain)otherChain).JointTripRep = false;
                            }
                        }
                    }
                }
            }
        }

        private static bool AreTogether(ITripChain f, ITripChain s)
        {
            if (f.Trips.Count != s.Trips.Count) return false;
            var fTrips = f.Trips;
            var sTrips = s.Trips;
            for (int i = 0; i < fTrips.Count; i++)
            {
                if (!AreTogether(fTrips[i], sTrips[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool AreTogether(ITrip f, ITrip s)
        {
            return (f.TripStartTime == s.TripStartTime)
                 & (f.Purpose == s.Purpose)
                 & (f.Purpose == Activity.Home | f.Purpose == Activity.JointMarket | f.Purpose == Activity.JointOther)
                 & (f.OriginalZone.ZoneNumber == s.OriginalZone.ZoneNumber)
                 & (f.DestinationZone.ZoneNumber == s.DestinationZone.ZoneNumber);
        }

        public bool RuntimeValidation(ref string error)
        {
            if((TripFile == null) != (ModeFile == null))
            {
                error = $"In {Name} either both the Trip file and Mode file need to be selected for or not.";
                return false;
            }
            return true;
        }
    }
}
