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

namespace XTMF.Commands
{
    internal class DeleteProject : ICommand
    {
        private IConfiguration Config;
        private IProject Project;

        /// <summary>
        /// Tell XTMF to delete the given project
        /// </summary>
        /// <param name="project">The project to delete</param>
        /// <param name="config">The current XTMF configuration</param>
        public DeleteProject(IProject project, IConfiguration config)
        {
            if ( project == null )
            {
                throw new ArgumentNullException( "project" );
            }
            this.Project = project;
            this.Config = config;
        }

        public bool Do(ref string error)
        {
            var repository = this.Config.ProjectRepository;
            if ( !repository.Remove( this.Project ) )
            {
                error = "Deleting the project '" + Project.Name + "' failed!";
                return false;
            }
            return true;
        }

        public bool Undo(ref string error)
        {
            return this.Project.Save( ref error );
        }
    }
}