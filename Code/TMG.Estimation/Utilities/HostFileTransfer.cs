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
using System.Text;
using XTMF;
using TMG.Input;
using XTMF.Networking;
using System.IO;
using System.Threading;

namespace TMG.Estimation.Utilities
{
    [ModuleInformation(Description =
        @"This module is designed to setup the host model system with the ability to send a file to the remote clients.  It is expected that sending and receiving will occur with the same channel number.  This is designed to integrate into the 
TMG.Estimation framework however it should also work with anything using XTMF.Networking.")]
    public class HostFileTransfer : ISelfContainedModule
    {
        [SubModelInformation(Required = true, Description = "The file to transfer")]
        public FileLocation FileLocation;

        [RunParameter("Data Channel", 10, "The custom data channel to use for receiving the request to send the file.")]
        public int DataChannel;

        [RunParameter("To Client", true, "Are we sending the file to the client (true) or receiving from client?")]
        public bool ToClient;

        // This is used to make sure the file is received before we continue
        private volatile bool FileReceived;

        public IHost Host;

        private byte[] Data;

        public void Start()
        {
            if ( ToClient )
            {
                // load the file
                this.Data = File.ReadAllBytes( this.FileLocation.GetFilePath() );
                // register the sender
                this.Host.RegisterCustomReceiver( this.DataChannel, (stream, remote) =>
                {
                    return null;
                } );
                this.Host.RegisterCustomMessageHandler( this.DataChannel, (_, stream) =>
                {
                    stream.SendCustomMessage( null, this.DataChannel );
                } );
                this.Host.RegisterCustomSender( this.DataChannel, (_, remote, stream) =>
                {
                    stream.Write( this.Data, 0, this.Data.Length );
                } );
            }
            else
            {
                // if we are going to receive a file from the client
                this.Host.RegisterCustomReceiver( this.DataChannel, (stream, _remote) =>
                    {
                        var data = new byte[stream.Length];
                        stream.Read( data, 0, data.Length );
                        return data;
                    } );
                this.Host.RegisterCustomMessageHandler( this.DataChannel, (obj, _remote) =>
                    {
                        var data = obj as byte[];
                        System.Threading.Tasks.Task.Factory.StartNew( () =>
                        {
                            try
                            {
                                var path = this.FileLocation.GetFilePath();
                                AppendToFile( path, data );
                                Thread.MemoryBarrier();
                                this.FileReceived = true;
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine( e.Message );
                            }
                        } );
                    } );
                this.Host.RegisterCustomSender( this.DataChannel, (data, _remote, stream) =>
                    {
                        // do nothing, no data is needed to trigger the send
                    } );
            }
        }

        private void AppendToFile(string path, byte[] data)
        {
            lock (this)
            {
                using (FileStream stream = new FileStream( path, FileMode.Append ))
                {
                    stream.Write( data, 0, data.Length );
                }
            }
        }

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
