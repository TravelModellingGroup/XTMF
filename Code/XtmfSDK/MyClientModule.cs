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

namespace XtmfSDK;

[ModuleInformation(Description =
    @"This is where you would describe what your client should be used for or other information about it.  It can handle <b>HTML</b> tags.")]
public class MyClientModule : IModelSystemTemplate
{
    public IClient Client;
    private static Tuple<byte, byte, byte> _ProgressColour = new( 50, 150, 50 );

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
        if ( Client == null )
        {
            error = "MyClientModule requires an XTMF that supports XTMF.Networking.IClient";
            return false;
        }
        return true;
    }

    public void Start()
    {
        // Your Code goes here
        InitializeCustomMessages();
    }

    private void CustomMessageHandler(object data)
    {
    }

    private object CustomMessageReceiver(Stream inputStream)
    {
        return null;
    }

    private void CustomMessageSender(object data, Stream outputStream)
    {

    }

    private void InitializeCustomMessages()
    {
        Client.RegisterCustomSender( 2, CustomMessageSender );
        Client.RegisterCustomReceiver( 1, CustomMessageReceiver );
        Client.RegisterCustomMessageHandler( 1, CustomMessageHandler );
    }
}