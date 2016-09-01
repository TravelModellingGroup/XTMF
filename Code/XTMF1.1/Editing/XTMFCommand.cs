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
using System.Linq;
using System.Text;

namespace XTMF.Editing
{
    /// <summary>
    /// The base class for internal XTMF commands
    /// </summary>
    public abstract class XTMFCommand
    {
        /// <summary>
        /// The name of the executing command
        /// </summary>
        public readonly string Name;

        public XTMFCommand(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Check to see if a command can be undone
        /// </summary>
        /// <returns></returns>
        public abstract bool CanUndo();

        /// <summary>
        /// Run the command for the first time
        /// </summary>
        public abstract bool Do(ref string error);

        /// <summary>
        /// Undo the command
        /// </summary>
        public abstract bool Undo(ref string error);

        /// <summary>
        /// Reapply the command
        /// </summary>
        public abstract bool Redo(ref string error);

        public delegate bool XTMFCommandMethod(ref string error);

        /// <summary>
        /// This class provides an easy way to generate commands through our factory
        /// </summary>
        private class DelegateCommand : XTMFCommand
        {
            private XTMFCommandMethod OnDo;
            private XTMFCommandMethod OnUndo = null;
            private XTMFCommandMethod OnRedo = null;

            public DelegateCommand(string name, XTMFCommandMethod onDo, XTMFCommandMethod onUndo = null, XTMFCommandMethod onRedo = null) : base(name)
            {
                OnDo = onDo;
                OnUndo = onUndo;
                OnRedo = onRedo;
            }

            public override bool CanUndo()
            {
                return this.OnUndo != null;
            }

            public override bool Do(ref string error)
            {
                return OnDo( ref error );
            }

            public override bool Redo(ref string error)
            {
                return OnRedo( ref error );
            }

            public override bool Undo(ref string error)
            {
                return OnUndo( ref error );
            }
        }

        /// <summary>
        /// Generate a new command given delegates.  If you use OnUndo you must also provide a OnRedo.
        /// </summary>
        /// <param name="OnDo">The action to perform</param>
        /// <param name="OnUndo">The inverse of the action to perform</param>
        /// <param name="OnRedo">The inverse of the inverse of the action to perform</param>
        /// <returns>A command with this behaviour.</returns>
        public static XTMFCommand CreateCommand(string name, XTMFCommandMethod OnDo, XTMFCommandMethod OnUndo = null, XTMFCommandMethod OnRedo = null)
        {
            if ( OnDo == null )
            {
                throw new ArgumentNullException( "OnDo" );
            }
            if ( ( OnUndo == null ) != ( OnRedo == null ) )
            {
                throw new ArgumentException( "Both OnUndo and OnRedo must be null or both have delegates." );
            }
            return new DelegateCommand( name, OnDo, OnUndo, OnRedo );
        }
    }
}
