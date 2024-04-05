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
using System.Linq;
using Tasha.Common;
using XTMF;
using TMG;
using TMG.Input;
using Datastructure;
using System.IO;
// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable MemberHidesStaticFromOuterClass
namespace Tasha.External
{

    public class ExternalTripChainLoader : IDataSource<List<ITripChain>>
    {

        [RootModule]
        public ITashaRuntime Root;

        public bool Loaded { get; set; }

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

        [SubModelInformation(Required = true, Description = "The location of the trip chain file.")]
        public FileLocation TripChainFile;

        private List<ITripChain> Data;

        public List<ITripChain> GiveData()
        {
            return Data;
        }

        private sealed class Household : Attachable, ITashaHousehold
        {
            public DwellingType DwellingType { get; set; }


            public float ExpansionFactor { get; set; }


            public HouseholdType HhType => default(HouseholdType);


            public IZone HomeZone { get; set; }


            public int HouseholdId { get; set; }

            public Dictionary<int, List<ITripChain>> JointTours => null;

            public int NumberOfAdults => 0;

            public int NumberOfChildren => 0;

            public ITashaPerson[] Persons { get { return null; } set { } }

            public IVehicle[] Vehicles => null;

            public int IncomeClass => throw new NotImplementedException();

            public ITashaHousehold Clone()
            {
                throw new NotImplementedException();
            }

            public List<ITashaPerson> GetJointTourMembers(int tourID)
            {
                throw new NotImplementedException();
            }

            public ITripChain GetJointTourTripChain(int tourID, ITashaPerson person)
            {
                throw new NotImplementedException();
            }

            public void Recycle()
            {
                throw new NotImplementedException();
            }

        }

        private sealed class Person : Attachable, ITashaPerson
        {
            public bool Adult { get { throw new NotImplementedException(); } }

            public int Age { get { throw new NotImplementedException(); } }

            public List<ITripChain> AuxTripChains { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

            public bool Child { get { throw new NotImplementedException(); } }

            public TTSEmploymentStatus EmploymentStatus { get { throw new NotImplementedException(); } }

            public IZone EmploymentZone { get { throw new NotImplementedException(); } }

            public float ExpansionFactor { get; set; }

            public bool Female => false;

            public bool FreeParking => false;

            // ReSharper disable once MemberHidesStaticFromOuterClass
            public ITashaHousehold Household { get; set; }

            public int Id => 0;

            public bool Licence => false;

            public bool Male => false;

            public Occupation Occupation { get { throw new NotImplementedException(); } }

            public IZone SchoolZone { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

            public StudentStatus StudentStatus { get { throw new NotImplementedException(); } }

            public TransitPass TransitPass { get { throw new NotImplementedException(); } }

            public List<ITripChain> TripChains { get; set; }

            public bool YoungAdult => false;

            public bool Youth => false;

            public ITashaPerson Clone() { throw new NotImplementedException(); }

            public void Recycle() { throw new NotImplementedException(); }
        }

        private sealed class TripChain : Attachable, ITripChain
        {
            public Time EndTime => Trips.Last().TripStartTime;

            public ITripChain GetRepTripChain => null;

            public bool JointTrip => false;

            public List<ITripChain> JointTripChains => null;

            public int JointTripID => 0;

            public bool JointTripRep => false;

            public List<ITashaPerson> Passengers => null;

            // ReSharper disable once MemberHidesStaticFromOuterClass
            public ITashaPerson Person { get; set; }

            public List<IVehicleType> RequiresVehicle => null;

            public Time StartTime
            {
                get { return Trips[0].TripStartTime; }
            }

            public bool TripChainRequiresPV => false;

            public List<ITrip> Trips { get; set; }

            public ITripChain Clone() { throw new NotImplementedException(); }

            public ITripChain DeepClone() { throw new NotImplementedException(); }

            public void Recycle() { throw new NotImplementedException(); }
        }

        private sealed class Trip : Attachable, ITrip
        {
            public Time ActivityStartTime => Time.Zero;

            public IZone DestinationZone { get; set; }

            public IZone IntermediateZone { get { return null; } set { } }

            public ITashaMode Mode { get; set; }

            public ITashaMode[] ModesChosen => null;

            public IZone OriginalZone { get; set; }

            public List<ITashaPerson> Passengers { get { return null; } set { } }

            public Activity Purpose { get { return default(Activity); } set { } }

            public ITashaPerson SharedModeDriver { get { return null; } set { } }

            public Time TravelTime => Time.Zero;

            public ITripChain TripChain { get; set; }

            public int TripNumber => 0;

            public Time TripStartTime { get; set; }

            public ITrip Clone() { throw new NotImplementedException(); }

            public void Recycle() { throw new NotImplementedException(); }
        }

        public void LoadData()
        {
            if (!Loaded)
            {
                try
                {
                    using CsvReader reader = new CsvReader(TripChainFile);
                    var zoneSystem = Root.ZoneSystem.ZoneArray;
                    var zones = zoneSystem.GetFlatData();
                    var chains = new List<ITripChain>();
                    Household[] households = CreateHouseholds(zones);
                    reader.LoadLine(out int columns);
                    int previousPerson = -1;
                    Person currentPerson;
                    TripChain currentChain = null;
                    while (reader.LoadLine(out columns))
                    {
                        if (columns < 7)
                        {
                            continue;
                        }
                        reader.Get(out int personNumber, 0);
                        reader.Get(out float expFactor, 1);
                        reader.Get(out int homeZone, 2);
                        reader.Get(out int origin, 3);
                        reader.Get(out int destination, 4);
                        reader.Get(out int startTime, 5);
                        reader.Get(out char modeCode, 6);
                        // check for the start of a new person
                        if (personNumber != previousPerson)
                        {
                            var homeZoneIndex = zoneSystem.GetFlatIndex(homeZone);
                            if (homeZoneIndex < 0)
                            {
                                throw new XTMFRuntimeException(this, $"An unknown household zone number was found {homeZone}!");
                            }
                            currentPerson = new Person()
                            {
                                Household = households[homeZoneIndex],
                                ExpansionFactor = expFactor
                            };
                            currentChain = new TripChain()
                            {
                                Person = currentPerson
                            };
                            currentChain.Trips = new List<ITrip>(4);
                            chains.Add(currentChain);
                        }
                        var oZone = zoneSystem[origin];
                        var dZone = zoneSystem[destination];
                        if (oZone == null)
                        {
                            throw new XTMFRuntimeException(this, $"Unable to find a zone #{origin}");
                        }
                        if (dZone == null)
                        {
                            throw new XTMFRuntimeException(this, $"Unable to find a zone #{destination}");
                        }
                        currentChain?.Trips.Add(new Trip()
                        {
                            OriginalZone = oZone,
                            DestinationZone = dZone,
                            TripStartTime = ConvertStartTime(startTime),
                            Mode = ConvertMode(modeCode)
                        });
                    }
                    // done
                    Data = chains;
                    Loaded = true;
                }
                catch(IOException e)
                {
                    throw new XTMFRuntimeException(this, e);
                }
            }
        }

        [SubModelInformation(Description = "A lookup to convert between mode character codes and the mode in Tasha.")]
        public ModeLookup[] ModeLinks;

        public class ModeLookup : IModule
        {
            [RootModule]
            public ITashaRuntime Root;

            [RunParameter("Mode Name", "Auto", "The name of the mode to attach to.")]
            public string ModeName;

            [RunParameter("Mode Code", 'D', "The code for the mode.")]
            public char ModeCode;

            private ITashaMode Mode;

            public ITashaMode GetMode()
            {
                return Mode;
            }

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

            public bool RuntimeValidation(ref string error)
            {
                foreach (var mode in Root.AllModes)
                {
                    if (mode.ModeName == ModeName)
                    {
                        Mode = mode;
                        break;
                    }
                }
                if (Mode == null)
                {
                    error = "In '" + Name + "' the mode named '" + ModeName + "' could not be found!";
                    return false;
                }
                return true;
            }
        }


        private ITashaMode ConvertMode(char modeCode)
        {
            return (from link in ModeLinks
                    where link.ModeCode == modeCode
                    select link.GetMode()).FirstOrDefault();
        }

        private Time ConvertStartTime(int startTime)
        {
            return Time.FromMinutes((startTime / 100) * 60 + (startTime % 100));
        }

        private Household[] CreateHouseholds(IZone[] zones)
        {
            return (from zone in zones
                    select new Household()
                    {
                        HomeZone = zone
                    }).ToArray();
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void UnloadData()
        {

        }
    }

}
