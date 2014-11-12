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
    internal class RenameProject : ICommand
    {
        private IConfiguration Config;
        private string OtherName;
        private IProject OurProject;

        public RenameProject(IConfiguration config, IProject modelSystem, string newName)
        {
            this.Config = config;
            this.OtherName = newName;
            this.OurProject = modelSystem;
        }

        public bool Do(ref string error)
        {
            var repository = Config.ProjectRepository;
            // we use this function to make sure that we don't end up with 2 projects with the same name
            if ( !repository.RenameProject( this.OurProject, this.OtherName ) )
            {
                error = "The project name '" + this.OtherName + "' is an invalid project name!";
                return false;
            }
            this.OurProject.HasChanged = true;
            return true;
        }

        public bool Undo(ref string error)
        {
            return Do( ref error );
        }
    }
}