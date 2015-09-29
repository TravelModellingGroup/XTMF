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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TMG.Input;
using XTMF;
using XTMF.Networking;

namespace TMG.Estimation
{
    public sealed class EstimationHost : IEstimationHost, IDisposable
    {
        [SubModelInformation(Required = true, Description = "The AI to explore the parameter space.")]
        public IEstimationAI AI;

        [SubModelInformation(Required = true, Description = "The client model system to execute.")]
        public IEstimationClientModelSystem ClientModelSystem;

        public bool Exit = false;

        [RunParameter("Hold Onto Result File", true, "Should we maintain the lock on the estimation file?")]
        public bool HoldOnToResultFile;

        /// <summary>
        /// The networking interface for the model system
        /// </summary>
        public IHost Host;

        [SubModelInformation(Required = false, Description = "The host model system to execute.")]
        public IModelSystemTemplate HostModelSystem;

        [SubModelInformation(Required = true, Description = "The logic to load in parameters.")]
        public IDataSource<List<ParameterSetting>> ParameterLoader;

        [RunParameter("Request Job Channel", 0, "The channel to use for requesting a new job.")]
        public int RequestJobChannel;

        [RunParameter("Result Channel", 1, "The channel to use to communicate the results of a run.")]
        public int ResultChannel;

        [SubModelInformation(Required = true, Description = "The location to save the estimation results.")]
        public FileLocation ResultFile;

        [RunParameter("Send Parameter Definitions", -1, "The channel to use for requesting the definitions for parameters.")]
        public int SendParameterDefinitions;

        private List<IRemoteXTMF> AvailableClients = new List<IRemoteXTMF>();
        private IModelSystemStructure ClientStructure;
        private bool FirstLineToWrite = true;

        private IModelSystemStructure OurStructure;
        private BlockingCollection<ResultMessage> PendingResults;

        private StreamWriter ResultFileWriter;
        private Func<string> Status = () => "Initializing";
        private IConfiguration XtmfConfig;

        public EstimationHost(IConfiguration xtmfConfig)
        {
            this.XtmfConfig = xtmfConfig;
        }

        public int CurrentIteration { get; set; }

        public List<Job> CurrentJobs { get; set; }

        [RunParameter("Input Directory", "../../Input", "The directory that the data is located in.")]
        public string InputBaseDirectory { get; set; }

        public string Name { get; set; }

        public string OutputBaseDirectory { get; set; }

        public List<ParameterSetting> Parameters { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); }
        }

        [RunParameter("Generations", 100, "The numbers of generations to run this estimation for.")]
        public int TotalIterations { get; set; }

        public bool ExitRequest()
        {
            this.Exit = true;
            return true;
        }

        public int IndexOfNextJob()
        {
            int i = 0;
            for ( ; i < this.CurrentJobs.Count; i++ )
            {
                if ( !this.CurrentJobs[i].Processing )
                {
                    return i;
                }
            }
            return -1;
        }

        public bool RuntimeValidation(ref string error)
        {
            foreach ( var mst in this.XtmfConfig.ProjectRepository.ActiveProject.ModelSystemStructure )
            {
                if ( FindUs( mst, ref this.OurStructure ) )
                {
                    foreach ( var child in OurStructure.Children )
                    {
                        if ( child.ParentFieldName == "ClientModelSystem" )
                        {
                            this.ClientStructure = child;
                            break;
                        }
                    }
                    break;
                }
            }
            if ( this.OurStructure == null )
            {
                error = "In '" + this.Name + "' we were unable to find ourselves through XTMF inside of project " + this.XtmfConfig.ProjectRepository.ActiveProject.Name;
                return false;
            }
            if ( this.ClientStructure == null )
            {
                error = "In '" + this.Name + "' we were unable to find our client model system!";
                return false;
            }
            return true;
        }

        public void Start()
        {
            int generation = 0;
            this.Status = () => "Initializing Parameters";
            LoadParameters();
            this.Status = () => "Initializing Networking";
            SetupNetworking();
            this.PendingResults = new BlockingCollection<ResultMessage>();
            using (var finishedGeneration = new MessageQueue<bool?>())
            {
                //execute the host model system
                this.Status = () => "Running Host Model System";
                if ( this.HostModelSystem != null )
                {
                    this.HostModelSystem.Start();
                }
                this.Status = () => "Distributing Tasks: Generation " + ( this.CurrentIteration + 1 ) + " / " + this.TotalIterations;
                Task processResults = Task.Factory.StartNew( () =>
                    {
                        foreach ( var result in this.PendingResults.GetConsumingEnumerable() )
                        {
                            // only process things from the current generation
                            if ( generation != result.Generation ) continue;
                            Job currentJob = null;
                            lock (this.CurrentJobs)
                            {
                                currentJob = this.CurrentJobs[result.ProcessedIndex];
                                currentJob.Value = result.ProcessedValue;
                                currentJob.Processed = true;
                                // store the result before starting the next generation
                                // so the AI can play with the values after we write
                                this.StoreResult( currentJob );
                            }
                            this.Progress += 1.0f / ( this.TotalIterations * this.CurrentJobs.Count );
                            //scan the rest of the jobs to see if they have been processed
                            finishedGeneration.Add( CheckForAllDone() );
                        }
                    } );

                for ( ; generation < this.TotalIterations & this.Exit == false; generation++ )
                {
                    this.CurrentIteration = generation;
                    this.CurrentJobs = this.AI.CreateJobsForIteration();
                    this.StartGeneration();
                    bool? done;
                    while ( this.Exit == false && ( done = finishedGeneration.GetMessageOrTimeout( 100 ) ) != true )
                    {
                        if ( done == null && this.Host.ConnectedClients.Count > 0 )
                        {
                        }
                        System.Threading.Thread.MemoryBarrier();
                    }
                    this.AI.IterationComplete();
                    // make sure to clear this to make sure we exit fine
                    System.Threading.Thread.MemoryBarrier();
                }
                this.PendingResults.CompleteAdding();
                processResults.Wait();
            }
            if ( this.ResultFileWriter != null )
            {
                this.ResultFileWriter.Close();
            }
            lock (this.Host)
            {
                foreach ( var client in this.Host.ConnectedClients )
                {
                    client.SendCancel( "End of model run" );
                }
            }
            this.Host.Shutdown();
        }

        public override string ToString()
        {
            return this.Status();
        }

        private bool CheckForAllDone()
        {
            for ( int i = 0; i < this.CurrentJobs.Count; i++ )
            {
                if ( !this.CurrentJobs[i].Processed )
                {
                    return false;
                }
            }
            return true;
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

        private void LoadParameters()
        {
            this.ParameterLoader.LoadData();
            this.Parameters = this.ParameterLoader.GiveData();
            this.ParameterLoader.UnloadData();
        }

        private void SendNewJob(IRemoteXTMF client)
        {
            System.Threading.Thread.MemoryBarrier();
            int i = IndexOfNextJob();
            if ( i >= 0 )
            {
                this.CurrentJobs[i].Processing = true;
                this.CurrentJobs[i].ProcessedBy = client;
                System.Threading.Thread.MemoryBarrier();
                client.SendCustomMessage( i, this.RequestJobChannel );
            }
        }

        private void SetupNetworking()
        {
            this.Host.RegisterCustomReceiver( this.ResultChannel, (stream, client) =>
                {
                    lock (this)
                    {
                        BinaryReader reader = new BinaryReader( stream );
                        this.PendingResults.Add( new ResultMessage()
                        {
                            Generation = reader.ReadInt32(),
                            ProcessedIndex = reader.ReadInt32(),
                            ProcessedValue = reader.ReadSingle()
                        } );
                        // do not dispose
                        reader = null;
                    }
                    return null;
                } );
            this.Host.RegisterCustomReceiver( this.RequestJobChannel, (stream, client) =>
                {
                    lock (Host)
                    {
                        lock (this.CurrentJobs)
                        {
                            this.SendNewJob( client );
                        }
                    }
                    return null;
                } );
            this.Host.RegisterCustomSender( this.RequestJobChannel, (data, client, stream) =>
                {
                    lock (this)
                    {
                        var index = (int)data;
                        var job = this.CurrentJobs[index];
                        BinaryWriter writer = new BinaryWriter( stream );
                        writer.Write( this.CurrentIteration );
                        writer.Write( index );
                        writer.Write( job.Parameters.Length );
                        for ( int i = 0; i < job.Parameters.Length; i++ )
                        {
                            writer.Write( job.Parameters[i].Current );
                        }
                        writer.Flush();
                        writer = null;
                    }
                } );
            this.Host.RegisterCustomReceiver( this.SendParameterDefinitions, (stream, client) =>
                {
                    lock (this.Host)
                    {
                        client.SendCustomMessage( null, this.SendParameterDefinitions );
                        if ( !this.AvailableClients.Contains( client ) )
                        {
                            this.AvailableClients.Add( client );
                        }
                    }
                    return null;
                } );
            this.Host.RegisterCustomSender( this.SendParameterDefinitions, (data, client, stream) =>
                {
                    BinaryWriter writer = new BinaryWriter( stream );
                    writer.Write( this.Parameters.Count );
                    for ( int i = 0; i < this.Parameters.Count; i++ )
                    {
                        writer.Write( this.Parameters[i].Names.Length );
                        for ( int j = 0; j < this.Parameters[i].Names.Length; j++ )
                        {
                            writer.Write( this.Parameters[i].Names[j] );
                        }
                    }
                    writer.Flush();
                    writer = null;
                } );
            this.Host.NewClientConnected += (client) =>
                {
                    lock (this.Host)
                    {
                        client.SendModelSystem( this.ClientStructure );
                        this.XtmfConfig.CreateProgressReport( client.UniqueID + " " + client.MachineName,
                            () => client.Progress, new Tuple<byte, byte, byte>( 50, 50, 150 ) );
                    }
                };
            this.Host.ClientDisconnected += (client) =>
                {
                    lock (this.Host)
                    {
                        this.AvailableClients.Remove( client );
                        foreach ( var job in this.CurrentJobs )
                        {
                            if ( job.ProcessedBy == client )
                            {
                                if ( !job.Processed && job.Processing )
                                {
                                    job.Processing = false;
                                    job.ProcessedBy = null;
                                }
                            }
                        }
                        this.XtmfConfig.DeleteProgressReport( client.UniqueID + " " + client.MachineName );
                    }
                };
        }

        private void StartGeneration()
        {
            // send each client a job to start with
            lock (this.Host)
            {
                lock (this.CurrentJobs)
                {
                    foreach ( var client in this.AvailableClients )
                    {
                        this.SendNewJob( client );
                    }
                }
            }
        }

        private void StoreResult(Job currentJob)
        {
            StringBuilder toWrite = new StringBuilder();
            toWrite.Append( this.CurrentIteration );
            toWrite.Append( ',' );
            toWrite.Append( currentJob.Value );
            for ( int i = 0; i < currentJob.Parameters.Length; i++ )
            {
                for ( int j = 0; j < this.Parameters[i].Names.Length; j++ )
                {
                    toWrite.Append( ',' );
                    // this uses the i th value since they are all the same
                    toWrite.Append( currentJob.Parameters[i].Current );
                }
            }
            while ( true )
            {
                try
                {
                    if ( this.HoldOnToResultFile )
                    {
                        if ( this.ResultFileWriter == null )
                        {
                            this.ResultFileWriter = new StreamWriter( this.ResultFile.GetFilePath() );
                        }
                        Write( currentJob, toWrite, this.ResultFileWriter );
                    }
                    else
                    {
                        using (var writer = new StreamWriter( this.ResultFile.GetFilePath(), true ))
                        {
                            Write( currentJob, toWrite, writer );
                        }
                    }
                }
                catch
                {
                    // let them close the file
                    System.Threading.Thread.Sleep( 10 );
                }
                break;
            }
        }

        private void Write(Job currentJob, StringBuilder toWrite, StreamWriter writer)
        {
            if ( this.FirstLineToWrite )
            {
                // write header here
                StringBuilder header = new StringBuilder();
                header.Append( "Generation,Value" );
                for ( int i = 0; i < currentJob.Parameters.Length; i++ )
                {
                    for ( int j = 0; j < this.Parameters[i].Names.Length; j++ )
                    {
                        header.Append( ',' );
                        header.Append('"');
                        header.Append( this.Parameters[i].Names[j] );
                        header.Append('"');
                    }
                }
                writer.WriteLine( header.ToString() );
                this.FirstLineToWrite = false;
            }
            writer.WriteLine( toWrite.ToString() );
        }

        private class ResultMessage
        {
            internal int Generation;
            internal int ProcessedIndex;
            internal float ProcessedValue;
        }

        public void Dispose()
        {
            if ( this.ResultFileWriter != null )
            {
                this.ResultFileWriter.Dispose();
                this.ResultFileWriter = null;
            }
            if ( this.PendingResults != null )
            {
                this.PendingResults.Dispose();
                this.PendingResults = null;
            }
        }
    }
}