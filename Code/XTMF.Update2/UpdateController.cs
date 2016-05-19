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
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using System.Xml;

namespace XTMF.Update
{
    public class UpdateController
    {
        public bool UseWebservices;
        private string ConfigFile;

        public UpdateController()
        {
            this.LoadConfiguration();
        }

        public string XTMFUpdateServerLocation { get; set; }

        public int XTMFUpdateServerPort { get; set; }

        public void UpdateAll(bool force32, bool force64, bool xtmfOnly, Action<float> Update = null, Action<string> status = null, string launchAfter = null)
        {
            bool x64 = Environment.Is64BitOperatingSystem;
            if ( force32 )
            {
                x64 = false;
            }
            else if ( force32 )
            {
                x64 = true;
            }
            this.WriteConfigFile();
            WriteIfNotNull( status, "Updating XTMF Core" );
            var containingDirectory = Path.GetDirectoryName( Application.ExecutablePath );
            var excludedPaths = new string[]
            {
                Application.ExecutablePath,
                Path.Combine(containingDirectory, String.Concat(Path.GetFileNameWithoutExtension(Application.ExecutablePath),".pdb")),
                Path.Combine(containingDirectory, String.Concat(Path.GetFileNameWithoutExtension(Application.ExecutablePath),".vshost.exe")),
                Path.Combine(containingDirectory, String.Concat(Path.GetFileNameWithoutExtension(Application.ExecutablePath),".XmlSerializers.dll"))
            };
            try
            {
                var ourNewAssemblyPath = UpdateCore( x64, excludedPaths, Update );
                WriteIfNotNull( status, "Updating Modules" );
                if (!xtmfOnly)
                {
                    UpdateModules(x64, Update);
                }
                WriteIfNotNull( status, "Update Complete" );
                this.RebootAndCopyBase( ourNewAssemblyPath, excludedPaths, launchAfter );
            }
            catch ( Exception e )
            {
                MessageBox.Show( e.Message, "Error connecting to server", MessageBoxButtons.OK, MessageBoxIcon.Error );
            }
        }

        private void UpdateSourceCode(Action<float> Update)
        {
            if ( !Directory.Exists( "Code" ) )
            {
                Directory.CreateDirectory( "Code" );
            }
            // actually getting the source code is not available yet.
            string[] redirectedPaths;
            GetFilesFromServer( -1, "Code", new string[0], out redirectedPaths, Update );
        }

        public string[] UpdateCore(bool x64, string[] excludedPaths, Action<float> update = null)
        {
            string[] routedPaths;
            var containingDirectory = Path.GetDirectoryName( Application.ExecutablePath );
            GetFilesFromServer( ( x64 ? 1 : 3 ), containingDirectory, excludedPaths,
                out routedPaths, update );
            return routedPaths;
        }

        public void UpdateModules(bool x64, Action<float> update = null)
        {
            var containingDirectory = Path.GetDirectoryName( Application.ExecutablePath );
            var dir = Path.Combine( containingDirectory, "Modules" );
            if ( !Directory.Exists( dir ) )
            {
                Directory.CreateDirectory( dir );
            }
            string[] routedPaths;
            GetFilesFromServer( ( x64 ? 2 : 4 ), dir, null, out routedPaths, update );
        }

        private static void DisplayErrors(List<string> errors)
        {
            if ( errors != null )
            {
                foreach ( var errorPath in errors )
                {
                    MessageBox.Show( "Unable to write to file \"" + errorPath + "\"!" );
                }
            }
        }

        private static int GetRedirectIndex(string[] redirectPaths, string destinationPath)
        {
            for ( int j = 0; j < redirectPaths.Length; j++ )
            {
                if ( destinationPath.Equals( redirectPaths[j], StringComparison.InvariantCultureIgnoreCase ) )
                {
                    return j;
                }
            }
            return -1;
        }

        private string BuildParameters(string[] tempPath, string[] destination, string launchAfter)
        {
            StringBuilder builder = new StringBuilder();
            for ( int i = 0; i < tempPath.Length; i++ )
            {
                if ( tempPath[i] != null && destination[i] != null )
                {
                    builder.Append( '"' );
                    builder.Append( tempPath[i] );
                    builder.Append( '"' );
                    builder.Append( ' ' );
                    builder.Append( '"' );
                    builder.Append( destination[i] );
                    builder.Append( '"' );
                    builder.Append( ' ' );
                }
            }
            if(launchAfter != null)
            {
                builder.Append('"');
                builder.Append(launchAfter);
                builder.Append('"');
            }
            var combined = builder.ToString();
            return combined;
        }

        private void GetFilesFromServer(int type, string directory, string[] redirectPaths, out string[] redirectedPaths, Action<float> update = null)
        {
            List<string> errors = null;
            if ( redirectPaths == null )
            {
                redirectedPaths = null;
            }
            else
            {
                redirectedPaths = new string[redirectPaths.Length];
            }
            if ( this.UseWebservices )
            {
                byte[] data;
                using ( TMG.WebUpdate.XTMFUpdateWebservice webservice = new TMG.WebUpdate.XTMFUpdateWebservice() )
                {
                    var address = XTMFUpdateServerLocation;
                    if ( !address.StartsWith( "http://", StringComparison.InvariantCultureIgnoreCase ) )
                    {
                        address = "http://" + address;
                    }
                    if ( !address.EndsWith( "XTMFUpdate.asmx" ) )
                    {
                        address = address + "/XTMFUpdate.asmx";
                    }
                    webservice.Url = address;
                    switch ( type )
                    {
                        case -1:
                            data = webservice.GetCodeFiles();
                            break;
                        case 1:
                            data = webservice.GetCoreFiles( true );
                            break;

                        case 2:
                            data = webservice.GetModuleFiles( true );
                            break;

                        case 3:
                            data = webservice.GetCoreFiles( false );
                            break;

                        case 4:
                            data = webservice.GetModuleFiles( false );
                            break;
                        default:
                            return;
                    }
                }
                MemoryStream stream = null;
                try
                {
                    stream = new MemoryStream( data );
                    using ( BinaryReader reader = new BinaryReader( stream ) )
                    {
                        stream = null;
                        errors = SaveFiles( directory, redirectPaths, redirectedPaths, update, errors, reader );
                    }
                }
                finally
                {
                    if ( stream != null )
                    {
                        stream.Dispose();
                        stream = null;
                    }
                }
            }
            else
            {
                using ( TcpClient client = new TcpClient( XTMFUpdateServerLocation, XTMFUpdateServerPort ) )
                {
                    var stream = client.GetStream();
                    BinaryWriter writer = new BinaryWriter( stream );
                    BinaryReader reader = new BinaryReader( stream );
                    writer.Write( type );
                    stream.Flush();
                    errors = SaveFiles( directory, redirectPaths, redirectedPaths, update, errors, reader );
                    writer.Write( 0 );
                    writer.Flush();
                }
            }
            DisplayErrors( errors );
        }

        private void LoadConfigFile()
        {
            XmlDocument doc = new XmlDocument();
            doc.Load( this.ConfigFile );
            var root = doc["Root"];
            this.XTMFUpdateServerLocation = root["XTMFUpdateServerLocation"].InnerText;
            this.XTMFUpdateServerPort = int.Parse( root["XTMFUpdateServerPort"].InnerText );
            var webserviceNode = root["UseWebservices"];
            if ( webserviceNode != null )
            {
                bool.TryParse( webserviceNode.InnerText, out this.UseWebservices );
            }
        }

        private void LoadConfiguration()
        {
            // The Default
            this.XTMFUpdateServerLocation = "tmg.utoronto.ca";
            this.XTMFUpdateServerPort = 1448;
            this.UseWebservices = false;

            // try to read from file
            var directory = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create ),
                "XTMF" );
            this.ConfigFile = Path.Combine( directory, "UpdateConfig.xml" );
            try
            {
                if ( File.Exists( this.ConfigFile ) )
                {
                    LoadConfigFile();
                }
            }
            catch
            {
            }
        }

        private void RebootAndCopyBase(string[] tempPath, string[] destination, string launchAfter)
        {
            var containingDirectory = Path.GetDirectoryName( Application.ExecutablePath );
            try
            {
                // Launch the process that will update this program
                Process.Start( Path.Combine( Application.StartupPath, "XTMF.UpdateCore.exe" ),
                    BuildParameters( tempPath, destination, launchAfter) );
            }
            catch
            {
                MessageBox.Show( "We were unable to start up XTMF.UpdateCore.exe!" );
            }
            finally
            {
                // terminate
                Application.Exit();
            }
        }

        private List<string> SaveFiles(string directory, string[] redirectPaths, string[] redirectedPaths, Action<float> update, List<string> errors, BinaryReader reader)
        {
            int numberOfFiles = reader.ReadInt32();
            for ( int i = 0; i < numberOfFiles; i++ )
            {
                string fileName = reader.ReadString();
                if ( fileName.Length != 0 )
                {
                    if ( ( fileName[0] == '/' ) | ( fileName[0] == '\\' ) )
                    {
                        fileName = fileName.Substring( 1 );
                    }
                }
                var destinationPath = Path.GetFullPath( Path.Combine( directory, fileName ) );
                if ( redirectPaths != null )
                {
                    int index = GetRedirectIndex( redirectPaths, destinationPath );
                    if ( index >= 0 )
                    {
                        redirectedPaths[index] = destinationPath = Path.GetTempFileName();
                    }
                }
                int size = reader.ReadInt32();
                byte[] temp = new byte[size];
                int ammount = 0;
                while ( ammount < size )
                {
                    ammount += reader.Read( temp, ammount, size - ammount );
                }
                try
                {
                    var dir = Path.GetDirectoryName( destinationPath );
                    if ( !String.IsNullOrEmpty( dir ) )
                    {
                        if ( !Directory.Exists( dir ) )
                        {
                            Directory.CreateDirectory( dir );
                        }
                    }
                    File.WriteAllBytes( destinationPath, temp );
                }
                catch
                {
                    if ( errors == null )
                    {
                        errors = new List<string>( 5 );
                    }
                    errors.Add( destinationPath );
                }
                WriteIfNotNull( update, i / (float)numberOfFiles );
            }
            return errors;
        }

        private void WriteConfigFile()
        {
            var dirName = Path.GetDirectoryName( this.ConfigFile );
            if ( !Directory.Exists( dirName ) )
            {
                Directory.CreateDirectory( dirName );
            }
            using ( XmlWriter writer = XmlTextWriter.Create( this.ConfigFile, new XmlWriterSettings() { Encoding = Encoding.Unicode, Indent = true } ) )
            {
                writer.WriteStartDocument();
                writer.WriteStartElement( "Root" );

                writer.WriteStartElement( "XTMFUpdateServerLocation" );
                writer.WriteString( this.XTMFUpdateServerLocation );
                writer.WriteEndElement();

                writer.WriteStartElement( "XTMFUpdateServerPort" );
                writer.WriteString( this.XTMFUpdateServerPort.ToString() );
                writer.WriteEndElement();

                writer.WriteStartElement( "UseWebservices" );
                writer.WriteString( this.UseWebservices.ToString() );
                writer.WriteEndElement();

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        private void WriteIfNotNull<T>(Action<T> func, T data)
        {
            if ( func != null )
            {
                func( data );
            }
        }
    }
}