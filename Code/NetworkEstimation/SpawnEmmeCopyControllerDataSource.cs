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
using System.Linq;
using System.Text;
using XTMF;
using TMG.Emme;
using System.IO;
using TMG.Input;
using System.Threading;
namespace TMG.NetworkEstimation
{
    [ModuleInformation( Description =
        @"This module is designed to create a temporary copy of the emme database an execute on that." )]
    public class SpawnEmmeCopyControllerDataSource : IDataSource<ModellerController>, IDisposable
    {
        [SubModelInformation( Required = true, Description = "The location of the Emme project file." )]
        public FileLocation ProjectFile;

        [SubModelInformation( Required = true, Description = "The location to base the temporary copy." )]
        public FileLocation TempBaseDirectory;

        private ModellerController Controller;

        public ModellerController GiveData()
        {
            return this.Controller;
        }

        private string TempDirectory;

        public bool Loaded
        {
            get { return this.Controller != null; }
        }

        public void LoadData()
        {
            if ( this.Controller == null )
            {
                lock ( this )
                {
                    if ( this.Controller == null )
                    {
                        GC.ReRegisterForFinalize( this );
                        var projectFile = this.ProjectFile.GetFilePath();
                        var originalDir = Path.GetDirectoryName( projectFile );
                        projectFile = Path.GetFileName( projectFile );
                        var dir = this.TempDirectory = Path.Combine( TempBaseDirectory.GetFilePath(), DateTime.Now.Ticks.ToString() );
                        DirectoryCopy( originalDir, dir );
                        var actuallyRunning = Path.GetFullPath( Path.Combine( dir, projectFile ) );
                        Console.WriteLine( "Opening EMME at " + actuallyRunning );
                        this.Controller = new ModellerController( actuallyRunning, false );
                    }
                }
            }
        }

        private void DirectoryCopy(string sourceDirectory, string destinationDirectory)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo( sourceDirectory );
            DirectoryInfo[] dirs = dir.GetDirectories();
            if ( !dir.Exists )
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirectory );
            }

            // If the destination directory doesn't exist, create it.
            if ( !Directory.Exists( destinationDirectory ) )
            {
                Directory.CreateDirectory( destinationDirectory );
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach ( FileInfo file in files )
            {
                string temppath = Path.Combine( destinationDirectory, file.Name );
                file.CopyTo( temppath, false );
            }

            // If copying subdirectories, copy them and their contents to new location.
            foreach ( DirectoryInfo subdir in dirs )
            {
                string temppath = Path.Combine( destinationDirectory, subdir.Name );
                DirectoryCopy( subdir.FullName, temppath );
            }
        }

        public void UnloadData()
        {
            //we don't dispose on unload
            //this.Dispose();
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

        ~SpawnEmmeCopyControllerDataSource()
        {
            this.Dispose( true );
        }

        public void Dispose()
        {
            this.Dispose( true );
            GC.SuppressFinalize( true );
        }

        protected virtual void Dispose(bool all)
        {
            if ( this.Controller != null )
            {
                this.Controller.Dispose();
                this.Controller = null;
            }
            // try to close for up to 2 seconds
            if ( this.TempDirectory != null )
            {
                for ( int i = 0; i < 10; i++ )
                {
                    try
                    {
                        Directory.Delete( this.TempDirectory, true );
                        this.TempDirectory = null;
                        break;
                    }
                    catch ( UnauthorizedAccessException )
                    {
                        Thread.Sleep( 200 );
                    }
                    catch ( IOException )
                    {
                        Thread.Sleep( 200 );
                    }
                }
            }
        }
    }
}
