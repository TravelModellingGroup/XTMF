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
using XTMF;
using TMG.Input;
using XTMF.Networking;
using System.IO;
using System.Threading;

namespace TMG.Estimation.Utilities;

[ModuleInformation(Description =
        @"This module is designed to interact with the host model system in order to receive a file.  It is expected that sending and receiving will occur with the same channel number.  This is designed to integrate into the 
TMG.Estimation framework however it should also work with anything using XTMF.Networking.")]
public class ClientFileTransfer : ISelfContainedModule
{
    [SubModelInformation(Required = true, Description = "The place to save the file")]
    public FileLocation FileLocation;

    public IClient Client;

    [RunParameter("Data Channel", 10, "The custom data channel to use for receiving the request to send the file.")]
    public int DataChannel;

    [RunParameter("Only Once", true, "Should we only get the copy of the file once?")]
    public bool OnceOnly;

    [RunParameter("From Host", true, "Are we receiving a file from the host (true) or sending the file to the host?")]
    public bool FromHost;

    private bool Loaded;
    // This is used to make sure the file is received before we continue
    private volatile bool FileTransmitted;
    private byte[] Data;

    public void Start()
    {
        if ( FromHost )
        {
            if ( !Loaded )
            {

                Client.RegisterCustomReceiver( DataChannel, (stream) =>
                {
                    var data = new byte[stream.Length];
                    stream.Read( data, 0, data.Length );
                    return data;
                } );
                Client.RegisterCustomMessageHandler( DataChannel, (obj) =>
                {
                    var data = obj as byte[];
                    if (data == null)
                    {
                        throw new XTMFRuntimeException(this, $"In {Name} we recieved something besides a byte array when gathering a file to send across the network.");
                    }
                    System.Threading.Tasks.Task.Factory.StartNew( () =>
                    {
                        try
                        {
                            var path = FileLocation.GetFilePath();
                            File.WriteAllBytes( path, data );
                            Thread.MemoryBarrier();
                            FileTransmitted = true;
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine( e.Message );
                        }
                    } );
                } );
                Client.RegisterCustomSender( DataChannel, (data, stream) =>
                {
                    // do nothing, no data is needed to trigger the send
                } );
            }
            if ( !Loaded | !OnceOnly )
            {
                FileTransmitted = false;
                Client.SendCustomMessage( null, DataChannel );
                while ( FileTransmitted == false )
                {
                    Thread.Sleep( 1 );
                    Thread.MemoryBarrier();
                }
            }
        }
        else
        {
            if ( !Loaded )
            {
                // register the sender
                Client.RegisterCustomReceiver( DataChannel, (stream) =>
                {
                    return null;
                } );
                    Client.RegisterCustomMessageHandler( DataChannel, (_) =>
                {
                    // do nothing
                } );
                    Client.RegisterCustomSender( DataChannel, (_, stream) =>
                {
                    stream.Write( Data, 0, Data.Length );
                    FileTransmitted = true;
                } );
            }
            Data = File.ReadAllBytes(FileLocation.GetFilePath());
            if ( !FileTransmitted | !OnceOnly )
            {
                FileTransmitted = false;
                Thread.MemoryBarrier();
                Client.SendCustomMessage( null, DataChannel );
                while ( !FileTransmitted ) Thread.Sleep( 0 );
            }
            // unload the data once it has been sent
            Data = null;
        }
        Loaded = true;
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
