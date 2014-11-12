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
using System.Collections.Generic;

namespace XTMF.Commands
{
    internal class AddModelSystemToProject : ICommand
    {
        private List<ILinkedParameter> CopiedLinkedParameters;
        private IModelSystem ModelSystem;
        private IModelSystemStructure MSS;
        private IProject Project;

        public AddModelSystemToProject(IProject project, IModelSystem modelSystem)
        {
            this.Project = project;
            this.ModelSystem = modelSystem;
        }

        public bool Do(ref string error)
        {
            if ( this.MSS == null )
            {
                this.MSS = this.ModelSystem.ModelSystemStructure.Clone();
                if ( !SetupLinkedParameters( ref error ) )
                {
                    return false;
                }
            }
            this.Project.LinkedParameters.Add( this.CopiedLinkedParameters );
            this.Project.ModelSystemStructure.Add( this.MSS );
            return true;
        }

        public bool Undo(ref string error)
        {
            this.Project.ModelSystemStructure.Remove( this.MSS );
            this.Project.LinkedParameters.Remove( this.CopiedLinkedParameters );
            return true;
        }

        private IModuleParameter GetCorrespondingParameter(IModuleParameter p, IModelSystemStructure newMSS)
        {
            return GetCorrespondingParameter( p, this.ModelSystem.ModelSystemStructure, newMSS );
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
            var linkedParameterList = this.ModelSystem.LinkedParameters;
            if ( linkedParameterList != null )
            {
                CopiedLinkedParameters = new List<ILinkedParameter>( linkedParameterList.Count );
                foreach ( var lp in linkedParameterList )
                {
                    var newLp = new LinkedParameter( lp.Name );
                    if ( !newLp.SetValue( lp.Value, ref error ) )
                    {
                        return false;
                    }
                    foreach ( var p in lp.Parameters )
                    {
                        newLp.Add( GetCorrespondingParameter( p, this.MSS ), ref error );
                    }
                    CopiedLinkedParameters.Add( newLp );
                }
            }
            return true;
        }
    }
}