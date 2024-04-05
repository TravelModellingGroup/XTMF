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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using XTMF;
using XTMF.Networking;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace TMG.NetworkEstimation
{
    public class GeneticNetworkEstimationHost : I4StepModel, IDisposable
    {
        [RunParameter("Cross Exponent", 2.2f, "The exponent used for selecting the parameters to breed.")]
        public float CrossExponent;

        [RunParameter("EvaluationFile", "Evaluation.csv", "The file that we store the evaluation in.")]
        public string EvaluationFile;

        public IHost Host;

        [RunParameter("Max Mutation", 0.4f, "The maximum amount  (in 0 to 1) that a parameter can be mutated")]
        public float MaxMutationPercent;

        [RunParameter("Mutation Exponent", 2f, "The exponent used for mutation")]
        public float MutationExponent;

        [RunParameter("Mutation Probability", 3.1f, "The number of mutations per person.  The remainder will be applied with a probability.")]
        public float MutationProbability;

        [RunParameter("Network Base Directory", @"D:\EMMENetworks\Test_Transit", "The original data bank's base directory")]
        public string NetworkBaseDirectory;

        [RunParameter("Parameter Instructions", "../../Input/ParameterInstructions.xml", "Describes which and how the parameters will be estimated.")]
        public string ParameterInstructions;

        [RunParameter("Population Size", 100, "The total population to be calculated.")]
        public int PopulationSize;

        [RunParameter("ModelSystemName", "Genetic Network Estimation", "The name of the model system that will be deployed to the remote machines.")]
        public string RemoteModelSystemName;

        [RunParameter("Reseed", 10, "The number of units in the population that will be reseeded with completely different values each generation.")]
        public int Reseed;

        protected ParameterSetting[] Parameters;

        protected ParameterSet[] Population;

        private static Tuple<byte, byte, byte> _ProgressColour = new(50, 150, 50);

        // We can start at 0 since we increment no matter what, to get the first one
        private int CurrentHighestNumber;

        private bool Exit;

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

        [RunParameter("Generations", 50, "The total number of generations to run.")]
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
            Exit = true;
            lock (Host)
            {
                // if we actually got a message then we are ready to start fireing off tasks
                var exceptions = new List<Exception>();
                foreach (var client in Host.ConnectedClients)
                {
                    try
                    {
                        client.SendCancel("Host Exiting");
                    }
                    catch (Exception e)
                    {
                        exceptions.Add(e);
                    }
                }
                if (exceptions.Any())
                {
                    throw exceptions.First();
                }
            }
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            if (!File.Exists(ParameterInstructions))
            {
                error = "The file \"" + ParameterInstructions + "\" was not found!";
                return false;
            }
            ModelSystemToExecute = Host.CreateModelSystem(RemoteModelSystemName, ref error);
            if (error != null)
            {
                return false;
            }
            if (ModelSystemToExecute == null)
            {
                error = "We were unable to find/create the remote model system!";
                return false;
            }
            if (Reseed > PopulationSize)
            {
                error = "You can not reseed more than the size of the population!";
                return false;
            }
            return true;
        }

        public void Start()
        {
            using (ResultQueue = new MessageQueue<ResultMessage>())
            {
                InitializeHost();
                RandomGenerator = new Random();
                LoadInstructions();
                GenerateInitialPopulation();
                CreateDistributionThread();
                for (int generation = 0; generation < TotalIterations; generation++)
                {
                    // now that we have a population we need to go and send out the processing requests
                    int processed = 0;
                    CurrentIteration = generation;
                    StartGeneration.Add(new StartGenerationMessage());
                    var lastResultProcessed = DateTime.Now;
                    while (processed < PopulationSize)
                    {
                        var gatherResult = ResultQueue.GetMessageOrTimeout(200);
                        if (gatherResult != null)
                        {
                            lock (this)
                            {
                                lastResultProcessed = DateTime.Now;
                                int index = gatherResult.ProcessedIndex;
                                if (index != -1)
                                {
                                    Population[index].Value = gatherResult.ProcessedValue;
                                    Population[index].Processed = true;
                                    Save(index);
                                    processed++;
                                }
                                var generationProgressIncrement = 1f / TotalIterations;
                                Progress = (processed / (float)PopulationSize) * generationProgressIncrement
                                    + generation * generationProgressIncrement;
                                Thread.MemoryBarrier();
                            }
                        }
                        else if (Exit)
                        {
                            ExitRequest();
                            return;
                        }
                        else
                        {
                            var timeSinceLastUpdate = DateTime.Now - lastResultProcessed;
                            if (timeSinceLastUpdate.TotalMinutes > 2)
                            {
                                StartGeneration.Add(new StartGenerationMessage());
                            }
                        }
                    }
                    // clean out the buffer
                    while (StartGeneration.GetMessageOrTimeout(0) != null)
                    {
                    }
                    // now we can safely generate a new population
                    if (generation < TotalIterations - 1)
                    {
                        GenerateNextGeneration();
                    }
                }
            }
            ExitRequest();
        }

        public override string ToString()
        {
            return string.Concat("Generation: ", CurrentIteration, " of ", TotalIterations);
        }

        protected virtual void GenerateInitialPopulation()
        {
            var middle = (ParameterSetting[])Parameters.Clone();
            var numberOfParameters = middle.Length;
            // now that we have the middle point setup we can start to clone our new population
            var totalPopulation = PopulationSize;
            lock (Population)
            {
                var population = new ParameterSet[PopulationSize];
                for (int i = 0; i < totalPopulation; i++)
                {
                    // add in a population of mutents from the middle
                    population[i] = (new ParameterSet { Value = 0, Parameters = middle.Clone() as ParameterSetting[] });
                    for (int j = 0; j < numberOfParameters; j++)
                    {
                        population[i].Parameters[j].Current = ((Parameters[j].Stop - Parameters[j].Start) *
                                                               (float)RandomGenerator.NextDouble()) +
                                                              Parameters[j].Start;
                    }
                    population[i].ProcessedBy = null;
                    population[i].Processing = false;
                    population[i].Processed = false;
                    population[i].Value = float.NegativeInfinity;
                }
                Population = population;
            }
        }

        protected virtual void GenerateNextGeneration()
        {
            var oldPopulation = Population;
            Array.Sort(oldPopulation, new CompareParameterSet());
            lock (Population)
            {
                var newPopulation = new ParameterSet[PopulationSize];
                for (int i = 0; i < PopulationSize - Reseed; i++)
                {
                    int firstIndex = Select();
                    int secondIndex = Select();
                    if (secondIndex == firstIndex)
                    {
                        secondIndex++;
                    }
                    if (secondIndex >= PopulationSize)
                    {
                        secondIndex = 0;
                    }

                    newPopulation[i].Value = float.NegativeInfinity;
                    newPopulation[i].Processing = false;
                    newPopulation[i].ProcessedBy = null;
                    newPopulation[i].Processed = false;
                    newPopulation[i].Parameters =
                        Mutate(CrossGenes(oldPopulation[firstIndex].Parameters, oldPopulation[secondIndex].Parameters));
                }
                int numberOfParameters = Parameters.Length;
                for (int i = PopulationSize - Reseed; i < PopulationSize; i++)
                {
                    newPopulation[i].Parameters = oldPopulation[i].Parameters;
                    for (int j = 0; j < numberOfParameters; j++)
                    {
                        newPopulation[i].Parameters[j].Current = ((Parameters[j].Stop - Parameters[j].Start) *
                                                                  (float) RandomGenerator.NextDouble()) +
                                                                 Parameters[j].Start;
                    }
                }
                Population = newPopulation;
            }
        }

        private void ClientDisconnected(IRemoteXTMF obj)
        {
            // Recover the lost data by putting it back on the processing Queue
            lock (this)
            {
                bool othersStillProcessing = false;
                var populationLength = Population.Length;

                for (int i = 0; i < populationLength; i++)
                {
                    if (Population[i].ProcessedBy != obj && Population[i].Processing && Population[i].Processed == false)
                    {
                        othersStillProcessing = true;
                    }
                    if (Population[i].ProcessedBy == obj && Population[i].Processing && Population[i].Processed == false)
                    {
                        Population[i].ProcessedBy = null;
                        Population[i].Processing = false;
                    }
                }
                if (!othersStillProcessing)
                {
                    StartGeneration.Add(new StartGenerationMessage());
                }
            }
        }

        private void CreateDistributionThread()
        {
            StartGeneration = new MessageQueue<StartGenerationMessage>();
            Thread distributionThread = new(DistributionMain);
            distributionThread.IsBackground = true;
            distributionThread.Start();
        }

        private ParameterSetting[] CrossGenes(ParameterSetting[] baseSet, ParameterSetting[] spouse)
        {
            var length = baseSet.Length;
            var ret = (ParameterSetting[])baseSet.Clone();
            for (int i = 0; i < length; i++)
            {
                if (RandomGenerator.NextDouble() > 0.5)
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
            while (!Exit)
            {
                if (StartGeneration != null)
                {
                    var start = StartGeneration.GetMessageOrTimeout(200);
                    if (start != null)
                    {
                        lock (Host)
                        {
                            var exceptions = new List<Exception>();
                            // if we actually got a message then we are ready to start fireing off tasks
                            foreach (var client in Host.ConnectedClients)
                            {
                                try
                                {
                                    if (client.Connected)
                                    {
                                        SendNextParameter(client);
                                    }
                                }
                                catch(Exception e)
                                {
                                    exceptions.Add(e);
                                }
                            }
                            if (exceptions.Any())
                            {
                                throw exceptions.First();
                            }
                        }
                    }
                }
                else
                {
                    Thread.Sleep(200);
                }
                Thread.MemoryBarrier();
            }
            StartGeneration?.Dispose();
        }

        private void EditModelSystemTemplate(IModelSystemStructure msscopy, int i)
        {
            IModuleParameters parameterList;
            // First edit our children
            foreach (var child in msscopy.Children)
            {
                if (child.ParentFieldType.Name == "INetworkAssignment")
                {
                    parameterList = child.Parameters;
                    foreach (var param in parameterList.Parameters)
                    {
                        if (param.Name == "Emme Project Folder")
                        {
                            param.Value = $"{NetworkBaseDirectory}-{i}/Database";
                        }
                    }
                }
            }
            // after they are setup we just need to tune a couple of our parameters
            parameterList = msscopy.Parameters;
            foreach (var param in parameterList.Parameters)
            {
                if (param.Name == "Emme Input Output")
                {
                    param.Value = $"{NetworkBaseDirectory}-{i}/Database/cache/scalars.311";
                }
                else if (param.Name == "Emme Macro Output")
                {
                    param.Value = $"{NetworkBaseDirectory}-{i}/Database/cache/boardings_predicted.621";
                }
            }
        }

        private void FixParameters(IModelSystemStructure msCopy)
        {
            var ith = Interlocked.Increment(ref CurrentHighestNumber);
            EditModelSystemTemplate(msCopy, ith);
        }

        private void InitializeHost()
        {
            Host.NewClientConnected += NewClientConnected;
            Host.ClientDisconnected += ClientDisconnected;
            Host.RegisterCustomReceiver(0, ReadEvaluation);
            Host.RegisterCustomMessageHandler(0, ProcessEvaluation);
            Host.RegisterCustomSender(1, SendParametersToEvaluate);
        }

        private void LoadInstructions()
        {
            XmlDocument doc = new();
            doc.Load(ParameterInstructions);
            List<ParameterSetting> parameters = [];
            var childNodes = doc["Root"]?.ChildNodes;
            if (childNodes != null)
            {
                foreach (XmlNode child in childNodes)
                {
                    if (child.Name == "Parameter")
                    {
                        ParameterSetting current = new();
                        var attributes = child.Attributes;
                        if (attributes != null)
                        {
                            current.ParameterName = attributes["Name"].InnerText;
                            current.MsNumber = int.Parse(attributes["MS"].InnerText);
                            current.Start = float.Parse(attributes["Start"].InnerText);
                            current.Stop = float.Parse(attributes["Stop"].InnerText);
                            current.Current = current.Start;
                            parameters.Add(current);
                        }
                    }
                }
            }
            Parameters = parameters.ToArray();
        }

        private ParameterSetting[] Mutate(ParameterSetting[] original)
        {
            var ret = (ParameterSetting[])original.Clone();
            var numberOfParameters = ret.Length;
            // see if we will have an addition mutation randomly
            int numberOfMutations = (MutationProbability - (int)MutationProbability) > RandomGenerator.NextDouble() ? (int)MutationProbability + 1 : (int)MutationProbability;
            for (int i = 0; i < numberOfMutations; i++)
            {
                int index = (int)(RandomGenerator.NextDouble() * numberOfParameters);
                var space = ret[index].Stop - ret[index].Start;
                var mutation = (float)(Math.Pow(RandomGenerator.NextDouble(), MutationExponent) * (space * MaxMutationPercent));
                ret[index].Current += (RandomGenerator.NextDouble() > 0.5 ? mutation : -mutation);
                if (ret[index].Current < ret[index].Start)
                {
                    ret[index].Current = ret[index].Start;
                }
                else if (ret[index].Current > ret[index].Stop)
                {
                    ret[index].Current = ret[index].Stop;
                }
            }
            return ret;
        }

        private void NewClientConnected(IRemoteXTMF obj)
        {
            // Setup a new model system for them to execute
            var msCopy = ModelSystemToExecute.Clone();
            FixParameters(msCopy);
            Host.ExecuteModelSystemAsync(msCopy);
        }

        private void ProcessEvaluation(object evaluation, IRemoteXTMF r)
        {
            if (evaluation is ResultMessage message)
            {
                ResultQueue.Add(message);
            }
        }

        /// <summary>
        /// Process the custom message
        /// </summary>
        /// <param name="memory"></param>
        /// <param name="remote"></param>
        /// <returns></returns>
        private object ReadEvaluation(Stream memory, IRemoteXTMF remote)
        {
            BinaryReader reader = new(memory);
            ResultMessage ret = new();
            var generation = reader.ReadInt32();
            ret.ProcessedIndex = reader.ReadInt32();
            if (ret.ProcessedIndex == -1)
            {
                SendNextParameter(remote);
                return null;
            }
            ret.ProcessedValue = reader.ReadSingle();
            SendNextParameter(remote);
            return CurrentIteration == generation ? ret : null;
        }

        private void Save(int index)
        {
            var run = Population[index];
            var length = run.Parameters.Length;
            lock (this)
            {
                bool writeHeader = !File.Exists(EvaluationFile);
                while (true)
                {
                    try
                    {
                        using StreamWriter writer = new(EvaluationFile, true);
                        if (writeHeader)
                        {
                            writer.Write("Generation");
                            writer.Write(',');
                            writer.Write("Client");
                            writer.Write(',');
                            writer.Write("Value");
                            for (int i = 0; i < length; i++)
                            {
                                writer.Write(',');
                                writer.Write(run.Parameters[i].ParameterName);
                            }
                            writer.WriteLine();
                        }
                        writer.Write(CurrentIteration);
                        writer.Write(',');
                        writer.Write(Population[index].ProcessedBy.UniqueID);
                        writer.Write(',');
                        writer.Write(run.Value);
                        for (int i = 0; i < length; i++)
                        {
                            writer.Write(',');
                            writer.Write(run.Parameters[i].Current);
                        }
                        writer.WriteLine();
                        break;
                    }
                    catch
                    {
                        Thread.Sleep(10);
                    }
                }
            }
        }

        private int Select()
        {
            return (int)(Math.Pow(RandomGenerator.NextDouble(), CrossExponent) * PopulationSize);
        }

        private void SendNextParameter(IRemoteXTMF client)
        {
            ResultMessage msg = new();
            lock (this)
            {
                // make sure we have the newest memory loaded up
                Thread.MemoryBarrier();
                var populationLength = Population.Length;
                for (int i = 0; i < populationLength; i++)
                {
                    if (Population[i].Processing == false)
                    {
                        Population[i].ProcessedBy = client;
                        Population[i].Processing = true;
                        msg.ProcessedIndex = i;
                        client.SendCustomMessage(msg, 1);
                        break;
                    }
                }
                // make sure changes have gone through
                Thread.MemoryBarrier();
            }
        }

        ///  <summary>
        ///  </summary>
        ///  <param name="o"></param>
        /// <param name="remote"></param>
        /// <param name="s"></param>
        private void SendParametersToEvaluate(object o, IRemoteXTMF remote, Stream s)
        {
            lock (this)
            {
                var message = (ResultMessage)o;
                var index = message.ProcessedIndex;
                var toProcess = Population[index];
                var length = toProcess.Parameters.Length;
                BinaryWriter writer = new(s);
                writer.Write(CurrentIteration);
                writer.Write(index);
                writer.Write(length);
                for (int i = 0; i < length; i++)
                {
                    writer.Write(toProcess.Parameters[i].Current);
                }
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
                return Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        protected class CompareParameterSet : IComparer<ParameterSet>
        {
            public int Compare(ParameterSet x, ParameterSet y)
            {
                if (x.Value < y.Value) return -1;
                if (x.Value == y.Value)
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
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool managedOnly)
        {
            if (StartGeneration != null)
            {
                StartGeneration.Dispose();
                StartGeneration = null;
            }
        }
    }
}