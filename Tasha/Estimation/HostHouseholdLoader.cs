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
using System.Linq;
using System.Security.Cryptography;
using Tasha.Common;
using XTMF;
using XTMF.Networking;

namespace Tasha.Estimation
{
    [ModuleInformation( Description =
        "This module is designed to provide the interface to process requests from the client model system for household data."
        )]
    public class HostHouseholdLoader : IDataLoader<ITashaHousehold>
    {
        [SubModelInformation( Required = true, Description = "A regular household loader that will load the data from disk." )]
        public IDataLoader<ITashaHousehold> BaseLoader;

        [RunParameter( "Household Data Channel", 2, "The custom channel to use to receive the household data from the host." )]
        public int HouseholdDataChannel;

        [RunParameter( "Encryption Salt", "This is the salt for the encryption", "The key to use when receiving the household data." )]
        public string IV;

        [RunParameter( "Encryption Key", "Hello World, This is an Encryption Key", "The key to use when receiving the household data." )]
        public string Key;

        public bool Loaded = false;

        [RunParameter( "Observed Mode", "ObservedMode", "The 'attached' name for the observed mode for a given trip." )]
        public string ObservedMode;

        [RootModule]
        public ITashaRuntime Root;

        /// <summary>
        /// The connection to the host model system
        /// </summary>
        public IHost ToClients;

        private byte[] HouseholdEncryptedData;
        private ITashaHousehold[] Households;

        public int Count { get; set; }

        public bool IsSynchronized { get; set; }

        public string Name { get; set; }

        public bool OutOfData { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get; set; }

        public object SyncRoot { get; set; }

        public void CopyTo(ITashaHousehold[] array, int index)
        {
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<ITashaHousehold> GetEnumerator()
        {
            return this.BaseLoader.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.BaseLoader.GetEnumerator();
        }

        public void LoadData()
        {
            if ( !Loaded )
            {
                lock ( this )
                {
                    if ( !Loaded )
                    {
                        BuildHouesholdData();
                        SetupHost();
                    }
                }
            }
        }

        public void Reset()
        {
            this.BaseLoader.Reset();
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public ITashaHousehold[] ToArray()
        {
            return this.Households;
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

        private static byte[] ConvertToEncryptionKey(string baseString, int numberOfCharacters)
        {
            var keyChars = baseString.ToCharArray();
            byte[] finalKey = new byte[numberOfCharacters];
            for ( int i = 0; i < keyChars.Length && i < finalKey.Length; i++ )
            {
                finalKey[i] = (byte)keyChars[i];
            }
            return finalKey;
        }

        private void BuildHouesholdData()
        {
            this.BaseLoader.LoadData();
            var households = this.BaseLoader.ToArray();
            this.Households = households;
            using ( MemoryStream mem = new MemoryStream() )
            {
                BinaryWriter writer = new BinaryWriter( mem );
                writer.Write( (Int32)households.Length );
                var numberOfVehicleTypes = this.Root.VehicleTypes.Count;
                writer.Write( (Int32)numberOfVehicleTypes );
                for ( int i = 0; i < numberOfVehicleTypes; i++ )
                {
                    writer.Write( this.Root.VehicleTypes[i].VehicleName );
                }
                var vehicleCount = new int[numberOfVehicleTypes];
                foreach ( var household in households )
                {
                    // write out all of the household attributes
                    writer.Write( (Int32)household.HouseholdId );
                    writer.Write( (Int32)household.Persons.Length );
                    for ( int i = 0; i < numberOfVehicleTypes; i++ )
                    {
                        writer.Write( (Int32)household.Vehicles.Count( (v) => v.VehicleType.VehicleName == this.Root.VehicleTypes[i].VehicleName ) );
                    }
                    writer.Write( (Int32)household.HomeZone.ZoneNumber );
                    SendAttached( writer, household );
                    foreach ( var person in household.Persons )
                    {
                        // Send the person's information
                        writer.Write( (Int32)person.Age );
                        writer.Write( (Boolean)person.Female );
                        writer.Write( (Int32)person.EmploymentStatus );
                        writer.Write( (Int32)person.Occupation );
                        if ( person.EmploymentZone == null )
                        {
                            writer.Write( (Int32)( -1 ) );
                        }
                        else
                        {
                            writer.Write( (Int32)person.EmploymentZone.ZoneNumber );
                        }
                        writer.Write( (Int32)person.StudentStatus );
                        if ( person.SchoolZone == null )
                        {
                            writer.Write( (Int32)( -1 ) );
                        }
                        else
                        {
                            writer.Write( (Int32)person.SchoolZone.ZoneNumber );
                        }
                        writer.Write( (bool)person.Licence );

                        writer.Write( (bool)person.FreeParking );
                        SendAttached( writer, person );
                        // Start sending the trip chains
                        writer.Write( (Int32)person.TripChains.Count );
                        foreach ( var tripChain in person.TripChains )
                        {
                            writer.Write( (Int32)tripChain.JointTripID );
                            writer.Write( (bool)tripChain.JointTripRep );
                            SendAttached( writer, tripChain );
                            writer.Write( (Int32)tripChain.Trips.Count );
                            foreach ( var trip in tripChain.Trips )
                            {
                                writer.Write( (Int32)trip.OriginalZone.ZoneNumber );
                                writer.Write( (Int32)trip.DestinationZone.ZoneNumber );
                                writer.Write( (Int32)trip.Purpose );
                                writer.Write( (Int32)trip.ActivityStartTime.Hours );
                                writer.Write( (Int32)trip.ActivityStartTime.Minutes );
                                writer.Write( (Int32)trip.ActivityStartTime.Seconds );
                                var mode = ( (ITashaMode)trip[this.ObservedMode] );
                                if ( mode == null )
                                {
                                    throw new XTMFRuntimeException( "In household #" + household.HouseholdId
                                        + " for Person #" + person.Id + " for Trip #" + trip.TripNumber + " there was no observed mode stored!" );
                                }
                                writer.Write( mode.ModeName );
                                SendAttached( writer, trip );
                            }
                        }
                    }
                }
                writer.Flush();
                writer = null;
                // rewind to the beginning
                mem.Seek( 0, SeekOrigin.Begin );
                MemoryStream encryptedMemory = new MemoryStream();
                using ( var encryption = new CryptoStream( encryptedMemory, new RijndaelManaged().CreateEncryptor( this.GetKey(), this.GetIV() ), CryptoStreamMode.Write ) )
                {
                    mem.WriteTo( encryption );
                    encryption.FlushFinalBlock();
                    this.HouseholdEncryptedData = encryptedMemory.ToArray();
                    encryptedMemory = null;
                }
            }
        }

        private byte[] GetIV()
        {
            return ConvertToEncryptionKey( this.IV, 16 );
        }

        private byte[] GetKey()
        {
            return ConvertToEncryptionKey( this.Key, 32 );
        }

        private void SendAttached(BinaryWriter writer, IAttachable att)
        {
            var keys = att.Keys;
            int keysLength = keys.Count();
            writer.Write( (Int32)keysLength );
            foreach ( var key in keys )
            {
                writer.Write( key );
                var o = att[key];
                if ( o == null )
                {
                    writer.Write( "System.Null" );
                    writer.Write( String.Empty );
                }
                else
                {
                    writer.Write( o.GetType().FullName );
                    writer.Write( o.ToString() );
                }
            }
        }

        private void SetupHost()
        {
            this.ToClients.RegisterCustomReceiver( this.HouseholdDataChannel, (stream, client) =>
                {
                    client.SendCustomMessage( this.HouseholdEncryptedData, this.HouseholdDataChannel );
                    return null;
                } );
            this.ToClients.RegisterCustomSender( this.HouseholdDataChannel, (data, client, stream) =>
                {
                    var byteData = data as byte[];
                    stream.Write( byteData, 0, byteData.Length );
                    stream.Flush();
                } );
        }
    }
}