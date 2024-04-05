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
using XTMF;

namespace Tasha.Common;

public class Resource : IResource
{
    public IDataSource DataSource;

    private volatile bool Loaded;

    public string Name { get; set; }

    public float Progress
    {
        get { return 0; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return null; }
    }

    [RunParameter("Unload after acquire", false, "Should re release this resource after it has been acquired?")]
    public bool UnloadAfterAcquired;

    [RunParameter("Resource Name", "UniqueName", "The unique name for this resource.")]
    public string ResourceName { get; set; }

    public T AcquireResource<T>()
    {
        var source = DataSource as IDataSource<T>;
        if(source == null)
        {
            return default(T);
        }
        // check to see if the resource needs to be loaded

        lock (this)
        {
            if(!Loaded)
            {
                DataSource.LoadData();
                Loaded = true;
            }
            var data = source.GiveData();
            if(UnloadAfterAcquired)
            {
                Loaded = false;
                source.UnloadData();
            }
            return data;
        }
    }

    public bool CheckResourceType(Type dataType)
    {
        return GetLocalType() == dataType;
    }

    public bool CheckResourceType<T>()
    {
        return (DataSource as IDataSource<T>) != null;
    }

    private Type GetLocalType()
    {
        var interfaces = DataSource.GetType().GetInterfaces();
        Type dataSourceInterface = null;
        foreach(var t in interfaces)
        {
            if(t.IsGenericType)
            {
                if(t.FullName.StartsWith("XTMF.IDataSource`1"))
                {
                    dataSourceInterface = t;
                    break;
                }
            }
        }
        var genericArguments = dataSourceInterface?.GetGenericArguments();
        if(genericArguments == null || genericArguments.Length != 1)
        {
            return null;
        }
        return genericArguments[0];
    }

    public Type GetResourceType()
    {
        return GetLocalType();
    }

    public void ReleaseResource()
    {
        if(Loaded)
        {
            // Unload the data
            lock (this)
            {
                if(Loaded)
                {
                    DataSource.UnloadData();
                    Loaded = false;
                }
            }
        }
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public IDataSource GetDataSource()
    {
        return DataSource;
    }
}