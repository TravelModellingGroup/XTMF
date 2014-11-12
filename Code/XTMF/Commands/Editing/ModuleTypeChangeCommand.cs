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
    public class ModuleTypeChangeCommand : LinkedCommandBase
    {
        private Type NewType;
        private List<IModelSystemStructure> PreviousChildren;
        private Type PreviousType;

        public ModuleTypeChangeCommand(List<ILinkedParameter> linkedParameters, IModelSystemStructure module, Type t)
            : base( linkedParameters, module )
        {
            this.NewType = t;
        }

        override public bool Do(ref string error)
        {
            var children = this.Child.Children;
            if ( children != null )
            {
                this.PreviousChildren = new List<IModelSystemStructure>( children );
            }
            this.PreviousType = this.Child.Type;
            this.Child.Type = this.NewType;
            return this.UnlinkLinkedParameters( ref error );
        }

        override public bool Undo(ref string error)
        {
            this.Child.Type = this.PreviousType;
            var mod = this.Child as ModelSystemStructure;
            if ( mod != null )
            {
                mod.Children = this.PreviousChildren;
            }
            return this.RelinkParameters( ref error );
        }
    }
}