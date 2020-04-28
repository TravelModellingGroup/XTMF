/*
    Copyright 2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF;
using Tasha.Common;
using Datastructure;
using TMG;
using TMG.Input;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace Tasha.Validation.ModeChoice
{
    [ModuleInformation(Description = "This module is designed to extract out data on each person, trip, trip mode assignment, and station choice from" +
        "GTAModelV4.0.  Data is only recorded for the final iteration.")]
    public sealed class ExtractPersonalAndTripRecords : IPostHouseholdIteration, IDisposable
    {
        /// <summary>
        /// This value lets us know if we should be collecting data for this iteration
        /// </summary>
        private bool _writeThisIteration;

        /// <summary>
        /// A local cache of the zone system
        /// </summary>
        private SparseArray<IZone> _zoneSystem;

        /// <summary>
        /// The data to use to compute the auto times for DAT
        /// </summary>
        private INetworkData _autoNetwork;

        /// <summary>
        /// The data to use to compute the transit times for DAT
        /// </summary>
        private ITripComponentData _transitNetwork;

        private struct PersonChain
        {
            internal ITashaPerson Person;
            internal ITripChain Chain;

            public PersonChain(ITashaPerson person, ITripChain chain)
            {
                Person = person;
                Chain = chain;
            }
        }

        /// <summary>
        /// A working-set of data to calculate the travel times for DAT properly
        /// </summary>
        private ConcurrentDictionary<ITashaHousehold, Dictionary<PersonChain, DATIterationInformation>> _activeDATData
            = new ConcurrentDictionary<ITashaHousehold, Dictionary<PersonChain, DATIterationInformation>>();

        private ConcurrentDictionary<ITashaHousehold, Dictionary<ITrip, PATIterationInformation>> _activePATData
            = new ConcurrentDictionary<ITashaHousehold, Dictionary<ITrip, PATIterationInformation>>();

        private ConcurrentDictionary<ITashaHousehold, Dictionary<ITrip, PassengerIterationInformation>> _activePassengerData
            = new ConcurrentDictionary<ITashaHousehold, Dictionary<ITrip, PassengerIterationInformation>>();

        [RootModule]
        public ITashaRuntime Root;

        [SubModelInformation(Required = false, Description = "The location to store individual household records.")]
        public FileLocation HouseholdRecords;

        [SubModelInformation(Required = true, Description = "The location to store individual person records.")]
        public FileLocation PersonRecords;

        [SubModelInformation(Required = true, Description = "The location to store records describing trips.")]
        public FileLocation TripRecords;

        [SubModelInformation(Required = true, Description = "The location to store the modes taken for each trip.")]
        public FileLocation TripModeRecords;

        [SubModelInformation(Required = true, Description = "The location to store station access information for trips.")]
        public FileLocation TripStationRecords;

        [SubModelInformation(Required = true, Description = "The location to store information for facilitate passenger trips.")]
        public FileLocation FacilitatePassengerRecords;


        [RunParameter("Compress Results", false, "Compress all of the result files.")]
        public bool CompressResults;

        [RunParameter("DAT Mode Name", "DAT", "The name of the drive access transit mode to extract all of the data from.  Set this to blank if there is not DAT mode in the model.")]
        public string DATModeName;

        [RunParameter("PAT Mode Name", "", "The name of the Passenger Access Transit mode to extract all of the data from.  Set this to blank if there is not PAT mode in the model.")]
        public string PATModeName;

        [RunParameter("PAT AccessStationZoneTag", "AccessStation", "The tag used to identify the access station that was selected.")]
        public string PATModeStationTag;

        [RunParameter("PET Mode Name", "", "The name of the Passenger Egress Transit mode to extract all of the data from.  Set this to blank if there is not PET mode in the model.")]
        public string PETModeName;

        [RunParameter("PET AccessStationZoneTag", "EgressStation", "The tag used to identify the access station that was selected.")]
        public string PETModeStationTag;

        [RunParameter("Passenger Mode Name", "Passenger", "The name of the passenger mode to extract all of the data from.")]
        public string PassengerModeName;

        [RunParameter("Auto Network Name", "Auto", "The name of the auto network to use in order to compute DAT auto trip times.")]
        public string AutoNetworkName;

        [RunParameter("Transit Network Name", "Transit", "The name of the transit network to use in order to compute DAT transit trip times.")]
        public string TransitNetworkName;

        [RunParameter("Export Times As Minutes", true, "Export the times as minutes since midnight instead of as a time stamp.")]
        public bool ExportTimesAsMinutes;



        /// <summary>
        /// Used to quickly check if an assigned mode is drive access transit
        /// </summary>
        private ITashaMode _dat;

        private ITashaMode _pat, _pet;

        private ITashaMode _passenger;

        public string Name { get; set; }

        public float Progress => 0.0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

        private struct HouseholdRecord
        {
            internal readonly int HouseholdID;
            internal readonly int HomeZone;
            internal readonly float ExpansionFactor;
            internal readonly int NumberOfPersons;
            internal readonly int DwellingType;
            internal readonly int NumberOfVehicles;
            internal readonly int IncomeClass;

            public HouseholdRecord(int householdID, int homeZone, float expansionFactor, int numberOfPersons, int dwellingType, int numberOfVehicles, int incomeClass)
            {
                HouseholdID = householdID;
                HomeZone = homeZone;
                ExpansionFactor = expansionFactor;
                NumberOfPersons = numberOfPersons;
                DwellingType = dwellingType;
                NumberOfVehicles = numberOfVehicles;
                IncomeClass = incomeClass;
            }
        }

        /// <summary>
        /// This is the queue used to process person data to disk
        /// </summary>
        private BlockingCollection<HouseholdRecord> _householdRecordQueue;

        /// <summary>
        /// Contains the data that will need to be stored to disk for Person information
        /// </summary>
        private struct PersonRecord
        {
            internal readonly int HouseholdID;
            internal readonly int PersonID;
            internal readonly int Age;
            internal readonly char Sex;
            internal readonly bool License;
            internal readonly bool TransitPass;
            internal readonly char EmploymentStatus;
            internal readonly char Occupation;
            internal readonly bool FreeParking;
            internal readonly char StudentStatus;
            internal readonly int WorkZone;
            internal readonly int SchoolZone;
            internal readonly float ExpFactor;

            public PersonRecord(int householdID, int personID, int age, char sex, bool license, bool transitPass, char employmentStatus,
                char occupation, bool freeParking, char studentStatus, int workZone, int schoolZone, float expFactor)
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
                ExpFactor = expFactor;
            }
        }

        /// <summary>
        /// This is the queue used to process person data to disk
        /// </summary>
        private BlockingCollection<PersonRecord> _personRecordQueue;

        /// <summary>
        /// Contains the data that will need to be stored to disk for Trip information
        /// </summary>
        private struct TripRecord
        {
            internal readonly int HouseholdID;
            internal readonly int PersonID;
            internal readonly int TripID;
            internal readonly int OriginZone;
            internal readonly string OriginActivity;
            internal readonly int DestinationZone;
            internal readonly string DestinationActivity;
            internal readonly float ExpFactor;

            public TripRecord(int householdID, int personID, int tripID, int originZone, string originActivity,
                int destinationZone, string destinationActivity, float expFactor)
            {
                HouseholdID = householdID;
                PersonID = personID;
                TripID = tripID;
                OriginZone = originZone;
                OriginActivity = originActivity;
                DestinationZone = destinationZone;
                DestinationActivity = destinationActivity;
                ExpFactor = expFactor;
            }
        }

        /// <summary>
        /// This is the queue used to process Trip data to disk
        /// </summary>
        private BlockingCollection<TripRecord> _tripRecordQueue;

        /// <summary>
        /// Contains the data that will need to be stored to disk for Mode assignment information
        /// </summary>
        private struct ModeRecord
        {
            internal readonly int HouseholdID;
            internal readonly int PersonID;
            internal readonly int TripID;
            internal readonly string Mode;
            internal readonly Time OriginDeparture;
            internal readonly Time DestinationArrivalTime;
            // The number of discrete selections of this mode for the given trip
            internal readonly int Weight;

            public ModeRecord(int householdID, int personID, int tripID, string mode,
                Time originDeparture, Time destinationArrivalTime, int weight)
            {
                HouseholdID = householdID;
                PersonID = personID;
                TripID = tripID;
                Mode = mode;
                OriginDeparture = originDeparture;
                DestinationArrivalTime = destinationArrivalTime;
                Weight = weight;
            }
        }

        /// <summary>
        /// This is the queue used to process person Mode Assignment to disk
        /// </summary>
        private BlockingCollection<ModeRecord> _modeRecordQueue;

        /// <summary>
        /// Contains the data that will need to be stored to disk for Station Choice information
        /// </summary>
        private struct StationRecord
        {
            public readonly int HouseholdID;
            public readonly int PersonID;
            public readonly int TripID;
            public readonly int StationID;
            public readonly bool ToTransit;
            // The number of discrete selections of this station for the given trip
            public readonly int Weight;

            public StationRecord(int householdID, int personID, int tripID, int stationID, bool toTransit, int weight)
            {
                HouseholdID = householdID;
                PersonID = personID;
                TripID = tripID;
                StationID = stationID;
                ToTransit = toTransit;
                Weight = weight;
            }
        }

        /// <summary>
        /// This is the queue used to process Station Choice data to disk
        /// </summary>
        private BlockingCollection<StationRecord> _stationRecordQueue;

        /// <summary>
        /// Contains the data that will need to be stored to disk for a Facilitate Passenger Trip 
        /// </summary>
        private struct FacilitatePassengerRecord
        {
            public readonly int HouseholdID;
            public readonly int PassengerID;
            public readonly int PassengerTripID;
            public readonly int DriverID;
            /// <summary>
            /// -1 if the trip is a pure facilitate passenger trip.
            /// </summary>
            public readonly int DriverTripID;
            public readonly float Weight;

            public FacilitatePassengerRecord(int householdID, int passengerID, int passengerTripID, int driverID, int driverTripID, float weight)
            {
                HouseholdID = householdID;
                PassengerID = passengerID;
                PassengerTripID = passengerTripID;
                DriverID = driverID;
                DriverTripID = driverTripID;
                Weight = weight;
            }
        }

        /// <summary>
        /// This is the queue used to process Station Choice data to disk
        /// </summary>
        private BlockingCollection<FacilitatePassengerRecord> _facilitatePassengerRecordQueue;

        /// <summary>
        /// The background task for processing the household queue to disk
        /// </summary>
        private Task _writeHouseholdOutput;

        /// <summary>
        /// The background task for processing the person queue to disk
        /// </summary>
        private Task _writePersonOutput;

        /// <summary>
        /// The background task for processing the trip queue to disk
        /// </summary>
        private Task _writeTripOutput;

        /// <summary>
        /// The background task for processing the mode assignment queue to disk
        /// </summary>
        private Task _writeModeOutput;

        /// <summary>
        /// The background task for processing the station choice queue to disk
        /// </summary>
        private Task _writeStationOutput;

        /// <summary>
        /// The background task for processing the facilitate passenger queue to disk
        /// </summary>
        private Task _writeFacilitatePassengerOutput;

        public void HouseholdComplete(ITashaHousehold household, bool success)
        {
            if (_writeThisIteration)
            {
                // if the household has data remove it from the active set since
                // we are now done with it.
                _activeDATData.TryRemove(household, out var datdata);
                _activePATData.TryRemove(household, out var patData);
                _activePassengerData.TryRemove(household, out var passData);
                var hhldID = household.HouseholdId;
                StoreHouseholdRecord(household);
                if (passData != null)
                {
                    StorePassengerTrips(hhldID, passData);
                }
                foreach (var person in household.Persons)
                {
                    var expFactor = person.ExpansionFactor;
                    StorePersonRecord(hhldID, person, expFactor);
                    int tripID = 1;
                    foreach (var tc in person.TripChains)
                    {
                        var previousActivity = Activity.Home;
                        foreach (var trip in tc.Trips)
                        {
                            StoreTripRecord(hhldID, person, expFactor, tripID, previousActivity, trip);
                            StoreTripModeRecords(hhldID, datdata, patData, person, expFactor, tripID, tc, trip);
                            previousActivity = trip.Purpose;
                            ++tripID;
                        }
                    }
                }
            }
        }

        private void StorePassengerTrips(int hhldID, Dictionary<ITrip, PassengerIterationInformation> passData)
        {
            foreach (var passTripData in passData)
            {
                var passengerTrip = passTripData.Key;
                var driverData = passTripData.Value;
                driverData.CreateRecords(_facilitatePassengerRecordQueue, hhldID, passengerTrip, this);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StoreTripModeRecords(int hhldID,
            Dictionary<PersonChain, DATIterationInformation> datData,
            Dictionary<ITrip, PATIterationInformation> patData,
            ITashaPerson person, float expFactor, int tripID, ITripChain chain, ITrip trip)
        {
            var query = from mc in trip.ModesChosen
                        group mc by mc into g
                        select new { Mode = g.Key, Count = g.Count() };
            DATIterationInformation chainData = null;
            PATIterationInformation data;
            datData?.TryGetValue(new PersonChain(person, chain), out chainData);
            bool storedPassengerTransit = false;
            if (trip.Purpose == Activity.Home)
            {
                foreach (var modeChoice in query)
                {
                    var mode = modeChoice.Mode;
                    if (mode == _dat && chainData != null)
                    {
                        // we need that defensive check in case of edge cases
                        var travelTime = chainData.ExpectedDATTravelTime(trip);
                        _modeRecordQueue.Add(new ModeRecord(hhldID, person.Id, tripID, mode.ModeName,
                        trip.TripStartTime, trip.TripStartTime + Time.FromMinutes(travelTime),
                        modeChoice.Count));
                        chainData.StoreStationRecords(_stationRecordQueue, hhldID, person.Id, tripID, trip, chainData);
                    }
                    else if(mode == _pat && patData.TryGetValue(trip, out data))
                    {
                        // compute the pat time
                        if (!storedPassengerTransit)
                        {
                            data.CreateStationRecords(_stationRecordQueue);
                            storedPassengerTransit = true;
                        }
                        var travelTime = data.ExpectedTravelTime(true);
                        _modeRecordQueue.Add(new ModeRecord(hhldID, person.Id, tripID, mode.ModeName,
                        trip.TripStartTime, trip.TripStartTime + Time.FromMinutes(travelTime),
                        modeChoice.Count));
                    }
                    else if(mode == _pet && patData.TryGetValue(trip, out data))
                    {
                        if (!storedPassengerTransit)
                        {
                            data.CreateStationRecords(_stationRecordQueue);
                            storedPassengerTransit = true;
                        }
                        var travelTime = data.ExpectedTravelTime(false);
                        _modeRecordQueue.Add(new ModeRecord(hhldID, person.Id, tripID, mode.ModeName,
                        trip.TripStartTime, trip.TripStartTime + Time.FromMinutes(travelTime),
                        modeChoice.Count));
                    }
                    else
                    {
                        _modeRecordQueue.Add(new ModeRecord(hhldID, person.Id, tripID, mode.ModeName,
                        trip.TripStartTime, trip.TripStartTime + mode.TravelTime(trip.OriginalZone, trip.DestinationZone, trip.TripStartTime),
                        modeChoice.Count));
                    }
                }
            }
            else
            {
                foreach (var modeChoice in query)
                {
                    var mode = modeChoice.Mode;
                    if (mode == _dat && chainData != null)
                    {
                        var travelTime = chainData.ExpectedDATTravelTime(trip);
                        _modeRecordQueue.Add(new ModeRecord(hhldID, person.Id, tripID, mode.ModeName,
                        trip.ActivityStartTime - Time.FromMinutes(travelTime), trip.ActivityStartTime,
                        modeChoice.Count));
                        chainData.StoreStationRecords(_stationRecordQueue, hhldID, person.Id, tripID, trip, chainData);
                    }
                    else if(mode == _pat && patData.TryGetValue(trip, out data))
                    {
                        // compute the pat time
                        if (!storedPassengerTransit)
                        {
                            data.CreateStationRecords(_stationRecordQueue);
                            storedPassengerTransit = true;
                        }
                        var travelTime = data.ExpectedTravelTime(true);
                        _modeRecordQueue.Add(new ModeRecord(hhldID, person.Id, tripID, mode.ModeName,
                        trip.ActivityStartTime - Time.FromMinutes(travelTime), trip.ActivityStartTime,
                        modeChoice.Count));
                    }
                    else if(mode == _pet && patData.TryGetValue(trip, out data))
                    {
                        // compute the pet time
                        if (!storedPassengerTransit)
                        {
                            data.CreateStationRecords(_stationRecordQueue);
                            storedPassengerTransit = true;
                        }
                        var travelTime = data.ExpectedTravelTime(false);
                        _modeRecordQueue.Add(new ModeRecord(hhldID, person.Id, tripID, mode.ModeName,
                        trip.ActivityStartTime - Time.FromMinutes(travelTime), trip.ActivityStartTime,
                        modeChoice.Count));
                    }
                    else
                    {
                        _modeRecordQueue.Add(new ModeRecord(hhldID, person.Id, tripID, mode.ModeName,
                        trip.ActivityStartTime - mode.TravelTime(trip.OriginalZone, trip.DestinationZone, trip.TripStartTime), trip.ActivityStartTime,
                        modeChoice.Count));
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StoreTripRecord(int hhldID, ITashaPerson person, float expFactor, int tripID, Activity previousActivity, ITrip trip)
        {
            _tripRecordQueue.Add(new TripRecord(hhldID, person.Id, tripID,
                                            trip.OriginalZone.ZoneNumber, GetActivityChar(previousActivity),
                                            trip.DestinationZone.ZoneNumber, GetActivityChar(trip.Purpose), expFactor));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetActivityChar(Activity purpose)
        {
            switch (purpose)
            {
                case Activity.PrimaryWork:
                    return "PrimaryWork";
                case Activity.SecondaryWork:
                    return "SecondaryWork";
                case Activity.ReturnFromWork:
                    return "ReturnFromWork";
                case Activity.WorkBasedBusiness:
                    return "WorkBasedBusiness";
                case Activity.School:
                    return "School";
                case Activity.JointOther:
                    return "JointOther";
                case Activity.IndividualOther:
                    return "IndividualOther";
                case Activity.Market:
                    return "Market";
                case Activity.JointMarket:
                    return "JointMarket";
                case Activity.Home:
                    return "Home";
                default:
                    return "UnkownActivity";
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StoreHouseholdRecord(ITashaHousehold household)
        {
            _householdRecordQueue?.Add(new HouseholdRecord(household.HouseholdId, household.HomeZone?.ZoneNumber ?? 0, household.ExpansionFactor, household.Persons.Length,
                GetDwellingInt(household.DwellingType), household.Vehicles.Length, household.IncomeClass));
        }

        private int GetDwellingInt(DwellingType dwellingType)
        {
            switch(dwellingType)
            {
                case DwellingType.House:
                    return 1;
                case DwellingType.Apartment:
                    return 2;
                case DwellingType.Townhouse:
                    return 3;
                default:
                    return 9;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void StorePersonRecord(int hhldID, ITashaPerson person, float expFactor)
        {
            _personRecordQueue.Add(new PersonRecord(hhldID, person.Id, person.Age, person.Female ? 'F' : 'M',
                                    person.Licence, person.TransitPass != TransitPass.None, GetEmploymentChar(person.EmploymentStatus),
                                    GetOccupationChar(person.Occupation), person.FreeParking, GetStudentChar(person.StudentStatus),
                                    person.EmploymentZone?.ZoneNumber ?? 0,
                                    person.SchoolZone?.ZoneNumber ?? 0, expFactor));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static char GetEmploymentChar(TTSEmploymentStatus employmentStatus)
        {
            switch (employmentStatus)
            {
                case TTSEmploymentStatus.FullTime:
                    return 'F';
                case TTSEmploymentStatus.PartTime:
                    return 'P';
                case TTSEmploymentStatus.WorkAtHome_FullTime:
                    return 'H';
                case TTSEmploymentStatus.WorkAtHome_PartTime:
                    return 'J';
                default:
                    return 'O';
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char GetOccupationChar(Occupation occupation)
        {
            switch (occupation)
            {
                case Occupation.Professional:
                    return 'P';
                case Occupation.Office:
                    return 'G';
                case Occupation.Retail:
                    return 'S';
                case Occupation.Manufacturing:
                    return 'M';
                default:
                    return 'O';
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char GetStudentChar(StudentStatus studentStatus)
        {
            switch (studentStatus)
            {
                case StudentStatus.FullTime:
                    return 'F';
                case StudentStatus.PartTime:
                    return 'P';
                default:
                    return 'O';
            }
        }


        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {
            if (_writeThisIteration)
            {
                _activeDATData.TryGetValue(household, out var datData);
                _activePATData.TryGetValue(household, out var patData);
                _activePassengerData.TryGetValue(household, out var passengerRecords);
                foreach (var person in household.Persons)
                {
                    foreach (var tc in person.TripChains)
                    {
                        if(tc.JointTrip && !tc.JointTripRep)
                        {
                            continue;
                        }
                        // this is only true if a station has been selected in the tours past
                        if (tc["AccessStation"] is IZone zone)
                        {
                            // if the household doesn't have data, allocate it
                            if (datData == null)
                            {
                                datData = new Dictionary<PersonChain, DATIterationInformation>();
                                _activeDATData[household] = datData;
                            }
                            var tcRef = new PersonChain(person, tc);
                            // if the trip chain does not have data, allocate it
                            if (!datData.TryGetValue(tcRef, out var tcData))
                            {
                                tcData = new DATIterationInformation();
                                datData[tcRef] = tcData;
                            }
                            tcData.AddTourData(_dat, tc, hhldIteration, zone, _autoNetwork,
                                _transitNetwork, _zoneSystem);
                        }
                        foreach (var trip in tc.Trips)
                        {
                            var mode = trip.Mode;
                            if (mode == _passenger)
                            {
                                if (trip["Driver"] is ITrip driverTrip)
                                {
                                    if (passengerRecords == null)
                                    {
                                        _activePassengerData[household] = passengerRecords = new Dictionary<ITrip, PassengerIterationInformation>();
                                    }
                                    passengerRecords.TryGetValue(trip, out var tripData);
                                    if (tripData == null)
                                    {
                                        passengerRecords.Add(trip, tripData = new PassengerIterationInformation());
                                    }
                                    tripData.Add(driverTrip, this);
                                }
                            }
                            else if(mode == _pat || mode == _pet)
                            {
                                if(patData == null)
                                {
                                    _activePATData[household] = patData = new Dictionary<ITrip, PATIterationInformation>();
                                }
                                if(!patData.TryGetValue(trip, out var currentTripData))
                                {
                                    patData[trip] = (currentTripData = new PATIterationInformation(household.HouseholdId, person.Id, trip.TripNumber));
                                }
                                if (mode == _pat)
                                {
                                    currentTripData.AddTripData(_zoneSystem, _autoNetwork, _transitNetwork, trip, true, (trip[PATModeStationTag] as IZone)?.ZoneNumber 
                                        ?? throw new XTMFRuntimeException(this, "Unable to get the zone number for the station used for passenger access transit"));
                                }
                                else
                                {
                                    currentTripData.AddTripData(_zoneSystem, _autoNetwork, _transitNetwork, trip, false, (trip[PETModeStationTag] as IZone)?.ZoneNumber 
                                        ?? throw new XTMFRuntimeException(this, "Unable to get the zone number for the station used for passenger egress transit"));
                                }
                            }
                        }
                    }
                }
            }
        }

        private sealed class DATIterationInformation
        {
            private struct ReplicationData
            {
                internal bool First;
                internal int StationIndex;

                public ReplicationData(bool first, int stationIndex)
                {
                    First = first;
                    StationIndex = stationIndex;
                }
            }

            private struct TripData
            {
                internal List<ReplicationData> Data;
                internal int DATTripCount;
                internal float TotalDatTravelTime;

                internal void Add(bool first, int stnZoneNumber, float travelTime)
                {
                    DATTripCount++;
                    TotalDatTravelTime += travelTime;
                    (Data = Data ?? new List<ReplicationData>()).Add(new ReplicationData(first, stnZoneNumber));
                }
            }

            private Dictionary<ITrip, TripData> _tripData = new Dictionary<ITrip, TripData>();

            internal void AddTourData(ITashaMode dat, ITripChain chain, int replication, IZone accessStationZone,
                INetworkData autoNetwork, ITripComponentData transitNetwork, SparseArray<IZone> _zones)
            {
                bool first = true;
                var stnZoneNumber = accessStationZone.ZoneNumber;
                var stnIndex = _zones.GetFlatIndex(accessStationZone.ZoneNumber);
                foreach (var trip in chain.Trips)
                {
                    if (trip.Mode == dat && dat != null)
                    {
                        if (!_tripData.TryGetValue(trip, out TripData data))
                        {
                            data = new TripData();
                        }
                        data.Add(first, stnZoneNumber, CalculateDATTime(_zones, autoNetwork, transitNetwork,
                            (trip.Purpose == Activity.Home ? trip.TripStartTime : trip.ActivityStartTime)
                            , trip.OriginalZone, stnIndex, trip.DestinationZone, first));
                        _tripData[trip] = data;
                        first = false;
                    }
                }
            }

            internal float ExpectedDATTravelTime(ITrip trip)
            {
                if (_tripData.TryGetValue(trip, out var tripData))
                {
                    return tripData.TotalDatTravelTime / tripData.DATTripCount;
                }
                return 0f;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private float CalculateDATTime(SparseArray<IZone> _zoneSystem, INetworkData _autoNetwork, ITripComponentData _transitNetwork, Time time,
                IZone originalZone, int accessFlat, IZone destinationZone, bool access)
            {
                var origin = _zoneSystem.GetFlatIndex(originalZone.ZoneNumber);
                var destination = _zoneSystem.GetFlatIndex(destinationZone.ZoneNumber);
                if (access)
                {
                    return (_autoNetwork.TravelTime(origin, accessFlat, time)
                        + _transitNetwork.TravelTime(accessFlat, destination, time)).ToMinutes();
                }
                else
                {
                    return (_transitNetwork.TravelTime(origin, accessFlat, time)
                        + _autoNetwork.TravelTime(accessFlat, destination, time)).ToMinutes();
                }
            }

            internal void StoreStationRecords(BlockingCollection<StationRecord> stationRecordQueue,
                int hhldID, int personID, int tripID, ITrip trip, DATIterationInformation chainData)
            {
                // for each trip that we have stored
                if (_tripData.TryGetValue(trip, out var tripData))
                {
                    var replicationData = tripData.Data;
                    // store the access and egresses per access station
                    foreach (var stn in from rec in replicationData
                                        group rec by rec.StationIndex into g
                                        select new
                                        {
                                            StationIndex = g.Key,
                                            TimesAccessed = g.Count(r => r.First),
                                            TimesEgressed = g.Count(r => !r.First)
                                        })
                    {
                        if (stn.TimesAccessed > 0)
                        {
                            stationRecordQueue.Add(new StationRecord(hhldID, personID, tripID, stn.StationIndex, true, stn.TimesAccessed));
                        }
                        if (stn.TimesEgressed > 0)
                        {
                            stationRecordQueue.Add(new StationRecord(hhldID, personID, tripID, stn.StationIndex, false, stn.TimesEgressed));
                        }
                    }
                }
            }
        }

        private sealed class PATIterationInformation
        {
            private struct PassengerTransitTrip
            {
                internal int StationZone;
                internal int Count;
                internal float Time;
                internal bool Access;
            }

            private readonly List<PassengerTransitTrip> _stationChoices = new List<PassengerTransitTrip>();
            private readonly int _householdId;
            private readonly int _personId;
            private readonly int _tripId;

            private float _totalAccessTime;
            private float _totalEgressTime;


            public PATIterationInformation(int householdId, int personId, int tripId)
            {
                _householdId = householdId;
                _personId = personId;
                _tripId = tripId;
            }

            internal void AddTripData(SparseArray<IZone> zoneSystem, INetworkData autoNetwork, ITripComponentData transitNetwork, ITrip trip, bool access, int stationZone)
            {
                int i;
                for(i = 0; i < _stationChoices.Count; i++)
                {
                    var record = _stationChoices[i];
                    if (record.StationZone == stationZone & record.Access == access)
                    {
                        record.Count++;
                        if(access)
                        {
                            _totalAccessTime += record.Time;
                        }
                        else
                        {
                            _totalEgressTime += record.Time;
                        }
                        _stationChoices[i] = record;
                        return;
                    }
                }
                // if we did not find a record already
                if(i == _stationChoices.Count)
                {
                    var time = CalculateTime(zoneSystem, autoNetwork, transitNetwork, (trip.Purpose == Activity.Home ? trip.TripStartTime : trip.ActivityStartTime), trip.OriginalZone,
                            zoneSystem.GetFlatIndex(stationZone), trip.DestinationZone, access);
                    if (access)
                    {
                        _totalAccessTime += time;
                    }
                    else
                    {
                        _totalEgressTime += time;
                    }
                    _stationChoices.Add(new PassengerTransitTrip()
                    {
                        Access = access,
                        Count = 1,
                        StationZone = stationZone,
                        Time = time
                    });
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private float CalculateTime(SparseArray<IZone> _zoneSystem, INetworkData _autoNetwork, ITripComponentData _transitNetwork, Time time,
                IZone originalZone, int accessFlat, IZone destinationZone, bool access)
            {
                var origin = _zoneSystem.GetFlatIndex(originalZone.ZoneNumber);
                var destination = _zoneSystem.GetFlatIndex(destinationZone.ZoneNumber);
                if (access)
                {
                    return (_autoNetwork.TravelTime(origin, accessFlat, time)
                        + _transitNetwork.TravelTime(accessFlat, destination, time)).ToMinutes();
                }
                else
                {
                    return (_transitNetwork.TravelTime(origin, accessFlat, time)
                        + _autoNetwork.TravelTime(accessFlat, destination, time)).ToMinutes();
                }
            }

            internal void CreateStationRecords(BlockingCollection<StationRecord> stationRecordQueue)
            {
                foreach (var stn in from rec in _stationChoices
                                    group rec by rec.StationZone into g
                                    select new { StationIndex = g.Key, Accesses = g.Count(r => r.Access == true), Egresses = g.Count(r => r.Access == false) })
                {
                    if (stn.Accesses > 0)
                    {
                        stationRecordQueue.Add(new StationRecord(_householdId, _personId, _tripId, stn.StationIndex, true, stn.Accesses));
                    }
                    if (stn.Egresses > 0)
                    {
                        stationRecordQueue.Add(new StationRecord(_householdId, _personId, _tripId, stn.StationIndex, false, stn.Egresses));
                    }
                }
            }

            internal float ExpectedTravelTime(bool access)
            {
                float time = access ? _totalAccessTime / _stationChoices.Count(r => r.Access == true)
                                    : _totalEgressTime / _stationChoices.Count(r => r.Access == false);
                return float.IsNaN(time) ? 0 : time;
            }
        }


        private class PassengerIterationInformation
        {
            private struct DriverTrip
            {
                internal readonly int DriverID;
                internal readonly int DriverTripID;
                internal readonly int Count;

                public DriverTrip(int driverID, int driverTripID, int count)
                {
                    DriverID = driverID;
                    DriverTripID = driverTripID;
                    Count = count;
                }

                public DriverTrip(DriverTrip previous) : this(previous.DriverID, previous.DriverTripID, previous.Count + 1) { }
            }

            private List<DriverTrip> _driverRecords = new List<DriverTrip>(1);

            public void Add(ITrip driverTrip, ExtractPersonalAndTripRecords module)
            {
                var chain = driverTrip.TripChain;
                var driver = chain.Person;
                var driverID = driver.Id;
                int tripIndex = ComputeTripIndex(driverTrip, chain, driver, module);
                // Now that we have our index data see if it already exists
                for (int i = 0; i < _driverRecords.Count; i++)
                {
                    if (_driverRecords[i].DriverID == driverID && _driverRecords[i].DriverTripID == tripIndex)
                    {
                        _driverRecords[i] = new DriverTrip(_driverRecords[i]);
                        return;
                    }
                }
                // If it was not found we need to add a new record
                _driverRecords.Add(new DriverTrip(driverID, tripIndex, 1));
            }

            private static int ComputeTripIndex(ITrip driverTrip, ITripChain chain, ITashaPerson driver, ExtractPersonalAndTripRecords module)
            {

                // If it is not a pure Home -> facilitate passenger -> home tour.
                if (chain.Trips.Count != 1)
                {
                    // trips are 1 indexed
                    var tripIndex = 1;
                    foreach (var tc in driver.TripChains)
                    {
                        if (tc == chain)
                        {
                            tripIndex += tc.Trips.IndexOf(driverTrip);
                            return tripIndex;
                        }
                        else
                        {
                            tripIndex += tc.Trips.Count;
                        }
                    }
                    throw new XTMFRuntimeException(module, $"In {module.Name} a driver trip that had a real tour was found that was not in the driver's tours!");
                }
                return -1;
            }

            public void CreateRecords(BlockingCollection<FacilitatePassengerRecord> queue, int householdID, ITrip passengerTrip, ExtractPersonalAndTripRecords module)
            {
                var chain = passengerTrip.TripChain;
                var passenger = chain.Person;
                var passengerId = passenger.Id;
                var passengerTripId = ComputeTripIndex(passengerTrip, chain, passenger, module);
                foreach (var record in _driverRecords)
                {
                    queue.Add(new FacilitatePassengerRecord(householdID, passengerId, passengerTripId, record.DriverID, record.DriverTripID, record.Count));
                }
            }
        }

        public void HouseholdStart(ITashaHousehold household, int householdIterations)
        {
            // nothing to do here
        }

        public void IterationStarting(int iteration, int totalIterations)
        {
            if (_writeThisIteration = (iteration >= totalIterations - 1))
            {
                _zoneSystem = Root.ZoneSystem.ZoneArray;
                _personRecordQueue = new BlockingCollection<PersonRecord>();
                _tripRecordQueue = new BlockingCollection<TripRecord>();
                _modeRecordQueue = new BlockingCollection<ModeRecord>();
                _stationRecordQueue = new BlockingCollection<StationRecord>();
                _facilitatePassengerRecordQueue = new BlockingCollection<FacilitatePassengerRecord>();
                if(HouseholdRecords != null)
                {
                    _householdRecordQueue = new BlockingCollection<HouseholdRecord>();
                    _writeHouseholdOutput = Task.Factory.StartNew(() => ProcessHouseholdRecords(), TaskCreationOptions.LongRunning);
                }
                _writePersonOutput = Task.Factory.StartNew(() => ProcessPersonRecords(), TaskCreationOptions.LongRunning);
                _writeTripOutput = Task.Factory.StartNew(() => ProcessTripRecords(), TaskCreationOptions.LongRunning);
                _writeModeOutput = Task.Factory.StartNew(() => ProcessModeRecords(), TaskCreationOptions.LongRunning);
                _writeStationOutput = Task.Factory.StartNew(() => ProcessStationRecords(), TaskCreationOptions.LongRunning);
                _writeFacilitatePassengerOutput = Task.Factory.StartNew(() => ProcessFacilitatePassengerRecords(), TaskCreationOptions.LongRunning);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string AsBoolString(bool val)
        {
            return val ? "true" : "false";
        }

        private static void CompressAndRemove(string originalFileName)
        {
            FileInfo fileToCompress = new FileInfo(originalFileName);
            if (fileToCompress.Exists)
            {
                using (FileStream originalFileStream = fileToCompress.OpenRead())
                {
                    using (FileStream compressedFileStream = File.Create(fileToCompress.FullName + ".gz"))
                    {
                        using (GZipStream compressionStream = new GZipStream(compressedFileStream,
                           CompressionMode.Compress))
                        {
                            originalFileStream.CopyTo(compressionStream);
                        }
                    }
                }
                //delete the file once we've finished compressing it
                fileToCompress.Delete();
            }
        }

        private void ProcessHouseholdRecords()
        {
            using (var writer = new StreamWriter(HouseholdRecords))
            {
                writer.WriteLine("household_id,home_zone,weight,persons,dwelling_type,vehicles,income_class");
                foreach(var household in _householdRecordQueue.GetConsumingEnumerable())
                {
                    writer.Write(household.HouseholdID);
                    writer.Write(',');
                    writer.Write(household.HomeZone);
                    writer.Write(',');
                    writer.Write(household.ExpansionFactor);
                    writer.Write(',');
                    writer.Write(household.NumberOfPersons);
                    writer.Write(',');
                    writer.Write(household.DwellingType);
                    writer.Write(',');
                    writer.Write(household.NumberOfVehicles);
                    writer.Write(',');
                    writer.WriteLine(household.IncomeClass);
                }
            }
            if (CompressResults)
            {
                CompressAndRemove(HouseholdRecords);
            }
        }

        private void ProcessPersonRecords()
        {
            using (var writer = new StreamWriter(PersonRecords))
            {
                writer.WriteLine("household_id,person_id,age,sex,license,transit_pass,employment_status,occupation,free_parking" +
                    ",student_status,work_zone,school_zone,weight");
                foreach (var person in _personRecordQueue.GetConsumingEnumerable())
                {
                    writer.Write(person.HouseholdID);
                    writer.Write(',');
                    writer.Write(person.PersonID);
                    writer.Write(',');
                    writer.Write(person.Age);
                    writer.Write(',');
                    writer.Write(person.Sex);
                    writer.Write(',');
                    writer.Write(AsBoolString(person.License));
                    writer.Write(',');
                    writer.Write(AsBoolString(person.TransitPass));
                    writer.Write(',');
                    writer.Write(person.EmploymentStatus);
                    writer.Write(',');
                    writer.Write(person.Occupation);
                    writer.Write(',');
                    writer.Write(AsBoolString(person.FreeParking));
                    writer.Write(',');
                    writer.Write(person.StudentStatus);
                    writer.Write(',');
                    writer.Write(person.WorkZone);
                    writer.Write(',');
                    writer.Write(person.SchoolZone);
                    writer.Write(',');
                    writer.WriteLine(person.ExpFactor);
                }
            }
            if (CompressResults)
            {
                CompressAndRemove(PersonRecords);
            }
        }

        private void ProcessTripRecords()
        {
            using (var writer = new StreamWriter(TripRecords))
            {
                writer.WriteLine("household_id,person_id,trip_id,o_act,o_zone,d_act,d_zone,weight");
                foreach (var trip in _tripRecordQueue.GetConsumingEnumerable())
                {
                    writer.Write(trip.HouseholdID);
                    writer.Write(',');
                    writer.Write(trip.PersonID);
                    writer.Write(',');
                    writer.Write(trip.TripID);
                    writer.Write(',');
                    writer.Write(trip.OriginActivity);
                    writer.Write(',');
                    writer.Write(trip.OriginZone);
                    writer.Write(',');
                    writer.Write(trip.DestinationActivity);
                    writer.Write(',');
                    writer.Write(trip.DestinationZone);
                    writer.Write(',');
                    writer.WriteLine(trip.ExpFactor);
                }
            }
            if (CompressResults)
            {
                CompressAndRemove(TripRecords);
            }
        }

        private void ProcessModeRecords()
        {
            using (var writer = new StreamWriter(TripModeRecords))
            {
                writer.WriteLine("household_id,person_id,trip_id,mode,o_depart,d_arrive,weight");
                foreach (var mode in _modeRecordQueue.GetConsumingEnumerable())
                {
                    writer.Write(mode.HouseholdID);
                    writer.Write(',');
                    writer.Write(mode.PersonID);
                    writer.Write(',');
                    writer.Write(mode.TripID);
                    writer.Write(',');
                    writer.Write(mode.Mode);
                    writer.Write(',');
                    if (ExportTimesAsMinutes)
                    {
                        writer.Write(mode.OriginDeparture.ToMinutes());
                        writer.Write(',');
                        writer.Write(mode.DestinationArrivalTime.ToMinutes());
                    }
                    else
                    {
                        writer.Write(mode.OriginDeparture);
                        writer.Write(',');
                        writer.Write(mode.DestinationArrivalTime);

                    }
                    writer.Write(',');
                    writer.WriteLine(mode.Weight);
                }
            }
            if (CompressResults)
            {
                CompressAndRemove(TripModeRecords);
            }
        }

        private void ProcessStationRecords()
        {
            using (var writer = new StreamWriter(TripStationRecords))
            {
                writer.WriteLine("household_id,person_id,trip_id,station,direction,weight");
                foreach (var station in _stationRecordQueue.GetConsumingEnumerable())
                {
                    writer.Write(station.HouseholdID);
                    writer.Write(',');
                    writer.Write(station.PersonID);
                    writer.Write(',');
                    writer.Write(station.TripID);
                    writer.Write(',');
                    writer.Write(station.StationID);
                    writer.Write(',');
                    writer.Write(station.ToTransit ? "auto2transit" : "transit2auto");
                    writer.Write(',');
                    writer.WriteLine(station.Weight);
                }
            }
            if (CompressResults)
            {
                CompressAndRemove(TripStationRecords);
            }
        }

        private void ProcessFacilitatePassengerRecords()
        {
            using (var writer = new StreamWriter(FacilitatePassengerRecords))
            {
                writer.WriteLine("household_id,passenger_id,passenger_trip_id,driver_id,driver_trip_id,weight");
                foreach (var passengerTrip in _facilitatePassengerRecordQueue.GetConsumingEnumerable())
                {
                    writer.Write(passengerTrip.HouseholdID);
                    writer.Write(',');
                    writer.Write(passengerTrip.PassengerID);
                    writer.Write(',');
                    writer.Write(passengerTrip.PassengerTripID);
                    writer.Write(',');
                    writer.Write(passengerTrip.DriverID);
                    writer.Write(',');
                    writer.Write(passengerTrip.DriverTripID);
                    writer.Write(',');
                    writer.WriteLine(passengerTrip.Weight);
                }
            }
            if (CompressResults)
            {
                CompressAndRemove(FacilitatePassengerRecords);
            }
        }

        public void IterationFinished(int iteration, int totalIterations)
        {
            // on the last iteration wait for all of the storage tasks to finish before continuing on.
            if (_writeThisIteration)
            {
                _householdRecordQueue?.CompleteAdding();
                _personRecordQueue.CompleteAdding();
                _tripRecordQueue.CompleteAdding();
                _modeRecordQueue.CompleteAdding();
                _stationRecordQueue.CompleteAdding();
                _facilitatePassengerRecordQueue.CompleteAdding();
                if (_writeHouseholdOutput != null)
                {
                    Task.WaitAll(new[] { _writeHouseholdOutput, _writePersonOutput, _writeTripOutput, _writeModeOutput, _writeStationOutput, _writeFacilitatePassengerOutput });
                }
                else
                {
                    Task.WaitAll(new[] { _writePersonOutput, _writeTripOutput, _writeModeOutput, _writeStationOutput, _writeFacilitatePassengerOutput });
                }
                _writePersonOutput = null;
                _writeTripOutput = null;
                _writeModeOutput = null;
                _writeStationOutput = null;
                _writeFacilitatePassengerOutput = null;
                _householdRecordQueue?.Dispose();
                _personRecordQueue.Dispose();
                _tripRecordQueue.Dispose();
                _modeRecordQueue.Dispose();
                _stationRecordQueue.Dispose();
                _facilitatePassengerRecordQueue.Dispose();
                _householdRecordQueue = null;
                _personRecordQueue = null;
                _tripRecordQueue = null;
                _modeRecordQueue = null;
                _stationRecordQueue = null;
                _facilitatePassengerRecordQueue = null;
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            foreach (var network in Root.NetworkData)
            {
                if (network.NetworkType == AutoNetworkName)
                {
                    _autoNetwork = network as INetworkData;
                }
                else if (network.NetworkType == TransitNetworkName)
                {
                    _transitNetwork = network as ITripComponentData;
                }
            }
            if (_autoNetwork == null)
            {
                error = "In '" + Name + "' we were unable to find an auto network called '" + AutoNetworkName + "'";
                return false;
            }
            if (_transitNetwork == null)
            {
                error = "In '" + Name + "' we were unable to find a transit network called '" + TransitNetworkName + "'";
                return false;
            }
            foreach (var mode in Root.AllModes)
            {
                if (mode.ModeName == DATModeName)
                {
                    _dat = mode;
                }
                else if (mode.ModeName == PATModeName)
                {
                    _pat = mode;
                }
                else if (mode.ModeName == PETModeName)
                {
                    _pet = mode;
                }
            }
            foreach (var mode in Root.AllModes)
            {
                if (mode.ModeName == PassengerModeName)
                {
                    _passenger = mode;
                    break;
                }
            }
            if (_dat == null && !String.IsNullOrWhiteSpace(DATModeName))
            {
                error = "In '" + Name + "' we were unable to find a DAT mode called '" + DATModeName + "'";
                return false;
            }
            if (_pat == null && !String.IsNullOrWhiteSpace(PATModeName))
            {
                error = "In '" + Name + "' we were unable to find a PAT mode called '" + PATModeName + "'";
                return false;
            }
            if (_pet == null && !String.IsNullOrWhiteSpace(PETModeName))
            {
                error = "In '" + Name + "' we were unable to find a PAT mode called '" + PETModeName + "'";
                return false;
            }
            if (_passenger == null)
            {
                error = "In '" + Name + "' we were unable to find a Passenger mode called '" + PassengerModeName + "'";
                return false;
            }
            return true;
        }

        private void Dispose(bool managed)
        {
            if (managed)
            {
                GC.SuppressFinalize(this);
            }
            _householdRecordQueue?.Dispose();
            _personRecordQueue?.Dispose();
            _tripRecordQueue?.Dispose();
            _modeRecordQueue?.Dispose();
            _stationRecordQueue?.Dispose();
            _facilitatePassengerRecordQueue?.Dispose();
            _householdRecordQueue = null;
            _personRecordQueue = null;
            _tripRecordQueue = null;
            _modeRecordQueue = null;
            _stationRecordQueue = null;
            _facilitatePassengerRecordQueue = null;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        ~ExtractPersonalAndTripRecords()
        {
            Dispose(false);
        }
    }
}
