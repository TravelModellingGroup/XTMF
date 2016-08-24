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

        private IConfiguration Config;

        public ResourceLookup(IConfiguration config)
        {
            Config = config;
        }

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

        public T AcquireResource<T>()
        {
            return LinkedResource.AcquireResource<T>();
        }

        public bool CheckResourceType(Type dataType)
        {
            EnsureLink();
            return LinkedResource.CheckResourceType( dataType );
        }

        public bool CheckResourceType<T>()
        {
            EnsureLink();
            return LinkedResource.CheckResourceType<T>();
        }

        public Type GetResourceType()
        {
            EnsureLink();
            return LinkedResource.GetResourceType();
        }

        private void EnsureLink()
        {
            if (LinkedResource == null )
            {
                string error = null;
                if ( !RuntimeValidation( ref error ) )
                {
                    throw new XTMFRuntimeException( error );
                }
            }
        }



        public void ReleaseResource()
        {
            LinkedResource.ReleaseResource();
        }

        public bool RuntimeValidation(ref string error)
        {
            var ancestry = TMG.Functions.ModelSystemReflection.BuildModelStructureChain(Config, this);
            for (int i = ancestry.Count - 1; i >= 0; i--)
            {
                var source = ancestry[i]?.Module as IResourceSource;
                if(source != null)
                {
                    foreach (var resource in source.Resources)
                    {
                        if (resource.ResourceName == ResourceName)
                        {
                            LinkedResource = resource;
                            return true;
                        }
                    }
                }
            }
            error = "In '" + Name + "' we were unable to find a resource in the closest resource source with the name '" + ResourceName + "'";
            return false;
        }

        public IDataSource GetDataSource()
        {
            return LinkedResource.GetDataSource();
        }
    }
}