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
using TMG;
using XTMF;

namespace Tasha.Common
{
    public class ResourceLookup : IResource
    {
        [RootModule]
        public IResourceSource Root;

        private IResource LinkedResource;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        [RunParameter( "Resource Name", "UniqueName", "The unique name for this resource." )]
        public string ResourceName
        {
            get;
            set;
        }

        public T AquireResource<T>()
        {
            return this.LinkedResource.AquireResource<T>();
        }

        public bool CheckResourceType(Type dataType)
        {
            EnsureLink();
            return this.LinkedResource.CheckResourceType( dataType );
        }

        public bool CheckResourceType<T>()
        {
            EnsureLink();
            return this.LinkedResource.CheckResourceType<T>();
        }

        public Type GetResourceType()
        {
            EnsureLink();
            return this.LinkedResource.GetResourceType();
        }

        private void EnsureLink()
        {
            if ( this.LinkedResource == null )
            {
                string error = null;
                if ( !this.RuntimeValidation( ref error ) )
                {
                    throw new XTMFRuntimeException( error );
                }
            }
        }



        public void ReleaseResource()
        {
            this.LinkedResource.ReleaseResource();
        }

        public bool RuntimeValidation(ref string error)
        {
            foreach ( var resource in this.Root.Resources )
            {
                if ( resource.ResourceName == this.ResourceName )
                {
                    this.LinkedResource = resource;
                    return true;
                }
            }
            error = "In '" + this.Name + "' we were unable to find a resource in the closest resource source with the name '" + this.ResourceName + "'";
            return false;
        }

        public IDataSource GetDataSource()
        {
            return LinkedResource.GetDataSource();
        }
    }
}