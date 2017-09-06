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
    [ModuleInformation(Description=
        @"The Emme Tool module takes in two parameters, a name of the tool, and the arguments to it. 
This module will work with both the traditional EMME macro system and the new modeller system. 
This module is unable to verify the existence of the modeller tool during the runtime validation step 
so please be careful to enter in the correct tool/macro names." )]
    public class EmmeTool : IEmmeTool
    {
        [RunParameter( "Tool Arguments", "", "The arguments of the Emme tool you want to run" )]
        public string ToolArguments;

        [RunParameter( "Tool Name", "", "The namespace of the Emme tool you want to run" )]
        public string ToolName;

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

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool Execute(Controller controller)
        {
            return controller.Run(this, ToolName, ToolArguments );
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}