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
using System.IO;
using XTMF;
using XTMF.Networking;
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedVariable

namespace XtmfSDK;

[ModuleInformation(Description =
    @"This is where you would describe what your host should be used for or other information about it.  It can handle <b>HTML</b> tags.")]
public class MyHostModule : IModelSystemTemplate
{
    public IHost Host;
    private static Tuple<byte, byte, byte> _ProgressColour = new( 50, 150, 50 );

    private volatile bool exit = false;

    private MessageQueue<int> Jobs;

    public string InputBaseDirectory
    {
        get;
        set;
    }

    public string Name { get; set; }

    public string OutputBaseDirectory
    {
        get;
        set;
    }

    public float Progress
    {
        get;
        set;
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return _ProgressColour; }
    }

    public bool ExitRequest()
    {
        return false;
    }

    public bool RuntimeValidation(ref string error)
    {
        if ( Host == null )
        {
            error = "MyHostModule requires an XTMF that supports XTMF.Networking.IHost";
            return false;
        }
        return true;
    }

    public void Start()
    {
        InitializeCustomMessages();
        // Your Code goes here
    }

    private void CustomMessageHandler(object data, IRemoteXTMF client)
    {
        if ( data is int )
        {
            Jobs.Add( (int)data );
        }
    }

    private object CustomMessageReceiver(Stream inputStream, IRemoteXTMF client)
    {
        return null;
    }

    private void CustomMessageSender(object data, IRemoteXTMF client, Stream outputStream)
    {
    }

    private void Host_AllModelSystemRunsComplete()
    {
    }

    private void Host_ClientDisconnected(IRemoteXTMF client)
    {
    }

    private void Host_ClientRunComplete(IRemoteXTMF client, int existStatus, string message)
    {
    }

    private void Host_NewClientConnected(IRemoteXTMF client)
    {
        // Handle a new client
    }

    private void Host_ProgressUpdated(IRemoteXTMF client, float progress)
    {
    }

    private void InitializeCustomMessages()
    {
        lock (Host)
        {
            Host.NewClientConnected += Host_NewClientConnected;
            Host.ProgressUpdated += Host_ProgressUpdated;
            Host.ClientDisconnected += Host_ClientDisconnected;
            Host.ClientRunComplete += Host_ClientRunComplete;
            Host.AllModelSystemRunsComplete += Host_AllModelSystemRunsComplete;
            Host.RegisterCustomSender(1, CustomMessageSender);
            Host.RegisterCustomReceiver(2, CustomMessageReceiver);
            Host.RegisterCustomMessageHandler(2, CustomMessageHandler);
        }
    }

    private void LookAtConnectedClients()
    {
        lock ( Host )
        {
            foreach ( var client in Host.ConnectedClients )
            {
                // Access the client here
            }
        }
    }

    private void RunJobs()
    {
        using ( Jobs = new MessageQueue<int>() )
        {
            while ( !exit )
            {
                var message = Jobs.GetMessageOrTimeout( 200 );
                if ( message != default( int ) )
                {
                    // Process your message here
                }
            }
        }
    }
}