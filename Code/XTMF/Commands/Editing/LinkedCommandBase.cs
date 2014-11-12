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
    public abstract class LinkedCommandBase : ICommand
    {
        protected IModelSystemStructure Child;
        protected List<ILinkedParameter> LinkedParameters;
        private Dictionary<ILinkedParameter, List<IModuleParameter>> RemovedParameters;

        public LinkedCommandBase(List<ILinkedParameter> linkedParameters, IModelSystemStructure child)
        {
            this.LinkedParameters = linkedParameters;
            this.Child = child;
        }

        public abstract bool Do(ref string error);

        public abstract bool Undo(ref string error);

        protected bool RelinkParameters(ref string error)
        {
            if ( this.RemovedParameters == null )
            {
                return true;
            }
            foreach ( var lps in this.RemovedParameters )
            {
                var lp = lps.Key;
                var pl = lps.Value;
                foreach ( var p in pl )
                {
                    if ( !lp.Add( p, ref error ) )
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        protected bool UnlinkLinkedParameters(ref string error)
        {
            if ( this.LinkedParameters == null )
            {
                return true;
            }
            return this.UnlinkLinkedParameters( this.Child, ref error );
        }

        private bool UnlinkLinkedParameters(IModelSystemStructure node, ref string error)
        {
            foreach ( var lp in this.LinkedParameters )
            {
                if ( lp.Parameters == null )
                {
                    continue;
                }
                bool newList = true;
                List<IModuleParameter> localList = null;
                List<IModuleParameter> removeList = null;
                if ( this.RemovedParameters != null )
                {
                    newList = !this.RemovedParameters.TryGetValue( lp, out removeList );
                }
                foreach ( var p in lp.Parameters )
                {
                    if ( p.BelongsTo == node )
                    {
                        if ( localList == null )
                        {
                            localList = new List<IModuleParameter>();
                        }
                        localList.Add( p );
                    }
                }
                if ( localList != null )
                {
                    foreach ( var p in localList )
                    {
                        if ( !lp.Remove( p, ref error ) )
                        {
                            return false;
                        }
                        // add this to the global remove list
                        if ( removeList == null )
                        {
                            removeList = new List<IModuleParameter>();
                        }
                        removeList.Add( p );
                    }
                    if ( newList )
                    {
                        if ( RemovedParameters == null )
                        {
                            RemovedParameters = new Dictionary<ILinkedParameter, List<IModuleParameter>>();
                        }
                        RemovedParameters[lp] = removeList;
                    }
                }
            }
            var list = node.Children;
            if ( list != null )
            {
                foreach ( var child in list )
                {
                    if ( !UnlinkLinkedParameters( child, ref error ) )
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}