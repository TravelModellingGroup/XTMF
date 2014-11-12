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
using System.Collections.Generic;

namespace XTMF.Commands.Editing
{
    public class ParameterChangeCommand : ICommand
    {
        private string After;
        private IModuleParameter AttachedParameter;
        private string Before;
        private ILinkedParameter ContainedIn;
        private List<ILinkedParameter> LinkedParameters;

        public ParameterChangeCommand(IModuleParameter parameter, string newValue, List<ILinkedParameter> linkedParameters)
        {
            this.Before = parameter.Value.ToString();
            this.AttachedParameter = parameter;
            this.After = newValue;
            this.LinkedParameters = linkedParameters;
            this.ContainedIn = null;
        }

        public bool Do(ref string error)
        {
            CheckLinkedParameters();
            return SetValue( this.After, ref error );
        }

        public bool Undo(ref string error)
        {
            return this.SetValue( this.Before, ref error );
        }

        private void CheckLinkedParameters()
        {
            // make sure that there is actually a linked parameter list
            if ( this.LinkedParameters == null )
            {
                return;
            }
            // search for this parameter in the list
            // for each linked parameter
            foreach ( var lp in this.LinkedParameters )
            {
                // for each parameter
                foreach ( var p in lp.Parameters )
                {
                    // if this is exactly the same parameter
                    if ( p == this.AttachedParameter )
                    {
                        this.ContainedIn = lp;
                        return;
                    }
                }
            }
        }

        private bool SetValue(string value, ref string error)
        {
            if ( this.ContainedIn == null )
            {
                var temp = ArbitraryParameterParser.ArbitraryParameterParse( AttachedParameter.Type, value, ref error );
                if ( temp == null )
                {
                    return false;
                }
                AttachedParameter.Value = temp;
            }
            else
            {
                if ( !this.ContainedIn.SetValue( this.After, ref error ) )
                {
                    return false;
                }
            }
            return true;
        }
    }
}