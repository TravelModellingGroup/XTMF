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
    public class ModuleSwapCommand : LinkedCommandBase
    {
        private int Index;
        private IModelSystemStructure NewValue;
        private IModelSystemStructure Parent;

        public ModuleSwapCommand(List<ILinkedParameter> linkedParameters, IModelSystemStructure parent, int index, IModelSystemStructure newValue)
            : base( linkedParameters, parent.Children[index] )
        {
            this.Parent = parent;
            this.NewValue = newValue;
            this.Index = index;
        }

        override public bool Do(ref string error)
        {
            Swap();
            return this.UnlinkLinkedParameters( ref error );
        }

        override public bool Undo(ref string error)
        {
            Swap();
            return this.RelinkParameters( ref error );
        }

        private void Swap()
        {
            var temp = this.Parent.Children[this.Index];
            this.NewValue.ParentFieldType = temp.ParentFieldType;
            this.NewValue.ParentFieldName = temp.ParentFieldName;
            this.Parent.Children[this.Index] = this.NewValue;
            this.NewValue = temp;
        }
    }
}