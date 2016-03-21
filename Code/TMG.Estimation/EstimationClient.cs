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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XTMF;
using XTMF.Networking;
using System.Threading;
using System.Threading.Tasks;
using TMG;
namespace TMG.Estimation
{
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

        private volatile bool Exit = false;

        private IModelSystemStructure ClientStructure;

        private IConfiguration XtmfConfig;

        public ClientTask CurrentTask { get; private set; }

        public EstimationClient(IConfiguration xtmfConfig)
        {
            this.XtmfConfig = xtmfConfig;
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
            this.Exit = true;
            return true;
        }

        public void Start()
        {
            using ( this.ClientTaskQueue = new MessageQueue<ClientTask>() )
            {
                this.SetupHostConnection();
                this.GetParameters();
                while ( !this.Exit )
                {
                    var task = this.ClientTaskQueue.GetMessageOrTimeout( MillisecondsToWait );
                    if ( task != null )
                    {
                        this.CurrentTask = task;
                        InitializeParameters( task );
                        this.MainClient.Start();
                        task.Result = this.RetrieveValue == null ? float.NaN : this.RetrieveValue();
                        this.ToHost.SendCustomMessage( task, this.ResultChannel );
                        if ( this.ClientTaskQueue.Count == 0 )
                        {
                            this.ToHost.SendCustomMessage( null, this.RequestJobChannel );
                        }
                        this.CurrentTask = null;
                        GC.Collect();
                    }
                    else
                    {
                        this.ToHost.SendCustomMessage( null, this.RequestJobChannel );
                    }
                }
            }
        }

        public Func<float> RetrieveValue { get; set; }

        private void InitializeParameters(ClientTask task)
        {
            for ( int i = 0; i < task.ParameterValues.Length && i < this.Parameters.Length; i++ )
            {
                for ( int j = 0; j < this.Parameters[i].Names.Length; j++ )
                {
                    AssignValue( this.Parameters[i].Names[j], task.ParameterValues[i] );
                }
            }
        }

        private void AssignValue(string parameterName, float value)
        {
            string[] parts = SplitNameToParts( parameterName );
            AssignValue( parts, 0, this.ClientStructure, value );
        }

        private void AssignValue(string[] parts, int currentIndex, IModelSystemStructure currentStructure, float value)
        {
            if ( currentIndex == parts.Length - 1 )
            {
                AssignValue( parts[currentIndex], currentStructure, value );
                return;
            }
            if ( currentStructure.Children != null )
            {
                for ( int i = 0; i < currentStructure.Children.Count; i++ )
                {
                    if ( currentStructure.Children[i].Name == parts[currentIndex] )
                    {
                        AssignValue( parts, currentIndex + 1, currentStructure.Children[i], value );
                        return;
                    }
                }
            }
            throw new XTMFRuntimeException( "Unable to find a child module in '" + currentStructure.Name + "' named '" + parts[currentIndex]
                + "' in order to assign parameters!" );
        }

        private void AssignValue(string variableName, IModelSystemStructure currentStructure, float value)
        {
            if ( currentStructure == null )
            {
                throw new XTMFRuntimeException( "Unable to assign '" + variableName + "', the module is null!" );
            }
            var p = currentStructure.Parameters;
            if ( p == null )
            {
                throw new XTMFRuntimeException( "The structure '" + currentStructure.Name + "' has no parameters!" );
            }
            var parameters = p.Parameters;
            bool any = false;
            if ( parameters != null )
            {
                for ( int i = 0; i < parameters.Count; i++ )
                {
                    if ( parameters[i].Name == variableName )
                    {
                        var type = currentStructure.Module.GetType();
                        if ( parameters[i].OnField )
                        {
                            var field = type.GetField( parameters[i].VariableName );
                            field.SetValue( currentStructure.Module, value );
                            any = true;
                        }
                        else
                        {
                            var field = type.GetProperty( parameters[i].VariableName );
                            field.SetValue( currentStructure.Module, value, null );
                            any = true;
                        }
                    }
                }
            }
            if ( !any )
            {
                throw new XTMFRuntimeException( "Unable to find a parameter named '" + variableName
                    + "' for module '" + currentStructure.Name + "' in order to assign it a parameter!" );
            }
        }

        private string[] SplitNameToParts(string parameterName)
        {
            List<string> parts = new List<string>();
            var stringLength = parameterName.Length;
            StringBuilder builder = new StringBuilder();
            for ( int i = 0; i < stringLength; i++ )
            {
                switch ( parameterName[i] )
                {
                    case '.':
                        parts.Add( builder.ToString() );
                        builder.Clear();
                        break;
                    case '\\':
                        if ( i + 1 < stringLength )
                        {
                            if ( parameterName[i + 1] == '.' )
                            {
                                builder.Append( '.' );
                                i += 2;
                            }
                            else if ( parameterName[i + 1] == '\\' )
                            {
                                builder.Append( '\\' );
                            }
                        }
                        break;
                    default:
                        builder.Append( parameterName[i] );
                        break;
                }
            }
            parts.Add( builder.ToString() );
            return parts.ToArray();
        }

        /// <summary>
        /// Get the parameters to be sent by the host
        /// </summary>
        private void GetParameters()
        {
            this.Parameters = null;
            // spin here until we have our parameters
            while ( this.Parameters == null )
            {
                this.ToHost.SendCustomMessage( null, this.SendParameterDefinitions );
                Thread.Sleep( 10 );
                Thread.MemoryBarrier();
            }
        }

        private void SetupHostConnection()
        {
            // The logic to send data
            this.ToHost.RegisterCustomSender( this.SendParameterDefinitions, (data, stream) =>
                {
                    // do nothing, this will just request the parameters
                } );
            this.ToHost.RegisterCustomSender( this.RequestJobChannel, (data, stream) =>
                {
                    // do nothing, this will just request a new job
                } );
            this.ToHost.RegisterCustomSender( this.ResultChannel, (data, stream) =>
                {
                    var job = data as ClientTask;
                    BinaryWriter writer = new BinaryWriter( stream );
                    writer.Write( job.Generation );
                    writer.Write( job.Index );
                    writer.Write( job.Result );
                    writer.Flush();
                    writer = null;
                } );
            //The logic to receive data
            this.ToHost.RegisterCustomReceiver( this.RequestJobChannel, (stream) =>
                {
                    BinaryReader reader = new BinaryReader( stream );
                    ClientTask newTask = new ClientTask();
                    newTask.Generation = reader.ReadInt32();
                    newTask.Index = reader.ReadInt32();
                    newTask.ParameterValues = new float[reader.ReadInt32()];
                    for ( int i = 0; i < newTask.ParameterValues.Length; i++ )
                    {
                        newTask.ParameterValues[i] = reader.ReadSingle();
                    }
                    reader = null;
                    this.ClientTaskQueue.Add( newTask );
                    return null;
                } );
            this.ToHost.RegisterCustomReceiver( this.SendParameterDefinitions, (stream) =>
                {
                    var parameters = new List<ParameterSetting>();
                    BinaryReader reader = new BinaryReader( stream );
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
                    this.Parameters = parameters.ToArray();
                    return null;
                } );
        }

        public string Name { get; set; }

        public float Progress { get { return this.MainClient.Progress; } }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); }
        }

        public bool RuntimeValidation(ref string error)
        {
            IModelSystemStructure ourStructure = null;
            foreach ( var mst in this.XtmfConfig.ProjectRepository.ActiveProject.ModelSystemStructure )
            {
                if ( FindUs( mst, ref ourStructure ) )
                {
                    foreach ( var child in ourStructure.Children )
                    {
                        if ( child.ParentFieldName == "MainClient" )
                        {
                            this.ClientStructure = child;
                            break;
                        }
                    }
                    break;
                }
            }
            if ( ClientStructure == null )
            {
                error = "In '" + this.Name + "' we were unable to find the Client Model System!";
                return false;
            }
            return true;
        }
    }
}
