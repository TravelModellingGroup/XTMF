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
// ReSharper disable InconsistentNaming

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
            return BaseLoader.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return BaseLoader.GetEnumerator();
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
            BaseLoader.Reset();
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
            BaseLoader.LoadData();
            var households = BaseLoader.ToArray();
            Households = households;
            using ( MemoryStream mem = new MemoryStream() )
            {
                BinaryWriter writer = new BinaryWriter( mem );
                writer.Write( households.Length );
                var numberOfVehicleTypes = Root.VehicleTypes.Count;
                writer.Write( numberOfVehicleTypes );
                for ( int i = 0; i < numberOfVehicleTypes; i++ )
                {
                    writer.Write( Root.VehicleTypes[i].VehicleName );
                }
                foreach ( var household in households )
                {
                    // write out all of the household attributes
                    writer.Write( household.HouseholdId );
                    writer.Write( household.Persons.Length );
                    for ( int i = 0; i < numberOfVehicleTypes; i++ )
                    {
                        writer.Write( household.Vehicles.Count( (v) => v.VehicleType.VehicleName == Root.VehicleTypes[i].VehicleName ) );
                    }
                    writer.Write( household.HomeZone.ZoneNumber );
                    SendAttached( writer, household );
                    foreach ( var person in household.Persons )
                    {
                        // Send the person's information
                        writer.Write( person.Age );
                        writer.Write( person.Female );
                        writer.Write( (Int32)person.EmploymentStatus );
                        writer.Write( (Int32)person.Occupation );
                        if ( person.EmploymentZone == null )
                        {
                            writer.Write( -1 );
                        }
                        else
                        {
                            writer.Write( person.EmploymentZone.ZoneNumber );
                        }
                        writer.Write( (Int32)person.StudentStatus );
                        if ( person.SchoolZone == null )
                        {
                            writer.Write( -1 );
                        }
                        else
                        {
                            writer.Write( person.SchoolZone.ZoneNumber );
                        }
                        writer.Write( person.Licence );

                        writer.Write( person.FreeParking );
                        SendAttached( writer, person );
                        // Start sending the trip chains
                        writer.Write( person.TripChains.Count );
                        foreach ( var tripChain in person.TripChains )
                        {
                            writer.Write( tripChain.JointTripID );
                            writer.Write( tripChain.JointTripRep );
                            SendAttached( writer, tripChain );
                            writer.Write( tripChain.Trips.Count );
                            foreach ( var trip in tripChain.Trips )
                            {
                                writer.Write( trip.OriginalZone.ZoneNumber );
                                writer.Write( trip.DestinationZone.ZoneNumber );
                                writer.Write( (Int32)trip.Purpose );
                                writer.Write( trip.ActivityStartTime.Hours );
                                writer.Write( trip.ActivityStartTime.Minutes );
                                writer.Write( trip.ActivityStartTime.Seconds );
                                var mode = ( (ITashaMode)trip[ObservedMode] );
                                if ( mode == null )
                                {
                                    throw new XTMFRuntimeException(this, "In household #" + household.HouseholdId
                                        + " for Person #" + person.Id + " for Trip #" + trip.TripNumber + " there was no observed mode stored!" );
                                }
                                writer.Write( mode.ModeName );
                                SendAttached( writer, trip );
                            }
                        }
                    }
                }
                writer.Flush();
                // rewind to the beginning
                mem.Seek( 0, SeekOrigin.Begin );
                MemoryStream encryptedMemory = new MemoryStream();
                using ( var encryption = new CryptoStream( encryptedMemory, new RijndaelManaged().CreateEncryptor( GetKey(), GetIV() ), CryptoStreamMode.Write ) )
                {
                    mem.WriteTo( encryption );
                    encryption.FlushFinalBlock();
                    HouseholdEncryptedData = encryptedMemory.ToArray();
                }
            }
        }

        private byte[] GetIV()
        {
            return ConvertToEncryptionKey( IV, 16 );
        }

        private byte[] GetKey()
        {
            return ConvertToEncryptionKey( Key, 32 );
        }

        private void SendAttached(BinaryWriter writer, IAttachable att)
        {
            var keys = att.Keys.ToList();
            writer.Write( keys.Count );
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
            ToClients.RegisterCustomReceiver( HouseholdDataChannel, (stream, client) =>
                {
                    client.SendCustomMessage( HouseholdEncryptedData, HouseholdDataChannel );
                    return null;
                } );
            ToClients.RegisterCustomSender( HouseholdDataChannel, (data, client, stream) =>
                {
                    var byteData = (byte[])data;
                    stream.Write( byteData, 0, byteData.Length );
                    stream.Flush();
                } );
        }
    }
}