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
namespace TMG.Emme
{
    [ModuleInformation( Description =
@"This module is designed to execute a series of emme tools from a modeller controller that is stored in an XTMF resource." )]
    public class ExecuteToolsFromModellerResource : ISelfContainedModule
    {
        [SubModelInformation( Required = false, Description = "The tools to run in order." )]
        public IEmmeTool[] Tools;

        [SubModelInformation( Required = true, Description = "The name of the resource that has modeller." )]
        public IResource EmmeModeller;

        public void Start()
        {
            var modeller = this.EmmeModeller.AquireResource<ModellerController>();
            var tools = this.Tools;
            int i = 0;
            _Progress = () => ( ( (float)i / tools.Length ) + tools[i].Progress * ( 1.0f / tools.Length ) );
            for ( ; i < tools.Length; i++ )
            {
                tools[i].Execute( modeller );
            }
            _Progress = () => 0f;
        }

        public string Name
        {
            get;
            set;
        }
        private Func<float> _Progress = () => 0f;
        public float Progress
        {
            get { return _Progress(); }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); }
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( !this.EmmeModeller.CheckResourceType<ModellerController>() )
            {
                error = "In '" + this.Name + "' the resource 'EmmeModeller' did not contain an Emme ModellerController!";
                return false;
            }
            return true;
        }
    }
}
