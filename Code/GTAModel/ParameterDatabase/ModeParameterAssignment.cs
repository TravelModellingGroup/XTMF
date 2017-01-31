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
using System.Threading.Tasks;
using TMG.ParameterDatabase;
using XTMF;

namespace TMG.GTAModel.ParameterDatabase
{
    public class ModeParameterAssignment : IModeParameterAssignment
    {
        [RunParameter( "Ignore Bad Parameters", false, "Ignore parameters that don't exist." )]
        public bool IgnoreBadParameters;

        public List<IParameterLink> Links;

        [RunParameter( "Mode Name", "Auto", "The name of the mode to bind to." )]
        public string ModeName;

        [RootModule]
        public I4StepModel Root;

        private int[] parameterIndexes;

        [DoNotAutomate]
        public IModeChoiceNode Mode { get; private set; }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public void AssignBlendedParameters(List<Parameter> parameters, float weight)
        {
            if ( parameterIndexes == null )
            {
                CheckParameterNames( parameters );
            }
            for ( int i = 0; i < Links.Count; i++ )
            {
                var index = parameterIndexes[i];
                if ( index >= 0 )
                {
                    Links[i].BlendedAssignment( parameters[index].Value, weight );
                }
            }
        }

        public void AssignParameters(List<Parameter> parameters)
        {
            if ( parameterIndexes == null )
            {
                CheckParameterNames( parameters );
            }
            for ( int i = 0; i < Links.Count; i++ )
            {
                var index = parameterIndexes[i];
                if ( index >= 0 )
                {
                    Links[i].Assign( parameters[index].Value );
                }
            }
        }

        public void FinishBlending()
        {
            for ( int i = 0; i < Links.Count; i++ )
            {
                Links[i].FinishBlending();
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            IModeChoiceNode mode;
            if ( !LinkMode( ModeName, out mode ) )
            {
                error = "In '" + Name + "' we were unable to find a mode named '" + ModeName + "'!";
                return false;
            }
            Mode = mode;
            return true;
        }

        public void StartBlend()
        {
            for ( int i = 0; i < Links.Count; i++ )
            {
                Links[i].StartBlending();
            }
        }

        private void CheckParameterNames(List<Parameter> parameters)
        {
            parameterIndexes = new int[Links.Count];
            for ( int i = 0; i < parameterIndexes.Length; i++ )
            {
                parameterIndexes[i] = -1;
            }
            Parallel.For( 0, Links.Count, i =>
            {
                for ( int j = 0; j < parameters.Count; j++ )
                {
                    if ( Links[i].ParameterName == parameters[j].ParameterName )
                    {
                        parameterIndexes[i] = j;
                    }
                }
                if ( parameterIndexes[i] == -1 )
                {
                    if ( IgnoreBadParameters )
                    {
                        parameterIndexes[i] = -1;
                    }
                    else
                    {
                        throw new XTMFRuntimeException( "We were unable to find a parameter called '" + Links[i].ParameterName
                            + "' to be used for mode '" + ModeName + "' for mode choice!" );
                    }
                }
            } );
        }

        private bool LinkMode(string modeName, out IModeChoiceNode mode)
        {
            var modes = Root.Modes;
            var length = modes.Count;
            for ( int i = 0; i < length; i++ )
            {
                if ( LinkMode( modeName, modes[i], out mode ) )
                {
                    return true;
                }
            }
            mode = null;
            return false;
        }

        private bool LinkMode(string modeName, IModeChoiceNode current, out IModeChoiceNode mode)
        {
            if ( current.ModeName == modeName )
            {
                mode = current;
                return true;
            }
            var cat = current as IModeCategory;
            if ( cat != null )
            {
                var modes = cat.Children;
                var length = modes.Count;
                for ( int i = 0; i < length; i++ )
                {
                    if ( LinkMode( modeName, modes[i], out mode ) )
                    {
                        return true;
                    }
                }
            }
            mode = null;
            return false;
        }
    }
}