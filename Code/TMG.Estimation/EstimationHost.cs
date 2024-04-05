/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
        // ReSharper disable once InconsistentNaming
        public IEstimationAI AI;

        [SubModelInformation(Required = true, Description = "The client model system to execute.")]
        public IEstimationClientModelSystem ClientModelSystem;

        public bool Exit;

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

        [RunParameter("Fail On Invalid Fitness", true, "Should the estimation terminate if an invalid fitness value is reported?")]
        public bool FailOnInvalidFitness;

        private readonly List<IRemoteXTMF> AvailableClients = [];
        private IModelSystemStructure ClientStructure;
        private bool FirstLineToWrite = true;

        private IModelSystemStructure OurStructure;
        private BlockingCollection<ResultMessage> PendingResults;

        private StreamWriter ResultFileWriter;
        private Func<string> Status = () => "Initializing";
        private IConfiguration XtmfConfig;

        public EstimationHost(IConfiguration xtmfConfig)
        {
            XtmfConfig = xtmfConfig;
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
            Exit = true;
            return true;
        }

        public int IndexOfNextJob()
        {
            int i = 0;
            for ( ; i < CurrentJobs.Count; i++ )
            {
                if ( !CurrentJobs[i].Processing )
                {
                    return i;
                }
            }
            return -1;
        }

        public bool RuntimeValidation(ref string error)
        {
            foreach ( var mst in XtmfConfig.ProjectRepository.ActiveProject.ModelSystemStructure )
            {
                if ( FindUs( mst, ref OurStructure ) )
                {
                    foreach ( var child in OurStructure.Children )
                    {
                        if ( child.ParentFieldName == "ClientModelSystem" )
                        {
                            ClientStructure = child;
                            break;
                        }
                    }
                    break;
                }
            }
            if ( OurStructure == null )
            {
                error = "In '" + Name + "' we were unable to find ourselves through XTMF inside of project " + XtmfConfig.ProjectRepository.ActiveProject.Name;
                return false;
            }
            if ( ClientStructure == null )
            {
                error = "In '" + Name + "' we were unable to find our client model system!";
                return false;
            }
            return true;
        }

        public void Start()
        {
            int generation = 0;
            Status = () => "Initializing Parameters";
            LoadParameters();
            Status = () => "Initializing Networking";
            SetupNetworking();
            // ReSharper disable InconsistentlySynchronizedField
            PendingResults = [];
            using (var finishedGeneration = new MessageQueue<bool?>())
            {
                //execute the host model system
                Status = () => "Running Host Model System";
                HostModelSystem?.Start();
                Status = () => "Distributing Tasks: Generation " + ( CurrentIteration + 1 ) + " / " + TotalIterations;
                    Task processResults = Task.Factory.StartNew( () =>
                    {
                        foreach (var result in PendingResults.GetConsumingEnumerable())
                        {
                            // only process things from the current generation
                            // ReSharper disable once AccessToModifiedClosure
                            if (generation == result.Generation)
                            {
                                lock (CurrentJobs)
                                {
                                    var currentJob = CurrentJobs[result.ProcessedIndex];
                                    currentJob.Value = result.ProcessedValue;
                                    currentJob.Processed = true;
                                    if(IsInvalidFitnessValue(currentJob))
                                    {
                                        if (FailOnInvalidFitness)
                                        {
                                            Console.WriteLine("Invalid fitness value detected.  Terminating estimation.");
                                            Exit = true;
                                        }
                                    }
                                    // store the result before starting the next generation
                                    // so the AI can play with the values after we write
                                    StoreResult(currentJob);
                                }
                                Progress += 1.0f / (TotalIterations * CurrentJobs.Count);
                                //scan the rest of the jobs to see if they have been processed
                                // ReSharper disable once AccessToDisposedClosure (Task Finishes before closer ends)
                                finishedGeneration.Add(CheckForAllDone());
                            }
                        }
                    } );

                for ( ; generation < TotalIterations & Exit == false; generation++ )
                {
                    CurrentIteration = generation;
                    CurrentJobs = AI.CreateJobsForIteration();
                    System.Threading.Thread.MemoryBarrier();
                    StartGeneration();
                    bool? done;
                    while ( Exit == false && ( done = finishedGeneration.GetMessageOrTimeout( 100 ) ) != true )
                    {
                        if ( done == null && Host.ConnectedClients.Count > 0 )
                        {
                        }
                        System.Threading.Thread.MemoryBarrier();
                    }
                    AI.IterationComplete();
                    // make sure to clear this to make sure we exit fine
                    System.Threading.Thread.MemoryBarrier();
                }
                PendingResults.CompleteAdding();
                processResults.Wait();
            }
            if ( ResultFileWriter != null )
            {
                ResultFileWriter.Close();
            }
            lock (Host)
            {
                foreach ( var client in Host.ConnectedClients )
                {
                    client.SendCancel( "End of model run" );
                }
            }
            Host.Shutdown();
        }

        private static bool IsInvalidFitnessValue(Job currentJob)
        {
            return float.IsNaN(currentJob.Value) | float.IsInfinity(currentJob.Value);
        }

        public override string ToString()
        {
            return Status();
        }

        private bool CheckForAllDone()
        {
            for ( int i = 0; i < CurrentJobs.Count; i++ )
            {
                if ( !CurrentJobs[i].Processed )
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
            ParameterLoader.LoadData();
            Parameters = ParameterLoader.GiveData();
            ParameterLoader.UnloadData();
        }

        private void SendNewJob(IRemoteXTMF client)
        {
            System.Threading.Thread.MemoryBarrier();
            int i = IndexOfNextJob();
            if ( i >= 0 )
            {
                CurrentJobs[i].Processing = true;
                CurrentJobs[i].ProcessedBy = client;
                System.Threading.Thread.MemoryBarrier();
                client.SendCustomMessage( i, RequestJobChannel );
            }
        }

        private void SetupNetworking()
        {
            Host.RegisterCustomReceiver( ResultChannel, (stream, client) =>
                {
                    lock (this)
                    {
                        BinaryReader reader = new BinaryReader( stream );
                        PendingResults.Add( new ResultMessage()
                        {
                            Generation = reader.ReadInt32(),
                            ProcessedIndex = reader.ReadInt32(),
                            ProcessedValue = reader.ReadSingle()
                        } );
                    }
                    return null;
                } );
            Host.RegisterCustomReceiver( RequestJobChannel, (stream, client) =>
                {
                    lock (Host)
                    {
                        lock (CurrentJobs)
                        {
                            SendNewJob( client );
                        }
                    }
                    return null;
                } );
            Host.RegisterCustomSender( RequestJobChannel, (data, client, stream) =>
                {
                    lock (this)
                    {
                        var index = (int)data;
                        var job = CurrentJobs[index];
                        BinaryWriter writer = new BinaryWriter( stream );
                        writer.Write( CurrentIteration );
                        writer.Write( index );
                        writer.Write( job.Parameters.Length );
                        for ( int i = 0; i < job.Parameters.Length; i++ )
                        {
                            writer.Write( job.Parameters[i].Current );
                        }
                        writer.Flush();
                    }
                } );
            Host.RegisterCustomReceiver( SendParameterDefinitions, (stream, client) =>
                {
                    lock (Host)
                    {
                        client.SendCustomMessage( null, SendParameterDefinitions );
                        if ( !AvailableClients.Contains( client ) )
                        {
                            AvailableClients.Add( client );
                        }
                    }
                    return null;
                } );
            Host.RegisterCustomSender( SendParameterDefinitions, (data, client, stream) =>
                {
                    BinaryWriter writer = new BinaryWriter( stream );
                    writer.Write( Parameters.Count );
                    for ( int i = 0; i < Parameters.Count; i++ )
                    {
                        writer.Write( Parameters[i].Names.Length );
                        for ( int j = 0; j < Parameters[i].Names.Length; j++ )
                        {
                            writer.Write( Parameters[i].Names[j] );
                        }
                    }
                    writer.Flush();
                } );
            Host.NewClientConnected += (client) =>
                {
                    lock (Host)
                    {
                        client.SendModelSystem( ClientStructure );
                        XtmfConfig.CreateProgressReport( client.UniqueID + " " + client.MachineName,
                            () => client.Progress, new Tuple<byte, byte, byte>( 50, 50, 150 ) );
                    }
                };
            Host.ClientDisconnected += (client) =>
                {
                    lock (Host)
                    {
                        AvailableClients.Remove( client );
                        foreach ( var job in CurrentJobs )
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
                        XtmfConfig.DeleteProgressReport( client.UniqueID + " " + client.MachineName );
                    }
                };
        }

        private void StartGeneration()
        {
            // send each client a job to start with
            lock (Host)
            {
                lock (CurrentJobs)
                {
                    foreach ( var client in AvailableClients )
                    {
                        SendNewJob( client );
                    }
                }
            }
        }

        private void StoreResult(Job currentJob)
        {
            StringBuilder toWrite = new StringBuilder();
            toWrite.Append( CurrentIteration );
            toWrite.Append( ',' );
            toWrite.Append( currentJob.Value );
            for ( int i = 0; i < currentJob.Parameters.Length; i++ )
            {
                for ( int j = 0; j < Parameters[i].Names.Length; j++ )
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
                    if ( HoldOnToResultFile )
                    {
                        if ( ResultFileWriter == null )
                        {
                            ResultFileWriter = new StreamWriter( ResultFile.GetFilePath() );
                        }
                        Write( currentJob, toWrite, ResultFileWriter );
                    }
                    else
                    {
                        using var writer = new StreamWriter(ResultFile.GetFilePath(), true);
                        Write(currentJob, toWrite, writer);
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
            if ( FirstLineToWrite )
            {
                // write header here
                StringBuilder header = new StringBuilder();
                header.Append( "Generation,Value" );
                for ( int i = 0; i < currentJob.Parameters.Length; i++ )
                {
                    for ( int j = 0; j < Parameters[i].Names.Length; j++ )
                    {
                        header.Append( ',' );
                        header.Append('"');
                        header.Append( Parameters[i].Names[j] );
                        header.Append('"');
                    }
                }
                writer.WriteLine( header.ToString() );
                FirstLineToWrite = false;
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
            if ( ResultFileWriter != null )
            {
                ResultFileWriter.Dispose();
                ResultFileWriter = null;
            }
            if ( PendingResults != null )
            {
                PendingResults.Dispose();
                PendingResults = null;
            }
        }
    }
}