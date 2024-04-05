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
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Output;

[ModuleInformation( Description = "This module provides the ability to copy a set of files.  The destination path will be replaced" )]
public class CopyFiles : ISelfContainedModule
{
    [RunParameter( "Ignore Copy Errors", false, "Should we ignore copy errors and just continue on?" )]
    public bool IgnoreCopyErrors;

    [SubModelInformation( Required = false, Description = "Definitions of what to copy." )]
    public List<CopyPair> Operations;

    public string Name
    {
        get;
        set;
    }

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

    public void Start()
    {
        for ( int i = 0; i < Operations.Count; i++ )
        {
            string error = null;
            if ( !( Operations[i].Copy(ref error) | IgnoreCopyErrors ) )
            {
                throw new XTMFRuntimeException(this, "We failed to copy the file '"
                    + Operations[i].Source.GetFilePath() + "' to '" + Operations[i].Destination.GetFilePath()
                    + "'.  Please make sure that both paths exist, or in the case of a network drive are online!\r\n" + (error ?? string.Empty) );
            }
        }
    }
    
    [ModuleInformation(
        Description = @"This module provides the information in order to copy between a source to the destination.  It will work for both files and directories.
If a directory with the same name exists already, it will be deleted before the copy begins." )]
    public class CopyPair : IModule
    {
        [RunParameter( "Delete After Copy", false, "Should we delete the source after it has been copied?" )]
        public bool DeleteAfterCopy;

        [SubModelInformation( Required = true, Description = "The location of where to copy the file to." )]
        public FileLocation Destination;

        [SubModelInformation( Required = true, Description = "The location of the file to copy." )]
        public FileLocation Source;

        public string Name { get; set; }

        public float Progress { get { return 0f; } }

        public Tuple<byte, byte, byte> ProgressColour { get { return null; } }

        public bool Copy(ref string error)
        {
            try
            {
                var sourceLocation = Source.GetFilePath();
                if ( Directory.Exists( sourceLocation ) )
                {
                    // check to see if we don't need to make a copy
                    if ( DeleteAfterCopy )
                    {
                        var destinationLocation = Destination.GetFilePath();
                        if ( Directory.Exists( destinationLocation ) )
                        {
                            Directory.Delete( destinationLocation );
                        }
                        Directory.Move( sourceLocation, destinationLocation );
                    }
                    else
                    {
                        DirectoryCopy( sourceLocation, Destination.GetFilePath() );
                    }
                }
                else
                {
                    if ( DeleteAfterCopy )
                    {
                        var destinationLocation = Destination.GetFilePath();
                        if ( File.Exists( destinationLocation ) )
                        {
                            File.Delete( destinationLocation );
                        }
                        File.Move( sourceLocation, destinationLocation );
                    }
                    else
                    {
                        var destinationPath = Destination.GetFilePath();
                        if (Directory.Exists(destinationPath))
                        {
                            File.Copy(sourceLocation, Path.Combine(destinationPath, Path.GetFileName(sourceLocation)), true);
                        }
                        else
                        {
                            File.Copy(sourceLocation, Destination.GetFilePath(), true);
                        }
                    }
                }
            }
            catch ( IOException e)
            {
                error = e.Message;
                return false;
            }
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private void DirectoryCopy(string sourceDirectory, string destinationDirectory)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new( sourceDirectory );
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
                file.CopyTo( temppath, true );
            }

            // If copying subdirectories, copy them and their contents to new location.
            foreach ( DirectoryInfo subdir in dirs )
            {
                string temppath = Path.Combine( destinationDirectory, subdir.Name );
                DirectoryCopy( subdir.FullName, temppath );
            }
        }
    }
}