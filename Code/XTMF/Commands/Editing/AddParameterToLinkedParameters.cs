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
namespace XTMF.Commands.Editing
{
    public class AddParameterToLinkedParameters : ICommand
    {
        private ILinkedParameter LinkedParameter;
        private List<ILinkedParameter> LinkedParameters;
        private IModuleParameter Parameter;
        private ILinkedParameter RemovedFrom;
        public AddParameterToLinkedParameters(List<ILinkedParameter> linkedParameters, ILinkedParameter linkedParameter, IModuleParameter parameter)
        {
            if ( linkedParameters == null ) throw new ArgumentNullException( "linkedParameters" );
            this.LinkedParameters = linkedParameters;
            this.LinkedParameter = linkedParameter;
            this.Parameter = parameter;
        }

        public bool Do(ref string error)
        {
            string error2 = null;
            for ( int i = 0; i < this.LinkedParameters.Count; i++ )
            {
                if ( this.LinkedParameters[i].Remove( this.Parameter, ref error2 ) )
                {
                    this.RemovedFrom = this.LinkedParameters[i];
                    break;
                }
            }

            if ( !this.LinkedParameter.Add( this.Parameter, ref error ) )
            {
                if ( this.RemovedFrom != null )
                {
                    this.RemovedFrom.Add( this.Parameter, ref error2 );
                }
                return false;
            }
            return true;
        }

        public bool Undo(ref string error)
        {
            if ( !this.LinkedParameter.Remove( this.Parameter, ref error ) )
            {
                return false;
            }
            if ( this.RemovedFrom != null )
            {
                this.RemovedFrom.Add( this.Parameter, ref error );
            }
            return true;
        }
    }
}