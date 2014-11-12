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
using System.IO;

namespace XTMF.Commands
{
    public class ImportModelSystem : ICommand
    {
        private IConfiguration Configuration;
        private string Location;
        private IModelSystem ModelSystem;
        private string Name;
        private bool Replace;

        public ImportModelSystem(IConfiguration configuration, string location, string name, bool replace = false)
        {
            this.Configuration = configuration;
            this.Location = location;
            this.Name = name;
            this.Replace = replace;
        }

        public bool Do(ref string error)
        {
            if ( this.Replace )
            {
                var modelSystems = this.Configuration.ModelSystemRepository.ModelSystems;
                for ( int i = 0; i < modelSystems.Count; i++ )
                {
                    if ( modelSystems[i].Name.Equals( this.Name, StringComparison.InvariantCultureIgnoreCase ) )
                    {
                        this.Configuration.ModelSystemRepository.Remove( modelSystems[i] );
                        break;
                    }
                }
            }
            try
            {
                File.Copy( this.Location, Path.Combine( this.Configuration.ModelSystemDirectory, this.Name + ".xml" ), true );
            }
            catch ( IOException )
            {
            }
            ModelSystem ms = new ModelSystem( this.Configuration, this.Name );
            this.Configuration.ModelSystemRepository.Add( ms );
            this.ModelSystem = ms;
            return ms.Save( ref error );
        }

        public bool Undo(ref string error)
        {
            if ( !this.Configuration.ModelSystemRepository.Remove( this.ModelSystem ) )
            {
                error = "We were unable to remove the model system '" + this.ModelSystem.Name + "'.";
                return false;
            }
            return true;
        }
    }
}