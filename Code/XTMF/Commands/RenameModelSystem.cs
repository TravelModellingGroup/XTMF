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
    internal class RenameModelSystem : ICommand
    {
        private IConfiguration Config;
        private IModelSystem ModelSystem;

        private string OtherName;

        public RenameModelSystem(IConfiguration config, IModelSystem modelSystem, string newName)
        {
            this.Config = config;
            this.OtherName = newName;
            this.ModelSystem = modelSystem;
        }

        public bool Do(ref string error)
        {
            var repository = Config.ModelSystemRepository;
            if ( !Project.ValidateProjectName( this.OtherName ) )
            {
                error = "The model system name '" + this.OtherName + "' is an invalid name for a model system!";
                return false;
            }
            if ( repository.ModelSystems.Any( (ms) => ms.Name.Equals( this.OtherName, StringComparison.InvariantCultureIgnoreCase ) ) )
            {
                error = "There already exists a model system with the name '" + this.OtherName + "'!";
                return false;
            }
            if ( !repository.Remove( this.ModelSystem ) )
            {
                error = "The model system '" + this.ModelSystem.Name + "' does not exist in the model system repository!";
                return false;
            }
            var oldName = this.ModelSystem.Name;
            this.ModelSystem.Name = this.OtherName;
            this.OtherName = oldName;
            repository.Add( this.ModelSystem );
            return true;
        }

        public bool Undo(ref string error)
        {
            return Do( ref error );
        }
    }
}