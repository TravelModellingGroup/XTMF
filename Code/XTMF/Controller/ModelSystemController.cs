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
using System.ComponentModel;

namespace XTMF.Controller
{
    public class ModelSystemController
    {
        private Configuration Configuration;
        private DataAccessView<IModelSystem> DataView;
        private XTMFRuntime XTMFRuntime;

        internal ModelSystemController(XTMFRuntime xtmfRuntime)
        {
            this.XTMFRuntime = xtmfRuntime;
            this.Configuration = this.XTMFRuntime.Configuration;
            this.DataView = new DataAccessView<IModelSystem>( this.Configuration.ModelSystemRepository.ModelSystems );
            // initialize the add / remove modelsystems
            ( (ModelSystemRepository)this.Configuration.ModelSystemRepository ).ModelSystemAdded += new Action<IModelSystem>( ModelSystemController_ModelSystemAdded );
            ( (ModelSystemRepository)this.Configuration.ModelSystemRepository ).ModelSystemRemoved += new Action<IModelSystem, int>( ModelSystemController_ModelSystemRemoved );
        }

        /// <summary>
        /// A view of all of the loaded projects
        /// </summary>
        public IBindingList ModelSystems { get { return DataView; } }

        /// <summary>
        /// Create a new model system
        /// </summary>
        /// <param name="name">The name of the project</param>
        /// <param name="description">A description of the project</param>
        /// <param name="error">An error message string in case of an error</param>
        /// <returns>The model system that was created, null if there was an error</returns>
        public IModelSystem CreateModelSystem(string name, string description, IModelSystemStructure strucuture, List<ILinkedParameter> linkedParameters, ref string error)
        {
            var command = new XTMF.Commands.CreateNewModelSystem( this.Configuration,
                  name, description, strucuture, linkedParameters );
            return this.XTMFRuntime.ProcessCommand( command, ref error ) ? command.GeneratedModelSystem : null;
        }

        /// <summary>
        /// Delete the given modelsytem
        /// </summary>
        /// <param name="modelSystem">The project to delete</param>
        /// <param name="error">A message containing the error if there was one</param>
        /// <returns>True if successful, false otherwise with an error message</returns>
        public bool DeleteModelSystem(IModelSystem modelSystem, ref string error)
        {
            var command = new XTMF.Commands.DeleteModelSystem( modelSystem, this.Configuration );
            return ( this.XTMFRuntime.ProcessCommand( command, ref error ) );
        }

        /// <summary>
        /// Import a model system from a given location
        /// </summary>
        /// <param name="oldLocation">The location to load the project from</param>
        /// <param name="newName">The name to save this project as</param>
        /// <param name="error">A message containing any error encountered</param>
        /// <param name="replace">Should we remove any project with the same name?</param>
        /// <returns>If the import was successful</returns>
        public bool ImportModelSystem(string oldLocation, string newName, ref string error, bool replace = false)
        {
            var command = new XTMF.Commands.ImportModelSystem( this.Configuration, oldLocation, newName, replace );
            if ( this.XTMFRuntime.ProcessCommand( command, ref error ) )
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Rename the model system
        /// </summary>
        /// <param name="ms">The model system to rename</param>
        /// <param name="newName">The name to change the model system to</param>
        /// <param name="error">An error message in case the operation fails</param>
        /// <returns>True if successful, false with an error message in case it fails</returns>
        public bool Rename(IModelSystem ms, string newName, ref string error)
        {
            var command = new XTMF.Commands.RenameModelSystem( this.Configuration, ms, newName );
            if ( this.XTMFRuntime.ProcessCommand( command, ref error ) )
            {
                return true;
            }
            return false;
        }

        private void ModelSystemController_ModelSystemAdded(IModelSystem modelSystem)
        {
            this.DataView.ItemWasAdded( modelSystem );
        }

        private void ModelSystemController_ModelSystemRemoved(IModelSystem modelSystem, int index)
        {
            this.DataView.ItemWasRemoved( modelSystem, index );
        }
    }
}