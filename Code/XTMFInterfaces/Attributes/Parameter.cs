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

namespace XTMF
{
    /// <summary>
    /// Provides information for XTMF to use for
    /// working with configurations
    /// </summary>
    public class ParameterAttribute : Attribute
    {
        /// <summary>
        /// Provides the information for
        /// configuration files
        /// </summary>
        /// <param name="defaultValue">The default value of this field</param>
        /// <param name="description">A description of what this field is supposed to be used for</param>
        /// <param name="name">The name of the parameter</param>
        public ParameterAttribute(string name, object defaultValue, string description)
        {
            this.Name = name;
            this.Description = description;
            this.DefaultValue = defaultValue;
        }

        /// <summary>
        /// Create a default value of the given type for a parameter
        /// </summary>
        /// <param name="defaultValue">The default value of this field</param>
        /// <param name="description">A description of what this field is supposed to be used for</param>
        /// <param name="type">The type to process</param>
        /// <param name="name">The name of the parameter</param>
        public ParameterAttribute(string name, string defaultValue, Type type, string description)
        {
            this.Name = name;
            this.Description = description;
            string error = null;
            this.DefaultValue = ArbitraryParameterParser.ArbitraryParameterParse( type, defaultValue, ref error );
        }

        public bool AttachedToField { get; set; }

        /// <summary>
        /// The default value for this parameter
        /// </summary>
        public object DefaultValue { get; set; }

        /// <summary>
        /// Describes what this parameter is used for
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The name of this parameter
        /// </summary>
        public string Name { get; set; }

        public string VariableName { get; set; }

        /// <summary>
        /// Gives a quick description of what this parameter is for the debugger
        /// </summary>
        /// <returns>A quick description of what type of parameter it is, and what it describes</returns>
        public override string ToString()
        {
            return String.Format( "{0}-> Default:{1}, {2}", this.GetType().Name, this.DefaultValue, this.Description );
        }
    }
}