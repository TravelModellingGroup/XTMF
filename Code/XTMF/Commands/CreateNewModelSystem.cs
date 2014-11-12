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
using System.Linq;

namespace XTMF.Commands
{
    internal class CreateNewModelSystem : ICommand
    {
        public ModelSystem GeneratedModelSystem;
        private IConfiguration Config;
        private string Description;
        private List<ILinkedParameter> LinkedParameters;
        private string Name;
        private IModelSystemStructure Strucuture;

        public CreateNewModelSystem(IConfiguration config, string name, string description, IModelSystemStructure strucuture, List<ILinkedParameter> linkedParameters)
        {
            this.Config = config;
            this.Name = name;
            this.Description = description;
            this.Strucuture = strucuture;
            this.LinkedParameters = linkedParameters;
        }

        public bool Do(ref string error)
        {
            var repository = Config.ModelSystemRepository;
            if ( !Project.ValidateProjectName( this.Name ) )
            {
                return false;
            }
            if ( repository.ModelSystems.Any( (ms) => ms.Name.Equals( this.Name, StringComparison.InvariantCultureIgnoreCase ) ) )
            {
                error = "There already exists a model system with the name '" + this.Name + "'!";
                return false;
            }
            this.GeneratedModelSystem = new ModelSystem( Config, this.Name );
            this.GeneratedModelSystem.Description = Description;
            if ( this.Strucuture != null )
            {
                this.GeneratedModelSystem.ModelSystemStructure = this.Strucuture.Clone();
                SetupLinkedParameters( ref error );
            }
            if ( !GeneratedModelSystem.Save( ref error ) )
            {
                this.GeneratedModelSystem = null;
                return false;
            }
            repository.Add( this.GeneratedModelSystem );
            return true;
        }

        public bool Undo(ref string error)
        {
            // we only need to remove it if it exists
            if ( GeneratedModelSystem == null )
            {
                return true;
            }
            var ModelSystemRepository = this.Config.ModelSystemRepository;
            ModelSystemRepository.Remove( GeneratedModelSystem );
            return true;
        }

        private IModuleParameter GetCorrespondingParameter(IModuleParameter p, IModelSystemStructure oldMSS)
        {
            return GetCorrespondingParameter( p, oldMSS, this.GeneratedModelSystem.ModelSystemStructure );
        }

        private IModuleParameter GetCorrespondingParameter(IModuleParameter p, IModelSystemStructure mss, IModelSystemStructure newMSS)
        {
            if ( mss == p.BelongsTo )
            {
                foreach ( var param in newMSS.Parameters.Parameters )
                {
                    if ( param.Name == p.Name )
                    {
                        return param;
                    }
                }
                return null;
            }

            var list = mss.Children;
            var newList = newMSS.Children;
            if ( list == null | newList == null )
            {
                return null;
            }
            for ( int i = 0; i < list.Count; i++ )
            {
                var ret = GetCorrespondingParameter( p, list[i], newList[i] );
                if ( ret != null )
                {
                    return ret;
                }
            }
            return null;
        }

        private bool SetupLinkedParameters(ref string error)
        {
            var linkedParameterList = this.LinkedParameters;
            if ( linkedParameterList != null )
            {
                foreach ( var lp in linkedParameterList )
                {
                    var newLp = new LinkedParameter( lp.Name );
                    if ( !newLp.SetValue( lp.Value, ref error ) )
                    {
                        return false;
                    }
                    foreach ( var p in lp.Parameters )
                    {
                        var copiedParameter = GetCorrespondingParameter( p, this.Strucuture );
                        if ( copiedParameter != null )
                        {
                            newLp.Add( copiedParameter, ref error );
                        }
                    }
                    this.GeneratedModelSystem.LinkedParameters.Add( newLp );
                }
            }
            return true;
        }
    }
}