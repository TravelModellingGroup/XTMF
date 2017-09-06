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
using TMG.Emme;
using XTMF;

namespace TMG.NetworkEstimation
{
    public class ExecuteToolsFromResource : IModelSystemTemplate
    {
        [SubModelInformation( Required = true, Description = "An instance of ModellerController" )]
        public IResource ResourceToEmme;

        [RunParameter( "Input Directory", "../../Input", "The directory relative to the run path that contains the input." )]
        public string InputBaseDirectory { get; set; }

        [SubModelInformation( Required = false, Description = "The list of tools to execute" )]
        public List<IEmmeTool> Tools;

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
            _Progress = () => 0f;
            var controller = ResourceToEmme.AcquireResource<ModellerController>();
            if ( controller == null )
            {
                throw new XTMFRuntimeException(this, "In '' the EMME Modeller controller resource did not contain a modeller controller!");
            }
            int i = 0;
            // ReSharper disable once AccessToModifiedClosure
            _Progress = () => (float)i / Tools.Count;
            for ( ; i < Tools.Count; i++ )
            {
                Tools[i].Execute( controller );
            }
            _Progress = () => 1f;
        }

        public string Name { get; set; }

        private Func<float> _Progress = () => 0f;

        public float Progress
        {
            get { return _Progress(); }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( !ResourceToEmme.CheckResourceType<ModellerController>() )
            {
                error = "In '" + Name + "' the resource must be returning a ModellerController!";
                return false;
            }
            return true;
        }
    }
}
