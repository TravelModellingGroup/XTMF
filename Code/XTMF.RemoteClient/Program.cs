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
using Microsoft.Win32;

namespace XTMF.RemoteClient
{
    internal class Program
    {
        private static PowerModeChangedEventHandler PowerHandeler = SystemEvents_PowerModeChanged;

        private static void Main(string[] args)
        {
            SystemEvents.PowerModeChanged += PowerHandeler;
            int port;
            if ( args.Length < 2 )
            {
                Console.WriteLine( "Usage: XTMF.RemoteClient.exe serverAddress severPort [<Optional>ConfigurationFile]" );
                return;
            }
            if ( !int.TryParse( args[1], out port ) )
            {
                Console.WriteLine( "The port needs to be number!\r\nUsage: XTMF.RemoteClient.exe [serverAddress] [severPort] [<Optional>ConfigurationFile]" );
                return;
            }
            Configuration config = null;
            if (args.Length >= 3 )
            {
                Console.WriteLine( "Using alternative configuration file '" + args[2] + "'" );
                config = new Configuration( args[2] );
            }
            XTMFRuntime xtmf = new XTMFRuntime(config);
            // fire up the remote client engine
            if ( xtmf.InitializeRemoteClient( args[0], port ) == null )
            {
                Console.WriteLine( "We were unable to start up the remote client.  Please ensure that the server address and port were correct!" );
                return;
            }
            Console.WriteLine( "Remote Client Activated" );
        }

        private static void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if ( e.Mode == PowerModes.Suspend )
            {
                SystemEvents.PowerModeChanged -= PowerHandeler;
                Environment.Exit( 0 );
            }
        }
    }
}