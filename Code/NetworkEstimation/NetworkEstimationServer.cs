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
using System.IO;
using System.Threading;
using XTMF;
using XTMF.Networking;

namespace TMG.NetworkEstimation
{
    public class NetworkEstimationServer : IModelSystemTemplate
    {
        /// <summary>
        /// Our connection to the XTMF host module
        /// </summary>
        public IHost Host;

        [RunParameter( "Network Base Directory", @"D:\Networks\2006", "The original data bank's base directory" )]
        public string NetworkBaseDirectory;

        [RunParameter( "Estimation MS", "Network Estimation", "The name of the Network Estimation Model System." )]
        public string NetworkEstimationModelSystemName;

        [RunParameter( "#Children", 5, "The number of times we should spawn." )]
        public int NumberOfChildren;

        [RunParameter( "ResultFile", "ParameterEvaluation.csv", "Parameter Evaluation File." )]
        public string ParameterEvaluationFile;

        [RunParameter( "ResultPort", 12345, "The Custom Port to use for sending back the results" )]
        public int ResultPort;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>( 50, 150, 50 );

        private volatile bool Canceled;

        private int CompletedCount;

        /// <summary>
        /// Our connection to the XTMF Configuration so we can setup individual progress
        /// </summary>
        private IConfiguration Configuration;

        private List<IRemoteXTMF> ConnectedClients = [];

        private Random RandomNumberGenerator;

        public NetworkEstimationServer(IConfiguration config)
        {
            Configuration = config;
        }

        public string InputBaseDirectory
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

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
            // Kill Everything
            Canceled = true;
            if ( Host != null )
            {
                lock ( this )
                {
                    foreach ( var client in ConnectedClients )
                    {
                        client.SendCancel( "Cancel Requested By User" );
                    }
                }
                Host.Shutdown();
            }
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            Host.NewClientConnected += Host_NewClientConnected;
            Host.ProgressUpdated += Host_ProgressUpdated;
            Host.ClientRunComplete += Host_ClientRunComplete;
            Host.RegisterCustomReceiver( ResultPort, delegate(Stream s, IRemoteXTMF r)
            {
                var length = s.Length / 4;
                BinaryReader reader = new BinaryReader( s );
                float[] res = new float[length];
                for ( int i = 0; i < length; i++ )
                {
                    res[i] = reader.ReadSingle();
                }
                return res;
            } );
            Host.RegisterCustomMessageHandler( ResultPort, delegate(object result, IRemoteXTMF remote)
            {
                var set = result as float[];
                if ( set == null || set.Length == 0 ) return;
                var length = set.Length;
                lock ( this )
                {
                    using ( StreamWriter writer = new StreamWriter( ParameterEvaluationFile, true ) )
                    {
                        writer.Write( set[0] );
                        for ( int i = 0; i < length; i++ )
                        {
                            writer.Write( ',' );
                            writer.Write( set[i] );
                        }
                        writer.WriteLine();
                    }
                }
            } );
            RandomNumberGenerator = new Random();
            string error = null;
            var baseModelSystem = Host.CreateModelSystem( NetworkEstimationModelSystemName, ref error );
            if ( baseModelSystem == null )
            {
                throw new XTMFRuntimeException(this, error );
            }
            for ( int i = 0; i < NumberOfChildren; i++ )
            {
                var msscopy = baseModelSystem.Clone();
                EditModelSystemTemplate( msscopy, i );
                Host.ExecuteModelSystemAsync( msscopy );
            }

            // We just keep going
            while ( !( Canceled || CompletedCount == NumberOfChildren ) )
            {
                Thread.Sleep( 250 );
                Thread.MemoryBarrier();
            }
        }

        private void EditModelSystemTemplate(IModelSystemStructure msscopy, int i)
        {
            IModuleParameters parameterList;
            // First edit our children
            foreach ( var child in msscopy.Children )
            {
                if ( child.ParentFieldType.Name == "INetworkEstimationAI" )
                {
                    parameterList = child.Parameters;
                    foreach ( var param in parameterList.Parameters )
                    {
                        if ( param.Name == "Random Seed" )
                        {
                            param.Value = RandomNumberGenerator.Next();
                        }
                    }
                }
                else if ( child.ParentFieldType.Name == "INetworkAssignment" )
                {
                    parameterList = child.Parameters;
                    foreach ( var param in parameterList.Parameters )
                    {
                        if ( param.Name == "Emme Project Folder" )
                        {
                            param.Value = $"{NetworkBaseDirectory}-{(i + 1)}/Database";
                        }
                    }
                }
            }
            // after they are setup we just need to tune a couple of our parameters
            parameterList = msscopy.Parameters;
            foreach ( var param in parameterList.Parameters )
            {
                if ( param.Name == "Emme Input Output" )
                {
                    param.Value = $"{NetworkBaseDirectory}-{(i + 1)}/Database/cache/scalars.311";
                }
                else if ( param.Name == "Emme Macro Output" )
                {
                    param.Value = $"{NetworkBaseDirectory}-{(i + 1)}/Database/cache/boardings_predicted.621";
                }
            }
        }

        private void Host_ClientRunComplete(IRemoteXTMF arg1, int arg2, string arg3)
        {
            Interlocked.Increment( ref CompletedCount );
        }

        private void Host_NewClientConnected(IRemoteXTMF obj)
        {
            lock ( this )
            {
                ConnectedClients.Add( obj );
            }
            Configuration.CreateProgressReport(obj.UniqueID ?? "Remote Host", delegate {
                return obj.Progress;
            }, new Tuple<byte, byte, byte>( 150, 50, 50 ) );
        }

        private void Host_ProgressUpdated(IRemoteXTMF arg1, float arg2)
        {
            var totalProgress = 0f;
            lock ( this )
            {
                foreach ( var client in ConnectedClients )
                {
                    totalProgress += client.Progress;
                }
            }
            Progress = totalProgress / NumberOfChildren;
        }
    }
}