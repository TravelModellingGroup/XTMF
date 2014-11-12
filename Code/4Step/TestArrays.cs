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
using TMG.Input;
using System.IO;
namespace James.UTDM
{
    public class TestArrays : IModelSystemTemplate
    {
        [SubModelInformation( Required = false, Description = "Test children" )]
        public IModule[] ChildrenArray;

        [SubModelInformation( Required = true, Description = "The file to save the names of the modules to." )]
        public FileLocation Output;

        public string InputBaseDirectory
        {
            get;
            set;
        }

        public string OutputBaseDirectory
        {
            get;
            set;
        }

        public bool ExitRequest()
        {
            return false;
        }

        public void Start()
        {
            // do nothing
            if ( this.ChildrenArray == null )
            {
                throw new XTMFRuntimeException( "The Children Array was not initialized!" );
            }
            using ( var writer = new StreamWriter( this.Output.GetFilePath() ) )
            {
                writer.WriteLine( "Contained Module Names:" );
                for ( int i = 0; i < this.ChildrenArray.Length; i++ )
                {
                    writer.WriteLine( this.ChildrenArray[i].Name );
                }
            }
        }

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
            get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
