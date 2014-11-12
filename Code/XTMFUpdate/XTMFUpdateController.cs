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
using System.Net.Sockets;

namespace XTMFUpdate
{
    public class XTMFUpdateController
    {
        public string XTMFDirectory { get; set; }

        public string XTMFUpdateServerLocation { get; set; }

        public int XTMFUpdateServerPort { get; set; }

        public void UpdateCore(Action<float> update = null)
        {
            GetFilesFromServer( ( Environment.Is64BitOperatingSystem ? 1 : 3 ), XTMFDirectory, update );
        }

        public void UpdateModules(Action<float> update = null)
        {
            var dir = Path.Combine( XTMFDirectory, "Modules" );
            if ( !Directory.Exists( dir ) )
            {
                Directory.CreateDirectory( dir );
            }
            GetFilesFromServer( ( Environment.Is64BitOperatingSystem ? 2 : 4 ), dir, update );
        }

        private void GetFilesFromServer(int type, string directory, Action<float> update = null)
        {
            using ( TcpClient client = new TcpClient( XTMFUpdateServerLocation, XTMFUpdateServerPort ) )
            {
                var stream = client.GetStream();
                BinaryWriter writer = new BinaryWriter( stream );
                BinaryReader reader = new BinaryReader( stream );
                writer.Write( type );
                stream.Flush();
                int numberOfFiles = reader.ReadInt32();
                for ( int i = 0; i < numberOfFiles; i++ )
                {
                    string fileName = reader.ReadString();
                    int size = reader.ReadInt32();
                    byte[] temp = new byte[size];
                    int ammount = 0;
                    while ( ammount < size )
                    {
                        ammount += reader.Read( temp, ammount, size - ammount );
                    }
                    File.WriteAllBytes( Path.Combine( directory, fileName ), temp );
                    if ( update != null )
                    {
                        update( i / (float)numberOfFiles );
                    }
                }
                writer.Write( 0 );
                writer.Flush();
            }
        }
    }
}