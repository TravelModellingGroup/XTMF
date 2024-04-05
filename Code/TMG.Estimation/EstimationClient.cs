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
using System.Collections.Generic;
using System.Globalization;
using XTMF;
using XTMF.Networking;
using System.Threading;

namespace TMG.Estimation;

public class EstimationClient : IEstimationClientModelSystem
{

    [RunParameter( "Request Job Channel", 0, "The channel to use for requesting a new job." )]
    public int RequestJobChannel;

    [RunParameter( "Result Channel", 1, "The channel to use to communicate the results of a run." )]
    public int ResultChannel;

    [RunParameter( "Send Parameter Definitions", -1, "The channel to use for requesting the definitions for parameters." )]
    public int SendParameterDefinitions;

    [RunParameter( "Wait Time", 1000, "The amount of time(milliseconds) to wait for the host to reply to our request for a new job." )]
    public int MillisecondsToWait;

    [SubModelInformation( Required = true, Description = "The model system to execute to evaluate fitness." )]
    public IModelSystemTemplate MainClient { get; set; }

    public IClient ToHost;

    public string InputBaseDirectory { get; set; }

    public string OutputBaseDirectory { get; set; }

    public ParameterSetting[] Parameters { get; private set; }

    public MessageQueue<ClientTask> ClientTaskQueue;

    private volatile bool Exit;

    private IModelSystemStructure ClientStructure;

    private IConfiguration XtmfConfig;

    public ClientTask CurrentTask { get; private set; }

    public EstimationClient(IConfiguration xtmfConfig)
    {
        XtmfConfig = xtmfConfig;
    }

    private bool FindUs(IModelSystemStructure mst, ref IModelSystemStructure modelSystemStructure)
    {
        if ( mst.Module == this )
        {
            modelSystemStructure = mst;
            return true;
        }
        if ( mst.Children != null )
        {
            foreach ( var child in mst.Children )
            {
                if ( FindUs( child, ref modelSystemStructure ) )
                {
                    return true;
                }
            }
        }
        // Then we didn't find it in this tree
        return false;
    }

    public bool ExitRequest()
    {
        Exit = true;
        return true;
    }

    public void Start()
    {
        using ( ClientTaskQueue = new MessageQueue<ClientTask>() )
        {
            SetupHostConnection();
            GetParameters();
            while ( !Exit )
            {
                var task = ClientTaskQueue.GetMessageOrTimeout( MillisecondsToWait );
                if ( task != null )
                {
                    CurrentTask = task;
                    InitializeParameters( task );
                    MainClient.Start();
                    task.Result = RetrieveValue == null ? float.NaN : RetrieveValue();
                    ToHost.SendCustomMessage( task, ResultChannel );
                    if ( ClientTaskQueue.Count == 0 )
                    {
                        ToHost.SendCustomMessage( null, RequestJobChannel );
                    }
                    CurrentTask = null;
                    GC.Collect();
                }
                else
                {
                    ToHost.SendCustomMessage( null, RequestJobChannel );
                }
            }
        }
    }

    public Func<float> RetrieveValue { get; set; }

    private void InitializeParameters(ClientTask task)
    {
        string error = null;
        for ( int i = 0; i < task.ParameterValues.Length && i < Parameters.Length; i++ )
        {
            for ( int j = 0; j < Parameters[i].Names.Length; j++ )
            {
                if (
                    !Functions.ModelSystemReflection.AssignValue(XtmfConfig, ClientStructure, Parameters[i].Names[j],
                        task.ParameterValues[i].ToString(CultureInfo.InvariantCulture), ref error))
                {
                    throw new XTMFRuntimeException(this, $"In '{Name}' we were unable to assign a parameter!\r\n{error}");
                }
            }
        }
    }

    /// <summary>
    /// Get the parameters to be sent by the host
    /// </summary>
    private void GetParameters()
    {
        Parameters = null;
        // spin here until we have our parameters
        while ( Parameters == null )
        {
            ToHost.SendCustomMessage( null, SendParameterDefinitions );
            Thread.Sleep( 10 );
            Thread.MemoryBarrier();
        }
    }

    private void SetupHostConnection()
    {
        // The logic to send data
        ToHost.RegisterCustomSender( SendParameterDefinitions, (data, stream) =>
            {
                // do nothing, this will just request the parameters
            } );
        ToHost.RegisterCustomSender( RequestJobChannel, (data, stream) =>
            {
                // do nothing, this will just request a new job
            } );
        ToHost.RegisterCustomSender( ResultChannel, (data, stream) =>
            {
                var job = data as ClientTask;
                if (job == null)
                {
                    throw new XTMFRuntimeException(this, $"In {Name} we were given a task that was not a job!");
                }
                BinaryWriter writer = new( stream );
                writer.Write( job.Generation );
                writer.Write( job.Index );
                writer.Write( job.Result );
                writer.Flush();
            } );
        //The logic to receive data
        ToHost.RegisterCustomReceiver( RequestJobChannel, (stream) =>
            {
                BinaryReader reader = new( stream );
                ClientTask newTask = new()
                {
                    Generation = reader.ReadInt32(),
                    Index = reader.ReadInt32(),
                    ParameterValues = new float[reader.ReadInt32()]
                };
                for ( int i = 0; i < newTask.ParameterValues.Length; i++ )
                {
                    newTask.ParameterValues[i] = reader.ReadSingle();
                }
                ClientTaskQueue.Add( newTask );
                return null;
            } );
        ToHost.RegisterCustomReceiver( SendParameterDefinitions, (stream) =>
            {
                var parameters = new List<ParameterSetting>();
                BinaryReader reader = new( stream );
                var numberOfParameters = reader.ReadInt32();
                for ( int i = 0; i < numberOfParameters; i++ )
                {
                    string[] names = new string[reader.ReadInt32()];
                    for ( int j = 0; j < names.Length; j++ )
                    {
                        names[j] = reader.ReadString();
                    }
                    parameters.Add( new ParameterSetting()
                    {
                        Current = 0f,
                        Names = names,
                        Minimum = 0f,
                        Maximum = 0f
                    } );
                }
                Parameters = [.. parameters];
                return null;
            } );
    }

    public string Name { get; set; }

    public float Progress { get { return MainClient.Progress; } }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); }
    }

    public bool RuntimeValidation(ref string error)
    {
        IModelSystemStructure ourStructure = null;
        foreach ( var mst in XtmfConfig.ProjectRepository.ActiveProject.ModelSystemStructure )
        {
            if ( FindUs( mst, ref ourStructure ) )
            {
                foreach ( var child in ourStructure.Children )
                {
                    if ( child.ParentFieldName == "MainClient" )
                    {
                        ClientStructure = child;
                        break;
                    }
                }
                break;
            }
        }
        if ( ClientStructure == null )
        {
            error = "In '" + Name + "' we were unable to find the Client Model System!";
            return false;
        }
        return true;
    }
}
