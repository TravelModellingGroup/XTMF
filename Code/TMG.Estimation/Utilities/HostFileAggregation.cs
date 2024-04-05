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
using XTMF.Networking;
using TMG.Input;
using System.IO;
using System.Threading.Tasks;

namespace TMG.Estimation.Utilities
{
    [ModuleInformation( Description =
        @"This module is designed to allow client model systems to send data back to the host and to save it to the output file." )]
    public sealed class HostFileAggregation : ISelfContainedModule
    {
        [SubModelInformation( Required = true, Description = "The place to save the file." )]
        public FileLocation OutputFile;

        [RunParameter( "Header", "", "The header to apply to the file, leave blank to not have a header." )]
        public string Header;

        /// <summary>
        /// The connection to the XTMF host
        /// </summary>
        public IHost Host;

        [RunParameter( "DataChannel", 11, "The networking channel to use, must be unique and the same as the client!" )]
        public int DataChannel;

        private bool Loaded;

        private object WriteLock = new();

        public void Start()
        {
            if ( !Loaded )
            {
                if ( !string.IsNullOrWhiteSpace( Header ) )
                {
                    using var writer = new StreamWriter(OutputFile);
                    writer.WriteLine(Header);
                }
                Host.RegisterCustomReceiver( DataChannel, (stream, remote) =>
                    {
                        byte[] data = new byte[stream.Length];
                        stream.Read( data, 0, data.Length );
                        return data;
                    } );
                Host.RegisterCustomMessageHandler( DataChannel, (dataObj, remote) =>
                    {
                        var data = dataObj as byte[];
                        if (data == null)
                        {
                            throw new XTMFRuntimeException(this, $"In {Name} we recieved something besides a byte[] while building a file.");
                        }
                        Task.Factory.StartNew( () =>
                            {
                                lock ( WriteLock )
                                {
                                    using var writer = File.Open(OutputFile, FileMode.Append);
                                    writer.Write(data, 0, data.Length);
                                    writer.Flush();
                                }
                            } );
                    } );
                Loaded = true;
            }
        }

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
