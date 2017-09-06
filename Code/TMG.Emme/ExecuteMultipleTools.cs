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

using XTMF;

namespace TMG.Emme
{
    [ModuleInformation(
        Description = @"Is another model system template very similar to ExecuteModeller however 
instead of containing the tool that you want to run, it takes in a list of IEmmeTool to execute. 
For that list you will probably use EmmeTool as the module to fill in for that, or a new custom module."
        )]
    public class ExecuteMultipleTools : IModelSystemTemplate
    {
        [RunParameter("Emme Project File", "*.emp", "The path to the Emme project file (.emp)")]
        public string EmmeProjectFile;

        [RunParameter( "Execute EMME", true, "Should we execute EMME?  Set this to false in order to not run EMME." )]
        public bool Execute;

        [SubModelInformation( Required = false, Description = "The tools to execute" )]
        public List<IEmmeTool> Tools;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>( 50, 150, 50 );

        private Func<float> CalculateProgress;

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
            get { return CalculateProgress == null ? 0 : CalculateProgress(); }
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
            if ( Tools.Count == 0 | !Execute ) return;
            using ( Controller controller = new ModellerController(this, EmmeProjectFile ) )
            {
                var length = Tools.Count;
                int i = 0;
                // ReSharper disable AccessToModifiedClosure
                CalculateProgress = () => Tools[i].Progress * ( 1.0f / length ) + ( i / (float)length );
                for ( ; i < length; i++ )
                {
                    Tools[i].Execute( controller );
                }
            }
            CalculateProgress = null;
        }
    }
}