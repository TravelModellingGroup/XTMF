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
using System.Xml;

namespace XTMF.ConsoleUpdate
{
    public class UpdateController
    {
        public bool UseWebservices;
        private string ConfigFile;

        public UpdateController()
        {
            LoadConfiguration();
        }

        public string XTMFUpdateServerLocation { get; set; }

        public int XTMFUpdateServerPort { get; set; }

        public void UpdateAll(bool force32, bool force64, Action<float> update = null, Action<string> status = null)
        {
            bool x64 = Environment.Is64BitOperatingSystem;
            if ( force32 )
            {
                x64 = false;
            }
            else if ( force64 )
            {
                x64 = true;
            }
            WriteConfigFile();
            WriteIfNotNull( status, "Updating XTMF Core" );
            var appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var containingDirectory = Path.GetDirectoryName( appPath );
            if (appPath == null)
            {
                throw new Exception($"Unable to get the containing directory from the assembly!");
            }
            var excludedPaths = new[]
            {
                appPath,
                Path.Combine(containingDirectory, String.Concat(Path.GetFileNameWithoutExtension(appPath),".pdb")),
                Path.Combine(containingDirectory, String.Concat(Path.GetFileNameWithoutExtension(appPath),".vshost.exe")),
                Path.Combine(containingDirectory, String.Concat(Path.GetFileNameWithoutExtension(appPath),".XmlSerializers.dll"))
            };
            try
            {
                var ourNewAssemblyPath = UpdateCore( x64, excludedPaths, update );
                WriteIfNotNull( status, "Updating XTMF Modules" );
                UpdateModules( x64, update );
                WriteIfNotNull( status, "Update Complete" );
                RebootAndCopyBase( ourNewAssemblyPath, excludedPaths );
            }
            catch ( Exception e )
            {
                Console.WriteLine( e.Message, "Error connecting to server" );
            }
        }

        public string[] UpdateCore(bool x64, string[] excludedPaths, Action<float> update = null)
        {
            var appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var containingDirectory = Path.GetDirectoryName( appPath );
            GetFilesFromServer( ( x64 ? 1 : 3 ), containingDirectory, excludedPaths,
                out string[] routedPaths, update );
            return routedPaths;
        }

        public void UpdateModules(bool x64, Action<float> update = null)
        {
            var appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var containingDirectory = Path.GetDirectoryName( appPath );
            if (containingDirectory == null)
            {
                throw new Exception($"Unable to get the containing directory from the binary!");
            }
            var dir = Path.Combine( containingDirectory, "Modules" );
            if ( !Directory.Exists( dir ) )
            {
                Directory.CreateDirectory( dir );
            }

            GetFilesFromServer((x64 ? 2 : 4), dir, null, out string[] routedPaths, update);
        }

        private static void DisplayErrors(List<string> errors)
        {
            if ( errors != null )
            {
                foreach ( var errorPath in errors )
                {
                    Console.WriteLine( "Unable to write to file \"" + errorPath + "\"!" );
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

        private string BuildParameters(string[] tempPath, string[] destination)
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
            if ( UseWebservices )
            {
                // TODO: Implement Web Services
#if FALSE
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
                        // since we have our reader we no longer need our link to the stream
                        stream = null;
                        errors = SaveFiles( directory, redirectPaths, redirectedPaths, update, ref errors, reader );
                    }
                }
                finally
                {
                    stream?.Dispose();
                }
#endif
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
                    errors = SaveFiles( directory, redirectPaths, redirectedPaths, update, ref errors, reader );
                    writer.Write( 0 );
                    writer.Flush();
                }
            }
            DisplayErrors( errors );
        }

        private void LoadConfigFile()
        {
            XmlDocument doc = new XmlDocument();
            doc.Load( ConfigFile );
            var root = doc["Root"];
            if (root == null)
            {
                throw new Exception("Invalid configuration file, there is no Root element.");
            }
            var port = root["XTMFUpdateServerPort"]?.InnerText;
            XTMFUpdateServerLocation = root["XTMFUpdateServerLocation"]?.InnerText;
            XTMFUpdateServerPort = port == null ? 1448 : int.Parse( port );
            var webserviceNode = root["UseWebservices"];
            if ( webserviceNode != null )
            {
                bool.TryParse( webserviceNode.InnerText, out UseWebservices );
            }
        }

        private void LoadConfiguration()
        {
            // The Default
            XTMFUpdateServerLocation = "tmg.utoronto.ca";
            XTMFUpdateServerPort = 1448;
            UseWebservices = false;

            // try to read from file
            var directory = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create ),
                "XTMF" );
            ConfigFile = Path.Combine( directory, "UpdateConfig.xml" );
            try
            {
                if ( File.Exists( ConfigFile ) )
                {
                    LoadConfigFile();
                }
            }
            catch(IOException)
            {
            }
        }

        private void RebootAndCopyBase(string[] tempPath, string[] destination)
        {
            var appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var containingDirectory = Path.GetDirectoryName( appPath );
            if (containingDirectory == null)
            {
                throw new Exception("We were unable to get the continaing directory from the assembly!");
            }
            try
            {
                // Launch the process that will update this program
                Process.Start( Path.Combine( containingDirectory, "XTMF.UpdateCore.exe" ),
                    BuildParameters( tempPath, destination ) );
            }
            catch
            {
                Console.WriteLine( "We were unable to start up XTMF.UpdateCore.exe!" );
            }
            finally
            {
                // terminate
                Environment.Exit( 0 );
            }
        }

        private List<string> SaveFiles(string directory, string[] redirectPaths, string[] redirectedPaths, Action<float> update, ref List<string> errors, BinaryReader reader)
        {
            int numberOfFiles = reader.ReadInt32();
            for ( int i = 0; i < numberOfFiles; i++ )
            {
                string fileName = reader.ReadString();
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
            var dirName = Path.GetDirectoryName( ConfigFile );
            if (dirName == null)
            {
                throw new Exception($"We were unable get the directory name from the configuration file location: '{ConfigFile}'!");
            }
            if ( !Directory.Exists( dirName ) )
            {
                Directory.CreateDirectory( dirName );
            }
            using ( XmlWriter writer = XmlWriter.Create( ConfigFile, new XmlWriterSettings() { Encoding = Encoding.Unicode, Indent = true } ) )
            {
                writer.WriteStartDocument();
                writer.WriteStartElement( "Root" );

                writer.WriteStartElement( "XTMFUpdateServerLocation" );
                writer.WriteString( XTMFUpdateServerLocation );
                writer.WriteEndElement();

                writer.WriteStartElement( "XTMFUpdateServerPort" );
                writer.WriteString( XTMFUpdateServerPort.ToString() );
                writer.WriteEndElement();

                writer.WriteStartElement( "UseWebservices" );
                writer.WriteString( UseWebservices.ToString() );
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