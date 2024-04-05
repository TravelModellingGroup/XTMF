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
using System.Data;
using System.Data.SqlClient;
using System.Threading;
namespace Tasha.DataExtraction;

[ModuleInformation(Description=
    @"This module is designed to be used as a resource for a model system that needs access to a SQLServer.
It will required the use of a Connection String in order to access the database.  If you need to learn how to use
a connection string visit the <a href='http://www.connectionstrings.com/'>website</a> to learn how to build a connection string."
    )]
public sealed class SqlServerDataSource : IDataSource<IDbConnection>, IDisposable
{
    IDbConnection Connection;

    [RunParameter( "Connection String", "", "The connection string to access the SQLServer." )]
    public string ConnectionString;

    public IDbConnection GiveData()
    {
        return Connection;
    }

    public bool Loaded
    {
        get { return Connection != null; }
    }

    public void LoadData()
    {
        if ( Connection == null )
        {
            lock ( this )
            {
                Thread.MemoryBarrier();
                if ( Connection == null )
                {
                    try
                    {
                        Connection = new SqlConnection( ConnectionString );
                        Connection.Open();
                    }
                    catch ( SqlException )
                    {
                        throw new XTMFRuntimeException(this, "In '" + Name + "' we were unable to connect to the the SQLServer."
                         + "Please check to make sure that the SQLServer is online and the connection string is correct." );
                    }
                }
            }
        }
    }

    public void UnloadData()
    {
        LocalDispose();
    }

    public void Dispose()
    {
        LocalDispose();
        // we have already been finalized
        GC.SuppressFinalize( this );
    }

    private void LocalDispose()
    {
        if ( Connection != null )
        {
            Connection.Dispose();
        }
        Connection = null;
    }

    ~SqlServerDataSource()
    {
        LocalDispose();
    }

    public string Name { get; set; }

    public float Progress
    {
        get { return 0f; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return null; }
    }

    public bool RuntimeValidation(ref string error)
    {
        if ( String.IsNullOrWhiteSpace( ConnectionString ) )
        {
            error = "In '" + Name + "' the connection string is empty!  A connection string is required to access the database!";
            return false;
        }
        return true;
    }
}
