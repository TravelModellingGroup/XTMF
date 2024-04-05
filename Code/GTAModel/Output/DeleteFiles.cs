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

[ModuleInformation( Description = "Provides a way of deleting files/directories during a model run." )]
public class DeleteFiles : ISelfContainedModule
{
    [SubModelInformation( Required = false, Description = "The paths to the files that should be deleted." )]
    public List<FileLocation> Files;

    [RunParameter( "Ignore Errors", false, "Should we ignore errors such as the file not existing?" )]
    public bool IgnoreErrors;

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
        for ( int i = 0; i < Files.Count; i++ )
        {
            try
            {
                var path = Files[i].GetFilePath();
                if ( Directory.Exists( path ) )
                {
                    Directory.Delete( path, true );
                }
                else
                {
                    File.Delete( path );
                }
            }
            catch ( IOException )
            {
                if ( !IgnoreErrors )
                {
                    throw new XTMFRuntimeException(this, "The file '" + Files[i].GetFilePath() + "' was unable to be deleted." );
                }
            }
        }
    }
}