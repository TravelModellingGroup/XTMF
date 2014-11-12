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
using System.IO;
using System.Threading;
using System.Xml;
using XTMF;
using XTMF.Networking;

namespace TMG.NetworkEstimation
{
    public class GeneticNetworkEstimationHost : I4StepModel, IDisposable
    {
        [RunParameter( "Cross Exponent", 2.2f, "The exponent used for selecting the parameters to breed." )]
        public float CrossExponent;

        [RunParameter( "EvaluationFile", "Evaluation.csv", "The file that we store the evaluation in." )]
        public string EvaluationFile;

        public IHost Host;

        [RunParameter( "Max Mutation", 0.4f, "The maximum amount  (in 0 to 1) that a parameter can be mutated" )]
        public float MaxMutationPercent;

        [RunParameter( "Mutation Exponent", 2f, "The exponent used for mutation" )]
        public float MutationExponent;

        [RunParameter( "Mutation Probability", 3.1f, "The number of mutations per person.  The remainder will be applied with a probability." )]
        public float MutationProbability;

        [RunParameter( "Network Base Directory", @"D:\EMMENetworks\Test_Transit", "The original data bank's base directory" )]
        public string NetworkBaseDirectory;

        [RunParameter( "Parameter Instructions", "../../Input/ParameterInstructions.xml", "Describes which and how the parameters will be estimated." )]
        public string ParameterInstructions;

        [RunParameter( "Population Size", 100, "The total population to be calculated." )]
        public int PopulationSize;

        [RunParameter( "ModelSystemName", "Genetic Network Estimation", "The name of the model system that will be deployed to the remote machines." )]
        public string RemoteModelSystemName;

        [RunParameter( "Reseed", 10, "The number of units in the population that will be reseeded with completely different values each generation." )]
        public int Reseed;

        protected ParameterSetting[] Parameters;

        protected ParameterSet[] Population;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>( 50, 150, 50 );

        // We can start at 0 since we increment no matter what, to get the first one
        private int CurrentHighestNumber = 0;

        private bool Exit = false;

        private IModelSystemStructure ModelSystemToExecute;

        private Random RandomGenerator;

        private MessageQueue<ResultMessage> ResultQueue;

        private MessageQueue<StartGenerationMessage> StartGeneration;

        public int CurrentIteration
        {
            get;
            set;
        }

        public string InputBaseDirectory
        {
            get;
            set;
        }

        [DoNotAutomate]
        public List<IModeChoiceNode> Modes
        {
            get { return null; }
        }

        public string Name
        {
            get;
            set;
        }

        [DoNotAutomate]
        public INetworkAssignment NetworkAssignment
        {
            get;
            set;
        }

        [DoNotAutomate]
        public IList<INetworkData> NetworkData { get { return null; } }

        public string OutputBaseDirectory
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return _ProgressColour; }
        }

        [DoNotAutomate]
        public List<IPurpose> Purpose
        {
            get;
            set;
        }

        [RunParameter( "Generations", 50, "The total number of generations to run." )]
        public int TotalIterations
        {
            get;
            set;
        }

        [DoNotAutomate]
        public IZoneSystem ZoneSystem
        {
            get { return null; }
        }

        public bool ExitRequest()
        {
            this.Exit = true;
            lock ( this.Host )
            {
                // if we actually got a message then we are ready to start fireing off tasks
                foreach ( var client in this.Host.ConnectedClients )
                {
                    try
                    {
                        client.SendCancel( "Host Exiting" );
                    }
                    catch
                    {
                    }
                }
            }
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( !File.Exists( this.ParameterInstructions ) )
            {
                error = "The file \"" + this.ParameterInstructions + "\" was not found!";
                return false;
            }
            this.ModelSystemToExecute = this.Host.CreateModelSystem( this.RemoteModelSystemName, ref error );
            if ( error != null )
            {
                return false;
            }
            if ( this.ModelSystemToExecute == null )
            {
                error = "We were unable to find/create the remote model system!";
                return false;
            }
            if ( this.Reseed > this.PopulationSize )
            {
                error = "You can not reseed more than the size of the population!";
                return false;
            }
            return true;
        }

        public void Start()
        {
            using ( this.ResultQueue = new MessageQueue<ResultMessage>() )
            {
                this.InitializeHost();
                this.RandomGenerator = new Random();
                this.LoadInstructions();
                this.GenerateInitialPopulation();
                this.CreateDistributionThread();
                for ( int generation = 0; generation < this.TotalIterations; generation++ )
                {
                    // now that we have a population we need to go and send out the processing requests
                    int processed = 0;
                    this.CurrentIteration = generation;
                    this.StartGeneration.Add( new StartGenerationMessage() );
                    var lastResultProcessed = DateTime.Now;
                    while ( processed < this.PopulationSize )
                    {
                        var gatherResult = this.ResultQueue.GetMessageOrTimeout( 200 );
                        if ( gatherResult != null )
                        {
                            lock ( this )
                            {
                                lastResultProcessed = DateTime.Now;
                                int index = gatherResult.ProcessedIndex;
                                if ( index != -1 )
                                {
                                    this.Population[index].Value = gatherResult.ProcessedValue;
                                    this.Population[index].Processed = true;
                                    this.Save( index );
                                    processed++;
                                }
                                var generationProgressIncrement = 1f / this.TotalIterations;
                                this.Progress = ( processed / (float)this.PopulationSize ) * generationProgressIncrement
                                    + generation * generationProgressIncrement;
                                Thread.MemoryBarrier();
                            }
                        }
                        else if ( this.Exit )
                        {
                            this.ExitRequest();
                            return;
                        }
                        else
                        {
                            var timeSinceLastUpdate = DateTime.Now - lastResultProcessed;
                            if ( timeSinceLastUpdate.TotalMinutes > 2 )
                            {
                                this.StartGeneration.Add( new StartGenerationMessage() );
                            }
                        }
                    }
                    // clean out the buffer
                    while ( this.StartGeneration.GetMessageOrTimeout( 0 ) != null ) ;
                    // now we can safely generate a new population
                    if ( generation < this.TotalIterations - 1 )
                    {
                        this.GenerateNextGeneration();
                    }
                }
            }
            this.ExitRequest();
        }

        public override string ToString()
        {
            return String.Concat( "Generation: ", this.CurrentIteration, " of ", this.TotalIterations );
        }

        protected virtual void GenerateInitialPopulation()
        {
            var middle = this.Parameters.Clone() as ParameterSetting[];
            var numberOfParameters = middle.Length;
            // now that we have the middle point setup we can start to clone our new population
            var totalPopulation = this.PopulationSize;
            this.Population = new ParameterSet[this.PopulationSize];
            for ( int i = 0; i < totalPopulation; i++ )
            {
                // add in a population of mutents from the middle
                this.Population[i] = ( new ParameterSet() { Value = 0, Parameters = middle.Clone() as ParameterSetting[] } );
                for ( int j = 0; j < numberOfParameters; j++ )
                {
                    this.Population[i].Parameters[j].Current = ( ( this.Parameters[j].Stop - this.Parameters[j].Start ) * (float)this.RandomGenerator.NextDouble() ) + this.Parameters[j].Start;
                }
                this.Population[i].ProcessedBy = null;
                this.Population[i].Processing = false;
                this.Population[i].Processed = false;
                this.Population[i].Value = float.NegativeInfinity;
            }
        }

        protected virtual void GenerateNextGeneration()
        {
            var oldPopulation = this.Population;
            Array.Sort( oldPopulation, new CompareParameterSet() );
            var newPopulation = new ParameterSet[this.PopulationSize];
            for ( int i = 0; i < this.PopulationSize - this.Reseed; i++ )
            {
                int firstIndex = Select();
                int secondIndex = Select();
                if ( secondIndex == firstIndex )
                {
                    secondIndex++;
                }
                if ( secondIndex >= this.PopulationSize )
                {
                    secondIndex = 0;
                }

                newPopulation[i].Value = float.NegativeInfinity;
                newPopulation[i].Processing = false;
                newPopulation[i].ProcessedBy = null;
                newPopulation[i].Processed = false;
                newPopulation[i].Parameters = this.Mutate( this.CrossGenes( oldPopulation[firstIndex].Parameters, oldPopulation[secondIndex].Parameters ) );
            }
            int numberOfParameters = this.Parameters.Length;
            for ( int i = this.PopulationSize - this.Reseed; i < this.PopulationSize; i++ )
            {
                newPopulation[i].Parameters = oldPopulation[i].Parameters;
                for ( int j = 0; j < numberOfParameters; j++ )
                {
                    newPopulation[i].Parameters[j].Current = ( ( this.Parameters[j].Stop - this.Parameters[j].Start ) * (float)this.RandomGenerator.NextDouble() ) + this.Parameters[j].Start;
                }
            }
            this.Population = newPopulation;
        }

        private void ClientDisconnected(IRemoteXTMF obj)
        {
            // Recover the lost data by putting it back on the processing Queue
            lock ( this )
            {
                bool othersStillProcessing = false;
                var populationLength = this.Population.Length;

                for ( int i = 0; i < populationLength; i++ )
                {
                    if ( this.Population[i].ProcessedBy != obj && this.Population[i].Processing == true && this.Population[i].Processed == false )
                    {
                        othersStillProcessing = true;
                    }
                    if ( this.Population[i].ProcessedBy == obj && this.Population[i].Processing == true && this.Population[i].Processed == false )
                    {
                        this.Population[i].ProcessedBy = null;
                        this.Population[i].Processing = false;
                    }
                }
                if ( !othersStillProcessing )
                {
                    this.StartGeneration.Add( new StartGenerationMessage() );
                }
            }
        }

        private void CreateDistributionThread()
        {
            this.StartGeneration = new MessageQueue<StartGenerationMessage>();
            Thread distributionThread = new Thread( DistributionMain );
            distributionThread.IsBackground = true;
            distributionThread.Start();
        }

        private ParameterSetting[] CrossGenes(ParameterSetting[] baseSet, ParameterSetting[] spouse)
        {
            var length = baseSet.Length;
            var ret = baseSet.Clone() as ParameterSetting[];
            for ( int i = 0; i < length; i++ )
            {
                if ( this.RandomGenerator.NextDouble() > 0.5 )
                {
                    ret[i] = baseSet[i];
                }
                else
                {
                    ret[i] = spouse[i];
                }
            }
            return ret;
        }

        private void DistributionMain()
        {
            while ( !this.Exit )
            {
                if ( this.StartGeneration != null )
                {
                    var start = this.StartGeneration.GetMessageOrTimeout( 200 );
                    if ( start != null )
                    {
                        lock ( this.Host )
                        {
                            // if we actually got a message then we are ready to start fireing off tasks
                            foreach ( var client in this.Host.ConnectedClients )
                            {
                                try
                                {
                                    if ( client.Connected )
                                    {
                                        this.SendNextParameter( client );
                                    }
                                }
                                catch
                                {
                                }
                            }
                        }
                    }
                }
                else
                {
                    Thread.Sleep( 200 );
                }
                Thread.MemoryBarrier();
            }
            if ( this.StartGeneration != null )
            {
                this.StartGeneration.Dispose();
            }
        }

        private void EditModelSystemTemplate(IModelSystemStructure msscopy, int i)
        {
            IModuleParameters parameterList;
            // First edit our children
            foreach ( var child in msscopy.Children )
            {
                if ( child.ParentFieldType.Name == "INetworkAssignment" )
                {
                    parameterList = child.Parameters;
                    foreach ( var param in parameterList.Parameters )
                    {
                        if ( param.Name == "Emme Project Folder" )
                        {
                            param.Value = String.Format( "{0}-{1}/Database", this.NetworkBaseDirectory, i );
                        }
                    }
                }
            }
            // after they are setup we just need to tune a couple of our parameters
            parameterList = msscopy.Parameters;
            foreach ( var param in parameterList.Parameters )
            {
                if ( param.Name == "Emme Input Output" )
                {
                    param.Value = String.Format( "{0}-{1}/Database/cache/scalars.311", this.NetworkBaseDirectory, i );
                }
                else if ( param.Name == "Emme Macro Output" )
                {
                    param.Value = String.Format( "{0}-{1}/Database/cache/boardings_predicted.621", this.NetworkBaseDirectory, i );
                }
            }
        }

        private void FixParameters(IModelSystemStructure msCopy)
        {
            var ith = Interlocked.Increment( ref CurrentHighestNumber );
            this.EditModelSystemTemplate( msCopy, ith );
        }

        private void InitializeHost()
        {
            this.Host.NewClientConnected += new Action<IRemoteXTMF>( NewClientConnected );
            this.Host.ClientDisconnected += new Action<IRemoteXTMF>( ClientDisconnected );
            this.Host.RegisterCustomReceiver( 0, this.ReadEvaluation );
            this.Host.RegisterCustomMessageHandler( 0, this.ProcessEvaluation );
            this.Host.RegisterCustomSender( 1, SendParametersToEvaluate );
        }

        private void LoadInstructions()
        {
            XmlDocument doc = new XmlDocument();
            doc.Load( this.ParameterInstructions );
            List<ParameterSetting> parameters = new List<ParameterSetting>();
            foreach ( XmlNode child in doc["Root"].ChildNodes )
            {
                if ( child.Name == "Parameter" )
                {
                    ParameterSetting current = new ParameterSetting();
                    current.ParameterName = child.Attributes["Name"].InnerText;
                    current.MSNumber = int.Parse( child.Attributes["MS"].InnerText );
                    current.Start = float.Parse( child.Attributes["Start"].InnerText );
                    current.Stop = float.Parse( child.Attributes["Stop"].InnerText );
                    current.Current = current.Start;
                    parameters.Add( current );
                }
            }
            this.Parameters = parameters.ToArray();
        }

        private ParameterSetting[] Mutate(ParameterSetting[] original)
        {
            var ret = original.Clone() as ParameterSetting[];
            var numberOfParameters = ret.Length;
            // see if we will have an addition mutation randomly
            int numberOfMutations = ( this.MutationProbability - (int)this.MutationProbability ) > this.RandomGenerator.NextDouble() ? (int)this.MutationProbability + 1 : (int)this.MutationProbability;
            for ( int i = 0; i < numberOfMutations; i++ )
            {
                int index = (int)( RandomGenerator.NextDouble() * numberOfParameters );
                var space = ret[index].Stop - ret[index].Start;
                var mutation = (float)( Math.Pow( this.RandomGenerator.NextDouble(), this.MutationExponent ) * ( space * this.MaxMutationPercent ) );
                ret[index].Current += ( this.RandomGenerator.NextDouble() > 0.5 ? mutation : -mutation );
                if ( ret[index].Current < ret[index].Start )
                {
                    ret[index].Current = ret[index].Start;
                }
                else if ( ret[index].Current > ret[index].Stop )
                {
                    ret[index].Current = ret[index].Stop;
                }
            }
            return ret;
        }

        private void NewClientConnected(IRemoteXTMF obj)
        {
            // Setup a new model system for them to execute
            var msCopy = this.ModelSystemToExecute.Clone() as IModelSystemStructure;
            this.FixParameters( msCopy );
            this.Host.ExecuteModelSystemAsync( msCopy );
        }

        private void ProcessEvaluation(object evaluation, IRemoteXTMF r)
        {
            var message = evaluation as ResultMessage;
            if ( message != null )
            {
                this.ResultQueue.Add( message );
            }
        }

        /// <summary>
        /// Process the custom message
        /// </summary>
        /// <param name="memory"></param>
        /// <returns></returns>
        private object ReadEvaluation(Stream memory, IRemoteXTMF r)
        {
            BinaryReader reader = new BinaryReader( memory );
            ResultMessage ret = new ResultMessage();
            var generation = reader.ReadInt32();
            ret.ProcessedIndex = reader.ReadInt32();
            if ( ret.ProcessedIndex == -1 )
            {
                reader = null;
                this.SendNextParameter( r );
                return null;
            }
            ret.ProcessedValue = reader.ReadSingle();
            reader = null;
            this.SendNextParameter( r );
            return this.CurrentIteration == generation ? ret : null;
        }

        private void Save(int index)
        {
            var run = this.Population[index];
            var length = run.Parameters.Length;
            lock ( this )
            {
                bool writeHeader = !File.Exists( this.EvaluationFile );
                while ( true )
                {
                    try
                    {
                        using ( StreamWriter writer = new StreamWriter( this.EvaluationFile, true ) )
                        {
                            if ( writeHeader )
                            {
                                writer.Write( "Generation" );
                                writer.Write( ',' );
                                writer.Write( "Client" );
                                writer.Write( ',' );
                                writer.Write( "Value" );
                                for ( int i = 0; i < length; i++ )
                                {
                                    writer.Write( ',' );
                                    writer.Write( run.Parameters[i].ParameterName );
                                }
                                writer.WriteLine();
                            }
                            writer.Write( this.CurrentIteration );
                            writer.Write( ',' );
                            writer.Write( this.Population[index].ProcessedBy.UniqueID );
                            writer.Write( ',' );
                            writer.Write( run.Value );
                            for ( int i = 0; i < length; i++ )
                            {
                                writer.Write( ',' );
                                writer.Write( run.Parameters[i].Current );
                            }
                            writer.WriteLine();
                        }
                        break;
                    }
                    catch
                    {
                        Thread.Sleep( 10 );
                    }
                }
            }
        }

        private int Select()
        {
            return (int)( Math.Pow( this.RandomGenerator.NextDouble(), this.CrossExponent ) * this.PopulationSize );
        }

        private void SendNextParameter(IRemoteXTMF client)
        {
            ResultMessage msg = new ResultMessage();
            lock ( this )
            {
                // make sure we have the newest memory loaded up
                Thread.MemoryBarrier();
                var populationLength = this.Population.Length;
                for ( int i = 0; i < populationLength; i++ )
                {
                    if ( this.Population[i].Processing == false )
                    {
                        this.Population[i].ProcessedBy = client;
                        this.Population[i].Processing = true;
                        msg.ProcessedIndex = i;
                        client.SendCustomMessage( msg, 1 );
                        break;
                    }
                }
                // make sure changes have gone through
                Thread.MemoryBarrier();
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="o"></param>
        /// <param name="s"></param>
        private void SendParametersToEvaluate(object o, IRemoteXTMF r, Stream s)
        {
            lock ( this )
            {
                var message = o as ResultMessage;
                var index = message.ProcessedIndex;
                var toProcess = this.Population[index];
                var length = toProcess.Parameters.Length;
                BinaryWriter writer = new BinaryWriter( s );
                writer.Write( this.CurrentIteration );
                writer.Write( index );
                writer.Write( length );
                for ( int i = 0; i < length; i++ )
                {
                    writer.Write( toProcess.Parameters[i].Current );
                }
                writer = null;
            }
        }

        protected struct ParameterSet
        {
            internal ParameterSetting[] Parameters;
            internal bool Processed;
            internal IRemoteXTMF ProcessedBy;
            internal bool Processing;
            internal float Value;

            public override string ToString()
            {
                return this.Value.ToString();
            }
        }

        protected class CompareParameterSet : IComparer<ParameterSet>
        {
            public int Compare(ParameterSet x, ParameterSet y)
            {
                if ( x.Value < y.Value ) return -1;
                if ( x.Value == y.Value )
                {
                    return 0;
                }
                return 1;
            }
        }

        private class ResultMessage
        {
            internal int ProcessedIndex;
            internal float ProcessedValue;
        }

        private class StartGenerationMessage
        {
        }

        public void Dispose()
        {
            this.Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose(bool managedOnly)
        {
            if ( this.StartGeneration != null )
            {
                this.StartGeneration.Dispose();
                this.StartGeneration = null;
            }
        }
    }
}