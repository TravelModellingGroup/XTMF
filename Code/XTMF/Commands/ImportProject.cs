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
    public class ImportProject : ICommand
    {
        private IConfiguration Configuration;
        private string NewName;
        private string OldLocation;
        private Project Project;
        private bool Replace;

        public ImportProject(IConfiguration configuration, string oldLocation, string newName, bool replace = false)
        {
            this.Configuration = configuration;
            this.OldLocation = oldLocation;
            this.NewName = newName;
            this.Replace = replace;
        }

        public bool Do(ref string error)
        {
            Project = new Project( OldLocation, this.Configuration );
            Project.Name = NewName;
            if ( Replace )
            {
                var projects = this.Configuration.ProjectRepository.Projects;
                for ( int i = 0; i < projects.Count; i++ )
                {
                    if ( projects[i].Name.Equals( Project.Name, StringComparison.InvariantCultureIgnoreCase ) )
                    {
                        this.Configuration.ProjectRepository.Remove( projects[i] );
                        break;
                    }
                }
            }
            else
            {
                bool foundSameName = false;
                var baseName = Project.Name;
                int iterations = 2;
                do
                {
                    foundSameName = false;
                    var projects = this.Configuration.ProjectRepository.Projects;
                    for ( int i = 0; i < projects.Count; i++ )
                    {
                        if ( projects[i].Name.Equals( Project.Name, StringComparison.InvariantCultureIgnoreCase ) )
                        {
                            foundSameName = true;
                            break;
                        }
                    }
                    if ( foundSameName )
                    {
                        Project.Name = String.Format( "{0}({1})", baseName, iterations );
                        iterations++;
                    }
                } while ( foundSameName );
            }
            return this.Configuration.ProjectRepository.AddProject( Project );
        }

        public bool Undo(ref string error)
        {
            if ( !this.Configuration.ProjectRepository.Remove( this.Project ) )
            {
                error = "We were unable to remove the project '" + this.Project.Name + "'!";
                return false;
            }
            return true;
        }
    }
}