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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Tasha.Common;
using Tasha.Internal;
using Tasha.Scheduler;
using TMG;
using XTMF;
using XTMF.Networking;
using System.Threading.Tasks;
// ReSharper disable InconsistentNaming

namespace Tasha.Estimation
{
    [ModuleInformation(Description =
    "This module is designed to receive in the data from the estimation host and produce household objects."
        )]
    public class EstimationHouseholdLoader : IDataLoader<ITashaHousehold>
    {
        [RunParameter("Household Data Channel", 2, "The custom channel to use to receive the household data from the host.")]
        public int HouseholdDataChannel;

        [RunParameter("Encryption Salt", "This is the salt for the encryption", "The key to use when receiving the household data.")]
        public string IV;

        [RunParameter("Encryption Key", "Hello World, This is an Encryption Key", "The key to use when receiving the household data.")]
        public string Key;

        [RunParameter("Observed Mode Name", "ObservedMode", "The name of the attachment for the observed mode.")]
        public string ObservedMode;

        public bool Loaded;

        [RootModule]
        public ITashaRuntime Root;

        /// <summary>
        /// The connection to the host model system
        /// </summary>
        public IClient ToHost;

        private ITashaHousehold[] Households;

        public int Count { get; set; }

        public bool IsSynchronized { get; set; }

        public string Name { get; set; }

        public bool OutOfData { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get; set; }

        public object SyncRoot { get; } = new object();

        public void CopyTo(ITashaHousehold[] array, int index)
        {
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<ITashaHousehold> GetEnumerator()
        {
            return ((IEnumerable<ITashaHousehold>)Households).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Households.GetEnumerator();
        }

        public void LoadData()
        {
            if (!Loaded)
            {
                lock (this)
                {
                    if (!Loaded)
                    {
                        bool householdsReceived = false;
                        ToHost.RegisterCustomReceiver(HouseholdDataChannel, (stream) =>
                           {
                               byte[] key = GetKey()
                                   , iv = GetIV();
                               byte[] data = new byte[(int)stream.Length];
                               stream.Read(data, 0, (int)stream.Length);
                               Task.Factory.StartNew(() =>
                                   {
                                       MemoryStream memStream = null;
                                       try
                                       {
                                           memStream = new MemoryStream(data);
                                           using Aes aes = Aes.Create();
                                           aes.Key = key;
                                           aes.IV = iv;
                                           using var fromHost = new CryptoStream(memStream,
                                               aes.CreateDecryptor(), CryptoStreamMode.Read);
                                           memStream = null;
                                           var reader = new BinaryReader(fromHost);
                                           int numberOfHouseholds = reader.ReadInt32();
                                           int numberOfVehicles = reader.ReadInt32();
                                           if (numberOfVehicles != Root.VehicleTypes.Count)
                                           {
                                               throw new XTMFRuntimeException(this, "We were expecting to have '" + Root.VehicleTypes.Count + "' different types of vehicles but the host has '" + numberOfVehicles + "'");
                                           }
                                           for (int i = 0; i < numberOfVehicles; i++)
                                           {
                                               string temp;
                                               if (Root.VehicleTypes[i].VehicleName != (temp = reader.ReadString()))
                                               {
                                                   throw new XTMFRuntimeException(this, "We were expecting the vehicle type to be named '" + Root.VehicleTypes[i].VehicleName + "' and instead found '" + temp + "'");
                                               }
                                           }
                                           ITashaHousehold[] households = new ITashaHousehold[numberOfHouseholds];
                                           var zoneArray = Root.ZoneSystem.ZoneArray;
                                           for (int i = 0; i < numberOfHouseholds; i++)
                                           {
                                               households[i] = LoadHousehold(reader, zoneArray);
                                           }
                                           Households = households;
                                           householdsReceived = true;
                                       }
                                       catch(Exception e)
                                       {
                                           Console.WriteLine(e.Message + "\r\n" + e.StackTrace);
                                       }
                                       finally
                                       {
                                           memStream?.Dispose();
                                       }
                                   });
                               return null;
                           });
                        ToHost.RegisterCustomSender(HouseholdDataChannel, (data, stream) =>
                           {
                                // do nothing
                            });
                        ToHost.RegisterCustomMessageHandler(HouseholdDataChannel, (householdArray) =>
                           {
                                // do nothing
                            });
                        // Tell the host that we want our households
                        ToHost.SendCustomMessage(null, HouseholdDataChannel);
                        while (!householdsReceived)
                        {
                            System.Threading.Thread.Sleep(0);
                            System.Threading.Thread.MemoryBarrier();
                        }
                        Loaded = true;
                    }
                }
            }
        }

        public void Reset()
        {
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public ITashaHousehold[] ToArray()
        {
            return Households;
        }

        public bool TryAdd(ITashaHousehold item)
        {
            return false;
        }

        public bool TryTake(out ITashaHousehold item)
        {
            item = null;
            return false;
        }

        private static byte[] ConvertToEncryptionKey(string baseString, int size)
        {
            var keyChars = baseString.ToCharArray();
            byte[] finalKey = new byte[size];
            for (int i = 0; i < keyChars.Length && i < finalKey.Length; i++)
            {
                finalKey[i] = (byte)keyChars[i];
            }
            return finalKey;
        }

        private static void LoadKeys(BinaryReader reader, IAttachable att)
        {
            var numberOfKeys = reader.ReadInt32();
            for (int i = 0; i < numberOfKeys; i++)
            {
                var name = reader.ReadString();
                var type = reader.ReadString();
                var text = reader.ReadString();
                switch (type)
                {
                    case "System.String":
                        att.Attach(name, text);
                        break;

                    case "System.Single":
                        att.Attach(name, float.Parse(text));
                        break;

                    case "System.Int32":
                        att.Attach(name, int.Parse(text));
                        break;
                }
            }
        }

        private ITripChain FindRepTripChain(SchedulerTripChain chain, ITashaHousehold tashaHousehold)
        {
            foreach (var person in tashaHousehold.Persons)
            {
                foreach (var tc in person.TripChains)
                {
                    if (tc.JointTripID == chain.JointTripID && tc.JointTripRep)
                    {
                        return tc;
                    }
                }
            }
            throw new XTMFRuntimeException(this, "We were unable to find a joint trip representative's trip chain!");
        }

        private byte[] GetIV()
        {
            return ConvertToEncryptionKey(IV, 16);
        }

        private byte[] GetKey()
        {
            return ConvertToEncryptionKey(Key, 32);
        }

        private TashaHousehold LoadHousehold(BinaryReader reader, Datastructure.SparseArray<IZone> zoneArray)
        {
            var household = new TashaHousehold();
            int numberOfPeople;
            household.HouseholdId = reader.ReadInt32();
            // Learn how many people this household has and their number of vehicles
            household.Persons = new ITashaPerson[(numberOfPeople = reader.ReadInt32())];
            var vehicleList = new List<IVehicle>();
            // Produce the vehicles, all auto since it is the only type of resource we have
            for (int i = 0; i < Root.VehicleTypes.Count; i++)
            {
                var numberOfVehicles = reader.ReadInt32();
                for (int j = 0; j < numberOfVehicles; j++)
                {
                    vehicleList.Add(TashaVehicle.MakeVehicle(Root.VehicleTypes[i]));
                }
            }
            household.Vehicles = vehicleList.ToArray();
            household.HomeZone = zoneArray[reader.ReadInt32()];
            LoadKeys(reader, household);
            // now we can go and load the people
            for (int i = 0; i < numberOfPeople; i++)
            {
                household.Persons[i] = LoadPerson(reader, zoneArray, household, i);
            }
            // Link in the joint trip chain trip chains
            foreach (var person in household.Persons)
            {
                foreach (var tc in person.TripChains)
                {
                    if (tc.JointTrip)
                    {
                        if (tc.JointTripRep)
                        {
                            ((SchedulerTripChain)tc).GetRepTripChain = tc;
                        }
                        else
                        {
                            ((SchedulerTripChain)tc).GetRepTripChain = FindRepTripChain((SchedulerTripChain)tc, person.Household);
                        }
                    }
                }
            }
            return household;
        }

        private TashaPerson LoadPerson(BinaryReader reader, Datastructure.SparseArray<IZone> zoneArray, TashaHousehold household, int personID)
        {
            TashaPerson person = new TashaPerson
            {
                Household = household,
                Id = personID,
                Age = reader.ReadInt32(),
                Female = reader.ReadBoolean(),
                EmploymentStatus = (TTSEmploymentStatus)reader.ReadInt32(),
                Occupation = (Occupation)reader.ReadInt32(),
                EmploymentZone = zoneArray[reader.ReadInt32()],
                StudentStatus = (StudentStatus)reader.ReadInt32(),
                SchoolZone = zoneArray[reader.ReadInt32()],
                Licence = reader.ReadBoolean(),
                FreeParking = reader.ReadBoolean()
            };
            int numberOfTripChains;
            LoadKeys(reader, person);
            person.TripChains = new List<ITripChain>(numberOfTripChains = reader.ReadInt32());
            for (int i = 0; i < numberOfTripChains; i++)
            {
                person.TripChains.Add(LoadTripChain(reader, zoneArray, person));
            }
            return person;
        }

        [RunParameter("Household Iterations", 100, "The number of household iterations.")]
        public int HouseholdIterations;

        private SchedulerTrip LoadTrip(BinaryReader reader, Datastructure.SparseArray<IZone> zoneArray, SchedulerTripChain chain, int tripNumber)
        {
            SchedulerTrip trip = SchedulerTrip.GetTrip(HouseholdIterations);
            var allModes = Root.AllModes;
            trip.TripNumber = tripNumber;
            trip.TripChain = chain;
            // figure out where we are going
            trip.OriginalZone = zoneArray[reader.ReadInt32()];
            trip.DestinationZone = zoneArray[reader.ReadInt32()];
            trip.Purpose = (Activity)reader.ReadInt32();
            // And learn when we are leaving, and at what time we need to get there
            Time time = new Time
            {
                // The activity's start time
                Hours = reader.ReadInt32(),
                Minutes = reader.ReadInt32(),
                Seconds = reader.ReadInt32()
            };
            trip.ActivityStartTime = time;
            // Get the observed mode
            var modeName = reader.ReadString();
            for (int i = 0; i < allModes.Count; i++)
            {
                if (modeName == allModes[i].ModeName)
                {
                    trip[ObservedMode] = allModes[i];
                }
            }
            LoadKeys(reader, trip);
            return trip;
        }

        private SchedulerTripChain LoadTripChain(BinaryReader reader, Datastructure.SparseArray<IZone> zoneArray, TashaPerson person)
        {
            SchedulerTripChain chain = SchedulerTripChain.GetTripChain(person);
            chain.JointTripID = reader.ReadInt32();
            chain.JointTripRep = reader.ReadBoolean();
            LoadKeys(reader, chain);
            int numberOfTrips = reader.ReadInt32();
            for (int i = 0; i < numberOfTrips; i++)
            {
                SchedulerTrip trip = LoadTrip(reader, zoneArray, chain, i);
                // Now that we have all of the data that we need, add ourselves to the trip chain
                chain.Trips.Add(trip);
            }
            return chain;
        }
    }
}