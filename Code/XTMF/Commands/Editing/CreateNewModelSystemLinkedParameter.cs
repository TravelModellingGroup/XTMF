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
namespace XTMF.Commands.Editing
{
    public class CreateNewModelSystemLinkedParameter : ICommand
    {
        private string InitialValue;
        private IModelSystem ModelSystem;

        private string Name;

        public CreateNewModelSystemLinkedParameter(IModelSystem modelSystem, string name, string intialValue)
        {
            this.ModelSystem = modelSystem;
            this.Name = name;
            this.InitialValue = intialValue;
        }

        public ILinkedParameter LinkedParameter { get; private set; }

        public bool Do(ref string error)
        {
            // if we don't already have a linked parameter, build one
            if ( this.LinkedParameter == null )
            {
                if ( !CheckLinkedParemterList( ref error ) )
                {
                    return false;
                }
                var lp = new LinkedParameter( this.Name );
                if ( !lp.SetValue( this.InitialValue, ref error ) )
                {
                    return false;
                }
                this.LinkedParameter = lp;
            }
            // store that parameter in the mode system's parameters
            this.ModelSystem.LinkedParameters.Add( this.LinkedParameter );
            return true;
        }

        public bool Undo(ref string error)
        {
            if ( !CheckLinkedParemterList( ref error ) )
            {
                return false;
            }
            this.ModelSystem.LinkedParameters.Remove( this.LinkedParameter );
            return true;
        }

        /// <summary>
        /// Do a quick test to make sure that there is actually a linked parameter list
        /// </summary>
        /// <param name="error">The error message field</param>
        /// <returns>True if there is a linked parameter list</returns>
        private bool CheckLinkedParemterList(ref string error)
        {
            if ( this.ModelSystem == null )
            {
                error = "The model System was not initialized!";
                return false;
            }
            if ( this.ModelSystem.LinkedParameters == null )
            {
                error = "The model system '" + this.ModelSystem.Name + "' does not support linked parameters!";
                return false;
            }
            return true;
        }
    }
}