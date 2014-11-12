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

namespace TMG.Emme
{
    [ModuleInformation(Description=@"Execute Modeller is similar to EmmeTool however 
it is a model system template and will create the connection to EMME through the modeller interface. 
In addition to the parameters in EmmeTool ExecuteModeller requires a 
path to where the Emme project is located. It also has another parameter called “Performance Testing”. If you enable this inside of the modeller logbook the time it takes to execute the tool will be recorded.")]
    public class ExecuteModeller : IModelSystemTemplate
    {
        [RunParameter( "Clean Logbook", false, "Delete the logbook before running." )]
        public bool CleanLogbook;

        [RunParameter("Emme Project File", "*.emp", "The path to the Emme project file (.emp)")]
        public string EmmeProjectFile;

        [RunParameter( "Emme Tool Arguments", "", "The arguments to pass to this tool" )]
        public string EmmeToolArguments;

        [RunParameter( "Emme Tool Name", "", "The name of the tool to execute" )]
        public string EmmeToolName;

        [RunParameter( "Performance Testing", false, "Test the performance of the tool, results are saved in the logbook." )]
        public bool PerformanceTesting;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>( 50, 150, 50 );

        [RunParameter( "Input Directory", "../../Input", "The input directory for the Model System" )]
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
            return false;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            using ( ModellerController controller = new ModellerController( this.EmmeProjectFile, this.PerformanceTesting ) )
            {
                if ( this.CleanLogbook )
                {
                    controller.CleanLogbook();
                }
                string ret = null;
                controller.Run( this.EmmeToolName, this.EmmeToolArguments, (p) => this.Progress = p, ref ret );
            }
        }
    }
}