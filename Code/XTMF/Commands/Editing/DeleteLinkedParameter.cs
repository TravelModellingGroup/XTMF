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
    public class DeleteLinkedParameter : ICommand
    {
        private ILinkedParameter LinkedParameter;
        private IModelSystem ModelSystem;

        public DeleteLinkedParameter(IModelSystem modelSystem, ILinkedParameter linkedParameter)
        {
            this.ModelSystem = modelSystem;
            this.LinkedParameter = linkedParameter;
        }

        public bool Do(ref string error)
        {
            if ( !CheckLinkedParemterList( ref error ) )
            {
                return false;
            }
            this.ModelSystem.LinkedParameters.Remove( this.LinkedParameter );
            return true;
        }

        public bool Undo(ref string error)
        {
            if ( !CheckLinkedParemterList( ref error ) )
            {
                return false;
            }
            this.ModelSystem.LinkedParameters.Add( this.LinkedParameter );
            return true;
        }

        /// <summary>
        /// Do a quick test to make sure that there is actually a linked parameter list
        /// </summary>
        /// <param name="error">The error message field</param>
        /// <returns>True if there is a linked parameter list</returns>
        private bool CheckLinkedParemterList(ref string error)
        {
            if ( this.ModelSystem.LinkedParameters == null )
            {
                error = "The model system '" + this.ModelSystem.Name + "' does not support linked parameters!";
                return false;
            }
            return true;
        }
    }
}