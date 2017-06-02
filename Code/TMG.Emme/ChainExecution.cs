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
    [ModuleInformation(Description=
        @"The chain execution module is a model system template that allows you to quickly chain together 
multiple ISelfContainedModule and run them sequentially. It does not support canceling. 
The goal of this module was for testing multiple executions of the EMME/3 Modeller Bridge." )]
    public class ChainExecution : IModelSystemTemplate
    {
        [SubModelInformation( Required = true, Description = "The modules to execute" )]
        public List<ISelfContainedModule> ToExecute;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>( 50, 150, 50 );

        [RunParameter( "Input Directory", "../../Input", "The input directory for the Model System" )]
        public string InputBaseDirectory
        {
            get;
            set;
        }

        [RunParameter("Execute", true, "Set this to false in order to skip the execution of it's children.")]
        public bool Execute;

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

        private Func<float> _Progress = () => 0.0f;

        public float Progress
        {
            get { return _Progress(); }
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
            if(Execute)
            {
                var incrment = 1.0f / ToExecute.Count;
                var soFar = 0.0f;
                foreach(var module in ToExecute)
                {
                    var localSoFar = soFar;
                    _Progress = () => localSoFar + incrment * module.Progress;
                    module.Start();
                    soFar += incrment;
                }
                _Progress = () => 1.0f;
            }
        }
    }
}