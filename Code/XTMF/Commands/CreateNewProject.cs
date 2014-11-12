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
using System.Linq;

namespace XTMF.Commands
{
    internal class CreateNewProject : ICommand
    {
        public Project GeneratedProject;
        private IConfiguration Config;
        private string Description;
        private string Name;

        public CreateNewProject(IConfiguration config, string name, string description)
        {
            this.Config = config;
            this.Name = name;
            this.Description = description;
        }

        public bool Do(ref string error)
        {
            var repository = Config.ProjectRepository;
            if ( !Project.ValidateProjectName( this.Name ) )
            {
                return false;
            }
            if ( repository.Projects.Any( (p) => p.Name.Equals( this.Name, StringComparison.InvariantCultureIgnoreCase ) ) )
            {
                error = "There already exists a model system with the name '" + this.Name + "'!";
                return false;
            }
            this.GeneratedProject = new Project( this.Name, Config );
            this.GeneratedProject.Description = Description;
            if ( !GeneratedProject.Save( ref error ) )
            {
                this.GeneratedProject = null;
                return false;
            }
            repository.AddProject( GeneratedProject );
            return true;
        }

        public bool Undo(ref string error)
        {
            // we only need to remove it if it exists
            if ( this.GeneratedProject == null )
            {
                return true;
            }
            var projectRepository = this.Config.ProjectRepository;
            GeneratedProject.HasChanged = true;
            projectRepository.Remove( GeneratedProject );
            return true;
        }
    }
}