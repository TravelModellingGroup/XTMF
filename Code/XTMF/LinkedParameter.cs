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

namespace XTMF
{
    public class LinkedParameter : ILinkedParameter
    {
        /// <summary>
        /// Create a new LinkedParameter with the given name
        /// </summary>
        /// <param name="name">The name for this linked parameter</param>
        public LinkedParameter(string name)
        {
            Name = name;
            Parameters = new List<IModuleParameter>();
            Value = String.Empty;
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
            if ( Parameters.Contains( parameter ) )
            {
                error = "The parameter '" + parameter.Name + "' already exists within the linked parameter '" + Name + "'.";
                return false;
            }
            var value = ArbitraryParameterParser.ArbitraryParameterParse( parameter.Type, Value, ref error );
            if ( value == null )
            {
                return false;
            }
            parameter.Value = value;
            Parameters.Add( parameter );
            return true;
        }

        /// <summary>
        /// Map the linked parameters to finish making a clone
        /// </summary>
        /// <param name="originalLinkedParameters"></param>
        /// <param name="newModelSystemStructure"></param>
        /// <param name="oldModelSystemStructure"></param>
        public static List<ILinkedParameter> MapLinkedParameters(List<ILinkedParameter> originalLinkedParameters,
            IModelSystemStructure newModelSystemStructure, IModelSystemStructure oldModelSystemStructure)
        {
            var ret = new List<ILinkedParameter>( originalLinkedParameters.Count );
            //first make a cloned copy of the original parameters
            for ( int i = 0; i < originalLinkedParameters.Count; i++ )
            {
                ret.Add( CopyLinkedParameter( originalLinkedParameters[i] ) );
            }
            // now that we have a copy we need to walk the model system and live translate it.
            WalkAndMoveParameters( ret, newModelSystemStructure, oldModelSystemStructure );
            return ret;
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="ret"></param>
        /// <param name="newModelSystemStructure"></param>
        /// <param name="oldModelSystemStructure"></param>
        private static void WalkAndMoveParameters(List<ILinkedParameter> ret, IModelSystemStructure newModelSystemStructure, IModelSystemStructure oldModelSystemStructure)
        {
            // we only need to walk if we are not a collection since collections can not have parameters
            if ( !oldModelSystemStructure.IsCollection )
            {
                // for each linked parameter
                for ( int i = 0; i < ret.Count; i++ )
                {
                    //check the links to see if they are attached to the old system
                    var parameters = ret[i].Parameters;
                    for ( int k = 0; k < parameters.Count; k++ )
                    {
                        if ( parameters[k].BelongsTo == oldModelSystemStructure )
                        {
                            var variableName = parameters[k].VariableName;
                            var newModuleParameters = newModelSystemStructure.Parameters.Parameters;
                            for ( int j = 0; j < newModuleParameters.Count; j++ )
                            {
                                if ( variableName == newModuleParameters[j].VariableName )
                                {
                                    parameters[k] = newModuleParameters[j];
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            var oldChildren = oldModelSystemStructure.Children;
            if ( oldChildren == null ) return;
            var newChildren = newModelSystemStructure.Children;
            // Now that we have finished mapping all of the variables inside of this we need to walk the children
            for ( int i = 0; i < oldModelSystemStructure.Children.Count; i++ )
            {
                WalkAndMoveParameters( ret, newChildren[i], oldChildren[i] );
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        private static ILinkedParameter CopyLinkedParameter(ILinkedParameter original)
        {
            var ret = new LinkedParameter( original.Name );
            var oldParameters = original.Parameters;
            var parameters = ret.Parameters;
            ret.Value = original.Value;
            for ( int i = 0; i < oldParameters.Count; i++ )
            {
                parameters.Add( oldParameters[i] );
            }
            return ret;
        }

        /// <summary>
        /// Remove a parameter from this linked parameter
        /// </summary>
        /// <param name="parameter">The parameter to remove</param>
        /// <returns>If we removed it from this linked parameter.
        /// If it is not contained, it will return false!</returns>
        public bool Remove(IModuleParameter parameter, ref string error)
        {
            if ( !Parameters.Remove( parameter ) )
            {
                error = "The parameter '" + parameter.Name + "' was not contained within '" + Name + "'!";
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
            if ( !AssignValue( value, ref error ) )
            {
                return false;
            }
            Value = value;
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
            for ( int i = 0; i < Parameters.Count; i++ )
            {
                Parameters[i].Value =
                    ArbitraryParameterParser.ArbitraryParameterParse( Parameters[i].Type, value, ref error );
                // if it failed, roll back
                if ( Parameters[i].Value == null )
                {
                    error = "Failed to assign to parameter " + Parameters[i].Name + "\r\n" + error;
                    for ( ; i >= 0; i-- )
                    {
                        Parameters[i].Value =
                            ArbitraryParameterParser.ArbitraryParameterParse( Parameters[i].Type, Value, ref error );
                    }
                    return false;
                }
            }
            // if we got here then all of the parameters were assigned correctly
            return true;
        }
    }
}