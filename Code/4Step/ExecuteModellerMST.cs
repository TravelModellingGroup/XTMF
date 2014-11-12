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

namespace James.UTDM
{
    public class ExecuteModellerMST : IModelSystemTemplate
    {
        [RunParameter("Project File", "*.emp", "The location of the Emme project's directory.")]
        public string EmmeProjectFile;

        [RunParameter("Tool Name", "tmg.test.TestXTMF", "The tool to execute")]
        public string MacroName;

        [RunParameter("Tool Parameters", "2 3", "The parameters to execute the tool with.")]
        public string MacroArguments;

        [RunParameter( "Performance Testing", false, "Test the performance of the tool, results are saved in the logbook." )]
        public bool PerformanceTesting;

        [RunParameter( "Clean Logbook", false, "Delete the logbook before running." )]
        public bool CleanLogbook;

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
            // Create the bridge between XTMF and emme's modeller
            using ( ModellerController emme = new ModellerController( this.EmmeProjectFile, this.PerformanceTesting ) )
            {
                if ( this.CleanLogbook )
                {
                    emme.CleanLogbook();
                }
                // now that we have the connection, run the tool
                emme.Run(this.MacroName, this.MacroArguments);
            }
        }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        private static Tuple<byte, byte, byte> _Colour = new Tuple<byte, byte, byte>(50, 100, 50);
        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return _Colour; }
        }

        public bool RuntimeValidation(ref string error)
        {
            if (String.IsNullOrWhiteSpace(this.MacroName))
            {
                error = "ExecuteModellerMST requres that the Tool's name want to execute's name is defined.";
                return false;
            }
            return true;
        }
    }
}
