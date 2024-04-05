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
namespace TMG.Estimation.Utilities
{
    public abstract class ClientFileAggregation : IModule
    {
        public IClient Client;

        [RunParameter( "DataChannel", 11, "The networking channel to use, must be unique and the same as the host!" )]
        public int DataChannel;

        private bool Loaded;

        protected void SendToHost(string toSend)
        {
            if ( !Loaded )
            {
                Client.RegisterCustomSender( DataChannel, (data, stream) =>
                    {
                        BinaryWriter writer = new( stream );
                        var str = ( (string)data ).ToCharArray();
                        writer.Write( str, 0, str.Length );
                        writer.Flush();
                    } );
                Loaded = true;
            }
            Client.SendCustomMessage( toSend, DataChannel );
        }

        public string Name { get; set; }

        public abstract float Progress
        {
            get;
        }

        public abstract Tuple<byte, byte, byte> ProgressColour
        {
            get;
        }

        public abstract bool RuntimeValidation(ref string error);
    }
}
