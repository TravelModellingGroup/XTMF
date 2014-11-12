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
    /// <summary>
    /// Use this command in order to delete a project
    /// </summary>
    public class DeleteProjectWithName : ICommand
    {
        private IConfiguration Config;
        private IProject Project;
        private string ProjectName;

        /// <summary>
        /// Tell XTMF to delete the given project
        /// </summary>
        /// <param name="project">The project to delete</param>
        /// <param name="config">The current XTMF configuration</param>
        public DeleteProjectWithName(string projectName, IConfiguration config)
        {
            this.Config = config;
            this.ProjectName = projectName;
        }

        public bool Do(ref string error)
        {
            var repository = this.Config.ProjectRepository;
            for ( int i = 0; i < repository.Projects.Count; i++ )
            {
                if ( repository.Projects[i].Name.Equals( this.ProjectName, StringComparison.InvariantCultureIgnoreCase ) )
                {
                    this.Project = repository.Projects[i];
                    break;
                }
            }
            if ( this.Project == null )
            {
                error = "We were unable to find a project with the name '" + this.ProjectName + "'";
                return false;
            }
            if ( !repository.Remove( this.Project ) )
            {
                error = "Deleting the project '" + Project.Name + "' failed!";
                return false;
            }
            return true;
        }

        public bool Undo(ref string error)
        {
            if ( this.Project == null )
            {
                error = "The project does not exist!";
                return false;
            }
            return this.Project.Save( ref error );
        }
    }
}