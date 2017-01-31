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
using System.Text;
using XTMF;

namespace TMG.Emme
{
    [ModuleInformation( Name = "Execute Emme Macro", Description = "Executes an Emme macro (usually a .mac file) with user-specified arguments. Included for backwards-compatibility" )]
    public class ExecuteEmmeMacro : IEmmeTool
    {
        private const string ToolName = "tmg.XTMF_internal.run_macro";
        private const string OldToolName = "TMG2.XTMF.RunMacro";

        [RunParameter( "Arguments", "", "A space-separated list of arguments to send to the macro" )]
        public string Arguments;

        [Parameter( "Macro File", "", "The filepath of the macro to execute. It is recommended to surround your path with \"\" quotations" )]
        public string MacroFile;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 1.0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>( 50, 51, 50 ); }
        }

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if ( mc == null )
                throw new XTMFRuntimeException( "Controller is not a modeller controller!" );

            var sb = new StringBuilder();
            sb.AppendFormat( "{0} {1}", MacroFile, Arguments );
            if(mc.CheckToolExists(ToolName))
            {
                return mc.Run(ToolName, sb.ToString());
            }
            else
            {
                return mc.Run(OldToolName, sb.ToString());
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}