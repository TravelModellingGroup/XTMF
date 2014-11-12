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
using System.Reflection;
using XTMF.Commands;
using XTMF.Controller;
using XTMF.Networking;

namespace XTMF
{
    public class XTMFRuntime : IDisposable
    {
        /// <summary>
        /// The configuration used for all of the settings
        /// and holding the data for the XTMF installation
        /// </summary>
        public Configuration Configuration;

        /// <summary>
        /// Creates a new instance of XTMF allowing for you to
        /// run all of the systems contained within
        /// </summary>
        public XTMFRuntime(Configuration config = null)
        {
            if ( config != null )
            {
                this.Configuration = config;
            }
            else
            {
                this.Configuration = new Configuration();
            }
            InitializeControllers();
        }

        public IHost ActiveHost
        {
            get
            {
                return this.Configuration.GetActiveHost();
            }
        }

        public ModelSystemController ModelSystemController { get; private set; }

        public NetworkingController NetworkingController { get; private set; }

        public ProjectController ProjectController { get; private set; }

        public RunController RunController { get; private set; }

        public void ExportModelSystem(string fileName, IModelSystem modelSystem)
        {
            List<string> Assemblies = this.GatherAssemblies( modelSystem );
            string tempFile = Path.GetTempFileName();
            modelSystem.ModelSystemStructure.Save( tempFile );
            using ( FileStream fs = new FileStream( fileName, FileMode.Create ) )
            {
                BinaryWriter writer = new BinaryWriter( fs );
                writer.Write( modelSystem.Name );
                writer.Write( Assemblies.Count );
                foreach ( var assembly in Assemblies )
                {
                    this.WriteAssembly( writer, assembly );
                }
                writer.Write( File.ReadAllText( tempFile ) );
            }
            File.Delete( tempFile );
        }

        public IClient InitializeRemoteClient(string address, int port)
        {
            IClient client;
            string error = null;
            this.Configuration.RemoteServerAddress = address;
            this.Configuration.RemoteServerPort = port;
            if ( this.Configuration.StartupNetworkingClient( out client, ref error ) )
            {
                return client;
            }
            return null;
        }

        /// <summary>
        /// Execute a command for XTMF
        /// </summary>
        /// <param name="command">The command to execute</param>
        /// <param name="error">In case of failure a description of the problem</param>
        /// <returns>If the command was successful or not.  If not error will contain a string describing why</returns>
        internal bool ProcessCommand(ICommand command, ref string error)
        {
            // ensure only 1 command at a time
            lock ( this )
            {
                return command.Do( ref error );
            }
        }

        private void AddAssemblies(List<string> list, AssemblyName dep)
        {
            var location = Path.Combine( "Modules", dep.Name + ".dll" );
            if ( !list.Contains( location ) && File.Exists( location ) )
            {
                list.Add( location );
            }
        }

        private List<string> GatherAssemblies(IModelSystem modelSystem)
        {
            List<string> list = new List<string>();
            this.GatherAssemblies( modelSystem.ModelSystemStructure, list );
            return list;
        }

        private void GatherAssemblies(IModelSystemStructure iModelSystemStructure, List<string> list)
        {
            Type t = iModelSystemStructure.Type;
            if ( t != null )
            {
                var dependancies = t.Assembly.GetReferencedAssemblies();
                this.AddAssemblies( list, new AssemblyName( t.Assembly.FullName ) );
                foreach ( var dep in dependancies )
                {
                    this.AddAssemblies( list, dep );
                }
            }
        }

        private void InitializeControllers()
        {
            this.ProjectController = new ProjectController( this );
            this.ModelSystemController = new ModelSystemController( this );
            this.NetworkingController = new NetworkingController( this );
            this.RunController = new RunController( this );
        }

        private void WriteAssembly(BinaryWriter writer, string assembly)
        {
            byte[] assemblyData = File.ReadAllBytes( assembly );
            writer.Write( assembly );
            writer.Write( assemblyData.Length );
            writer.Write( assemblyData );
        }

        public void Dispose()
        {
            this.Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose(bool all)
        {
            if ( this.Configuration != null )
            {
                this.Configuration.Dispose();
                this.Configuration = null;
            }
        }
    }
}