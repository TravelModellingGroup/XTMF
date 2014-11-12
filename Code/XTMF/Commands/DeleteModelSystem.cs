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
    internal class DeleteModelSystem : ICommand
    {
        private IConfiguration Config;
        private IModelSystem ModelSystem;

        /// <summary>
        /// Tell XTMF to delete the given project
        /// </summary>
        /// <param name="modelSystem">The project to delete</param>
        /// <param name="config">The current XTMF configuration</param>
        public DeleteModelSystem(IModelSystem modelSystem, IConfiguration config)
        {
            if ( modelSystem == null )
            {
                throw new ArgumentNullException( "modelSystem" );
            }
            this.ModelSystem = modelSystem;
            this.Config = config;
        }

        public bool Do(ref string error)
        {
            var repository = this.Config.ModelSystemRepository;
            if ( !repository.Remove( this.ModelSystem ) )
            {
                error = "Deleting the model system '" + ModelSystem.Name + "' failed!";
                return false;
            }
            return true;
        }

        public bool Undo(ref string error)
        {
            return this.ModelSystem.Save( ref error );
        }
    }
}