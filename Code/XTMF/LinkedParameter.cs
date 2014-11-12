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

namespace XTMF
{
    internal class LinkedParameter : ILinkedParameter
    {
        /// <summary>
        /// Create a new LinkedParameter with the given name
        /// </summary>
        /// <param name="name">The name for this linked parameter</param>
        public LinkedParameter(string name)
        {
            this.Name = name;
            this.Parameters = new List<IModuleParameter>();
        }

        /// <summary>
        /// The name of this linked parameter
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// A list of the parameters that are contained in this Linked Parameter
        /// </summary>
        public List<IModuleParameter> Parameters { get; set; }

        /// <summary>
        /// The string representation of the value for this linked parameter
        /// </summary>
        public string Value { get; private set; }

        /// <summary>
        /// Add a new parameter to this linkedParameter set
        /// </summary>
        /// <param name="parameter">The parameter to add</param>
        /// <returns>If we were able to add the parameter</returns>
        public bool Add(IModuleParameter parameter, ref string error)
        {
            // you can't have the same parameter multiple times!
            if ( parameter == null )
            {
                error = "The parameter does not exist!";
                return false;
            }
            if ( this.Parameters.Contains( parameter ) )
            {
                error = "The parameter '" + parameter.Name + "' already exists within the linked parameter '" + this.Name + "'.";
                return false;
            }
            var value = ArbitraryParameterParser.ArbitraryParameterParse( parameter.Type, this.Value, ref error );
            if ( value == null )
            {
                return false;
            }
            parameter.Value = value;
            this.Parameters.Add( parameter );
            return true;
        }

        /// <summary>
        /// Remove a parameter from this linked parameter
        /// </summary>
        /// <param name="parameter">The parameter to remove</param>
        /// <returns>If we removed it from this linked parameter.
        /// If it is not contained, it will return false!</returns>
        public bool Remove(IModuleParameter parameter, ref string error)
        {
            if ( !this.Parameters.Remove( parameter ) )
            {
                error = "The parameter '" + parameter.Name + "' was not contained within '" + this.Name + "'!";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Set the value of this linked parameter
        /// </summary>
        /// <param name="value">The new value to set it to</param>
        /// <returns>If we were able to assign this value to all of the parmeters</returns>
        public bool SetValue(string value, ref string error)
        {
            if ( !this.AssignValue( value, ref error ) )
            {
                return false;
            }
            this.Value = value;
            return true;
        }

        /// <summary>
        /// Assign the given value to all of the parameters,
        /// if it fails roll back to the old value
        /// </summary>
        /// <param name="value">The new value to assign to</param>
        /// <returns>True if all of the parameters were updated successfully</returns>
        private bool AssignValue(string value, ref string error)
        {
            // we can not cache the values because they are pointers, not value types
            // this lets us deal with cases where parameters are later removed from the
            // linked parameter set
            // assign the new value to all of the parameters
            for ( int i = 0; i < this.Parameters.Count; i++ )
            {
                this.Parameters[i].Value =
                    ArbitraryParameterParser.ArbitraryParameterParse( this.Parameters[i].Type, value, ref error );
                // if it failed, roll back
                if ( this.Parameters[i].Value == null )
                {
                    error = "Failed to assign to parameter " + this.Parameters[i].Name + "\r\n" + error;
                    for ( ; i >= 0; i-- )
                    {
                        this.Parameters[i].Value =
                            ArbitraryParameterParser.ArbitraryParameterParse( this.Parameters[i].Type, this.Value, ref error );
                    }
                    return false;
                }
            }
            // if we got here then all of the parameters were assigned correctly
            return true;
        }
    }
}