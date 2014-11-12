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
using System.ComponentModel;

namespace XTMF.Controller
{
    /// <summary>
    /// This class provides an organized access to project operations
    /// </summary>
    public class ProjectController
    {
        private Configuration Configuration;

        /// <summary>
        /// Our view of how the projects are stored
        /// </summary>
        private DataAccessView<IProject> DataView;

        private XTMFRuntime XTMFRuntime;

        /// <summary>
        /// Initialize the project controller
        /// </summary>
        /// <param name="xtmfRuntime">A connection back to the XTMF we are apart of</param>
        internal ProjectController(XTMFRuntime xtmfRuntime)
        {
            this.XTMFRuntime = xtmfRuntime;
            this.Configuration = xtmfRuntime.Configuration;
            this.DataView = new DataAccessView<IProject>( this.Configuration.ProjectRepository.Projects );
            // initialize the hooks into added / removed
            ( (ProjectRepository)this.Configuration.ProjectRepository ).ProjectAdded += new Action<IProject>( ProjectController_ProjectAdded );
            ( (ProjectRepository)this.Configuration.ProjectRepository ).ProjectRemoved += new Action<IProject, int>( ProjectController_ProjectRemoved );
        }

        /// <summary>
        /// A view of all of the loaded projects
        /// </summary>
        public IBindingList Projects { get { return DataView; } }

        /// <summary>
        /// Add a new model system to a project
        /// </summary>
        /// <param name="project">The project to add the model system to</param>
        /// <param name="modelSystem">The modelsystem to add to the project</param>
        /// <param name="error">Te returned error message in case the operation fails</param>
        /// <returns>True if the operation succeeded, false otherwise with error message</returns>
        public bool AddModelSystemToProject(IProject project, IModelSystem modelSystem, ref string error)
        {
            var command = new XTMF.Commands.AddModelSystemToProject( project, modelSystem );
            if ( this.XTMFRuntime.ProcessCommand( command, ref error ) )
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Create a new project
        /// </summary>
        /// <param name="name">The name of the project</param>
        /// <param name="description">A description of the project</param>
        /// <param name="error">An error message string in case of an error</param>
        /// <returns>The project that was created, null if there was an error</returns>
        public IProject CreateProject(string name, string description, ref string error)
        {
            var command = new XTMF.Commands.CreateNewProject( this.Configuration,
                  name, description );
            return this.XTMFRuntime.ProcessCommand( command, ref error ) ? command.GeneratedProject : null;
        }

        /// <summary>
        /// Delete the given project
        /// </summary>
        /// <param name="project">The project to delete</param>
        /// <param name="error">A message containing the error if there was one</param>
        /// <returns>True if successful, false otherwise with an error message</returns>
        public bool DeleteProject(IProject project, ref string error)
        {
            var command = new XTMF.Commands.DeleteProject( project, this.Configuration );
            return ( this.XTMFRuntime.ProcessCommand( command, ref error ) );
        }

        /// <summary>
        /// Import a project from a given location
        /// </summary>
        /// <param name="oldLocation">The location to load the project from</param>
        /// <param name="newName">The name to save this project as</param>
        /// <param name="error">A message containing any error encountered</param>
        /// <param name="replace">Should we remove any project with the same name?</param>
        /// <returns>If the import was successful</returns>
        public bool ImportProject(string oldLocation, string newName, ref string error, bool replace = false)
        {
            var command = new XTMF.Commands.ImportProject( this.Configuration, oldLocation, newName, replace );
            if ( this.XTMFRuntime.ProcessCommand( command, ref error ) )
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Rename the given project to the new name
        /// </summary>
        /// <param name="project">The project to change the name to</param>
        /// <param name="newName">The name to rename it to</param>
        /// <param name="error">A message containing any error encountered</param>
        /// <returns>If the renaming was successful</returns>
        public bool Rename(IProject project, string newName, ref string error)
        {
            var command = new XTMF.Commands.RenameProject( this.Configuration, project, newName );
            if ( this.XTMFRuntime.ProcessCommand( command, ref error ) )
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Validate that the given string is a valid project name
        /// </summary>
        /// <param name="projectName">The name to test for</param>
        /// <returns>If the given project name is valid</returns>
        public bool ValidateProjectName(string projectName)
        {
            return this.Configuration.ProjectRepository.ValidateProjectName( projectName );
        }

        private void ProjectController_ProjectAdded(IProject obj)
        {
            // redirect the add to the BindView
            this.DataView.ItemWasAdded( obj );
        }

        private void ProjectController_ProjectRemoved(IProject obj, int index)
        {
            // redirect the remove to the BindView
            this.DataView.ItemWasRemoved( obj, index );
        }

        internal void SetActiveProject(IProject project)
        {
            ( (ProjectRepository)( this.Configuration.ProjectRepository ) ).SetActiveProject( project );
        }
    }
}