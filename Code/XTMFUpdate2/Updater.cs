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
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace XTMF.Update
{
    internal class Updater
    {
        public static void Main(string[] args)
        {
            if ( args.Length < 2 ) return;
            for ( int i = 0; i < args.Length; i += 2 )
            {
                // make sure that we don't process the application we are supposed to startup
                if(i + 1 == args.Length)
                {
                    break;
                }
                for ( int times = 0; times < 5; times++ )
                {
                    try
                    {
                        // overwrite the old updater
                        File.Copy( args[i], args[i + 1], true );
                        File.Delete( args[i] );
                        break;
                    }
                    catch ( Exception e )
                    {
                        Console.WriteLine( "The updater is still busy waiting and trying again..." );
                        Console.WriteLine( e.Message );
                        Thread.Sleep( 200 );
                    }
                }
            }
            if(args.Length % 2 == 1)
            {
                var launchProgram = args[args.Length - 1];
                Process.Start(launchProgram);
            }
        }
    }
}