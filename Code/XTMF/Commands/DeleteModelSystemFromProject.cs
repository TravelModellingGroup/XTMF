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
namespace XTMF.Commands
{
    internal class DeleteModelSystemFromProject : ICommand
    {
        private IModelSystemStructure ModelSystemRoot;
        private IProject Project;
        private int RemovedAt = -1;

        public DeleteModelSystemFromProject(IProject project, IModelSystemStructure modelSystemRoot)
        {
            this.Project = project;
            this.ModelSystemRoot = modelSystemRoot;
        }

        public bool Do(ref string error)
        {
            var modelSystemStructures = this.Project.ModelSystemStructure;
            for ( int i = 0; i < modelSystemStructures.Count; i++ )
            {
                if ( modelSystemStructures[i] == this.ModelSystemRoot )
                {
                    modelSystemStructures.RemoveAt( i );
                    this.RemovedAt = i;
                    return true;
                }
            }
            // don't bother cleaning up linked parmaeters, they will find their own way
            return false;
        }

        public bool Undo(ref string error)
        {
            this.Project.ModelSystemStructure.Insert( this.RemovedAt, this.ModelSystemRoot );
            return true;
        }
    }
}