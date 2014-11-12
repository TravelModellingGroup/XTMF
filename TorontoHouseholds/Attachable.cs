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
using XTMF;

namespace Tasha.Common
{
    /// <summary>
    /// The base class of the common objects.
    /// This allows for the attaching of properties to the objects
    /// </summary>
    public abstract class Attachable : IAttachable
    {
        /// <summary>
        /// Where we store all of the attached objects
        /// </summary>
        protected SortedList<string, object> variables;

        /// <summary>
        /// Creates a new attachable object
        /// </summary>
        protected Attachable()
        {
            // we don't expect it to contain tooo many
            this.variables = new SortedList<string, object>( 5 );
        }

        /// <summary>
        /// Attempts to access a variable from the object
        /// </summary>
        /// <param name="name">The name of the variable to look for</param>
        /// <returns>The variable you wanted</returns>
        public object this[string name]
        {
            get
            {
                object o;
                if ( !variables.TryGetValue( name, out o ) )
                {
                    return null;
                }
                return o;
            }

            set
            {
                this.Attach( name, value );
            }
        }

        /// <summary>
        /// Attaches a new variable to this object
        /// </summary>
        /// <param name="name">The name of the variable (MUST BE UNIQUE!)</param>
        /// <param name="value">The object to store to this name</param>
        public void Attach(string name, object value)
        {
            if ( value == null )
            {
                throw new XTMFRuntimeException( "Attempted to attach a NULL to an object!" );
            }
            variables[name] = value;
            return;
        }

        /// <summary>
        /// Synonamous with Attachable[name].
        /// </summary>
        /// <param name="name">The name of the variable you want</param>
        /// <returns>The variable you wanted</returns>
        public object GetVariable(string name)
        {
            return this[name];
        }

        #region IAttachable Members

        /// <summary>
        /// Gets the keys associated with this object
        /// </summary>
        public IEnumerable<string> Keys
        {
            get { return this.variables.Keys; }
        }

        public void Release()
        {
            this.variables.Clear();
        }

        #endregion IAttachable Members
    }
}