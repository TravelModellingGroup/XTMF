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
using Datastructure;
using Tasha.Common;
using TMG;
using TMG.Input;
using XTMF;
using XTMF.Networking;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace Tasha;

public class ModeChoiceEstimationHost : ITashaRuntime, IDisposable
{
    [RunParameter("Cross Exponent", 2.2f, "The exponent used for selecting the parameters to breed.")]
    public float CrossExponent;

    [RunParameter("EvaluationFile", "Evaluation.csv", "The file that we store the evaluation in.")]
    public string EvaluationFile;

    /// <summary>
    /// Our connection to the XTMF Networking Host
    /// </summary>
    public IHost Host;

    [RunParameter("Max Mutation", 0.4f, "The maximum amount (in 0 to 1) that a parameter can be mutated")]
    public float MaxMutationPercent;

    [RunParameter("Mutation Exponent", 2f, "The exponent used for mutation")]
    public float MutationExponent;

    [RunParameter("Mutation Probability", 3.1f, "The number of mutations per gene. The remainder will be applied with a probability.")]
    public float MutationProbability;

    [RunParameter("Observed Mode Attachment", "ObservedMode", "The name of the attachment string from the loader")]
    public string ObservedMode;

    [RunParameter("Parameter Instructions", "ParameterInstructions.xml", "Describes which and how the parameters will be estimated.")]
    public string ParameterInstructions;

    [RunParameter("Population Size", 500, "The total population to be calculated.")]
    public int PopulationSize;

    [RunParameter("Previous Evaluation File Name", "Evaluation.csv", typeof(FileFromInputDirectory), "The file to use to gather the starting population from if the 'Start From Previous Best' is turned on.  This path is relative to the input directory.")]
    public FileFromInputDirectory PreviousRunFileName;

    [RunParameter("ModelSystemName", "Genetic Network Estimation", "The name of the model system that will be deployed to the remote machines.")]
    public string RemoteModelSystemName;

    [RunParameter("Reseed", 10, "The number of units in the population that will be reseeded with completely different values each generation.")]
    public int Reseed;

    [RunParameter("Start From Previous Best", false, "Should we use the output of a previous run as the starting population?")]
    public bool StartFromPreviousBest;

    [RunParameter("Total Generations", 300, "How many generations should we process?")]
    public int TotalIterations { get; set; }

    [RunParameter("Validate Parameter Names", false, "Should we throw an error if one of the parameter names does not actually belong to a mode?")]
    public bool ValidateParameterNames;

    protected ParameterSet[] Population;

    protected Random RandomGenerator;

    private static Tuple<byte, byte, byte> _ProgressColour = new(50, 150, 50);

    private IConfiguration Configuration;

    private List<IRemoteXTMF> ConnectedClients = [];

    private int CurrentIteration;

    private volatile bool Exit;

    private byte[] HouseholdData;

    private IModelSystemStructure ModelSystemToExecute;

    private ParameterSetting[] Parameters;

    private MessageQueue<ResultMessage> ResultQueue;

    private MessageQueue<StartGenerationMessage> StartGeneration;

    public ModeChoiceEstimationHost(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    [SubModelInformation(Description = "A list of all of the modes that can be used.", Required = false)]
    public List<ITashaMode> AllModes
    {
        get;
        set;
    }

    [SubModelInformation(Description = "The loader of the household data", Required = true)]
    public IDataLoader<ITashaHousehold> HouseholdLoader
    {
        get;
        set;
    }

    [RunParameter("Input Directory", "../../TashaInput", "The directory that contains the input for Tasha")]
    public string InputBaseDirectory
    {
        get;
        set;
    }

    public string Name
    {
        get;
        set;
    }

    [SubModelInformation(Description = "The networks that will be used.", Required = false)]
    public IList<INetworkData> NetworkData
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

    [RunParameter("Random Seed", 12345, "A number to use as the seed for the random number generator.")]
    public int RandomSeed
    {
        get;
        set;
    }

    [SubModelInformation(Description = "The available resources for this model system.", Required = false)]
    public List<IResource> Resources { get; set; }

    [SubModelInformation(Description = "Vehicles other than Auto.", Required = false)]
    public List<IVehicleType> VehicleTypes
    {
        get;
        set;
    }

    [SubModelInformation(Description = "The zone system that we will be using", Required = true)]
    public IZoneSystem ZoneSystem
    {
        get;
        set;
    }

    public bool ExitRequest()
    {
        Exit = true;
        lock (Host)
        {
            // if we actually got a message then we are ready to start fireing off tasks
            foreach (var client in Host.ConnectedClients)
            {
                try
                {
                    client.SendCancel("Host Exiting");
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch
                {
                }
            }
        }
        return true;
    }

    public bool RuntimeValidation(ref string error)
    {
        if (AllModes == null || AllModes.Count == 0)
        {
            error = "Mode Choice Estimation requires the modes in order to function properly!";
            return false;
        }
        if (Host == null)
        {
            error = "We require an XTMF Networking host to operate, please use a newer version of XTMF.";
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
        CurrentIteration = 0;
        TotalIterations = -1;
        BuildHouseholdData();
        using (ResultQueue = new MessageQueue<ResultMessage>())
        {
            InitializeHost();
            RandomGenerator = new Random();
            LoadInstructions();
            GenerateInitialPopulation();
            CreateDistributionThread();
            for (int generation = CurrentIteration; generation < TotalIterations; generation++)
            {
                // now that we have a population we need to go and send out the processing requests
                TotalIterations = generation;
                int processed = 0;
                CurrentIteration = generation;
                var lastResultProcessed = DateTime.Now;
                while (processed < PopulationSize)
                {
                    var gatherResult = ResultQueue.GetMessageOrTimeout(200);
                    if (gatherResult != null)
                    {
                        lock (this)
                        {
                            Thread.MemoryBarrier();
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
                        if (timeSinceLastUpdate.TotalMinutes > 120)
                        {
                            StartGeneration.Add(new StartGenerationMessage());
                        }
                    }
                }
                lock (this)
                {
                    // clean out the buffer
                    while (StartGeneration.GetMessageOrTimeout(0) != null)
                    {
                    }
                    // now we can safely generate a new population
                    if (generation < TotalIterations - 1)
                    {
                        GenerateNextGeneration();
                        StartGeneration.Add(new StartGenerationMessage());
                    }
                }
            }
        }
        ExitRequest();
    }

    public override string ToString()
    {
        return String.Format("Generation {0} of {1}", TotalIterations, TotalIterations);
    }

    protected ParameterSetting[] CrossGenes(ParameterSetting[] baseSet, ParameterSetting[] spouse)
    {
        var ret = (ParameterSetting[])baseSet.Clone();
        for (int i = 0; i < baseSet.Length; i++)
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

    protected virtual void GenerateInitialPopulation()
    {
        Population = new ParameterSet[PopulationSize];
        if (StartFromPreviousBest)
        {
            LoadInitialPopulation();
        }
        else
        {
            CreateNewInitialPopulation();
        }
    }

    protected virtual void GenerateNextGeneration()
    {
        lock (this)
        {
            var oldPopulation = Population;
            Array.Sort(oldPopulation, new CompareParameterSet());
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
                newPopulation[i].Parameters = Mutate(CrossGenes(oldPopulation[firstIndex].Parameters, oldPopulation[secondIndex].Parameters));
            }
            int numberOfParameters = Parameters.Length;
            for (int i = PopulationSize - Reseed; i < PopulationSize; i++)
            {
                newPopulation[i].Parameters = oldPopulation[i].Parameters;
                for (int j = 0; j < numberOfParameters; j++)
                {
                    newPopulation[i].Parameters[j].Current = ((Parameters[j].Stop - Parameters[j].Start) * (float)RandomGenerator.NextDouble()) + Parameters[j].Start;
                }
            }
            Population = newPopulation;
        }
        GC.Collect();
    }

    protected ParameterSetting[] Mutate(ParameterSetting[] original)
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

    private void BuildHouseholdData()
    {
        // We actually don't need to startup the networks since we don't need their data.
        // We just need them so we know that the modes will be satisfied when running
        ZoneSystem.LoadData();
        HouseholdLoader.LoadData();
        var households = HouseholdLoader.ToArray();
        VehicleTypes.Add(AutoType);
        using MemoryStream mem = new();
        BinaryWriter writer = new(mem);
        writer.Write(households.Length);
        var numberOfVehicleTypes = VehicleTypes.Count;
        writer.Write(numberOfVehicleTypes);
        for (int i = 0; i < numberOfVehicleTypes; i++)
        {
            writer.Write(VehicleTypes[i].VehicleName);
        }
        foreach (var household in households)
        {
            // write out all of the household attributes
            writer.Write(household.HouseholdId);
            writer.Write(household.Persons.Length);
            for (int i = 0; i < numberOfVehicleTypes; i++)
            {
                writer.Write(household.Vehicles.Count((v) => v.VehicleType.VehicleName == VehicleTypes[i].VehicleName));
            }
            writer.Write(household.HomeZone.ZoneNumber);
            SendAttached(writer, household);
            foreach (var person in household.Persons)
            {
                // Send the person's information
                writer.Write(person.Age);
                writer.Write(person.Female);
                writer.Write((Int32)person.EmploymentStatus);
                writer.Write((Int32)person.Occupation);
                if (person.EmploymentZone == null)
                {
                    writer.Write(-1);
                }
                else
                {
                    writer.Write(person.EmploymentZone.ZoneNumber);
                }
                writer.Write((Int32)person.StudentStatus);
                if (person.SchoolZone == null)
                {
                    writer.Write(-1);
                }
                else
                {
                    writer.Write(person.SchoolZone.ZoneNumber);
                }
                writer.Write(person.Licence);

                writer.Write(person.FreeParking);
                SendAttached(writer, person);
                // Start sending the trip chains
                writer.Write(person.TripChains.Count);
                foreach (var tripChain in person.TripChains)
                {
                    writer.Write(tripChain.JointTripID);
                    writer.Write(tripChain.JointTripRep);
                    SendAttached(writer, tripChain);
                    writer.Write(tripChain.Trips.Count);
                    foreach (var trip in tripChain.Trips)
                    {
                        writer.Write(trip.OriginalZone.ZoneNumber);
                        writer.Write(trip.DestinationZone.ZoneNumber);
                        writer.Write((Int32)trip.Purpose);
                        writer.Write(trip.ActivityStartTime.Hours);
                        writer.Write(trip.ActivityStartTime.Minutes);
                        writer.Write(trip.ActivityStartTime.Seconds);
                        var mode = ((ITashaMode)trip[ObservedMode]);
                        if (mode == null)
                        {
                            throw new XTMFRuntimeException(this, "In household #" + household.HouseholdId
                                + " for Person #" + person.Id + " for Trip #" + trip.TripNumber + " there was no observed mode stored!");
                        }
                        writer.Write(mode.ModeName);
                        SendAttached(writer, trip);
                    }
                }
            }
        }
        writer.Flush();
        HouseholdData = mem.ToArray();
    }

    private void ClientDisconnected(IRemoteXTMF obj)
    {
        // Recover the lost data by putting it back on the processing Queue
        lock (this)
        {
            Configuration.DeleteProgressReport(obj.UniqueID);
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

    private object ClientReady(Stream o, IRemoteXTMF client)
    {
        return null;
    }

    private void ClientReady2(object o, IRemoteXTMF client)
    {
        ConnectedClients.Add(client);
        SendNextParameter(client);
    }

    private object ClientWantsHouseholds(Stream o, IRemoteXTMF client)
    {
        return null;
    }

    private void ClientWantsHouseholds2(object o, IRemoteXTMF client)
    {
        // Send the household data!
        client.SendCustomMessage(null, 2);
    }

    private void CreateDistributionThread()
    {
        StartGeneration = new MessageQueue<StartGenerationMessage>();
        Thread distributionThread = new(DistributionMain);
        distributionThread.IsBackground = true;
        distributionThread.Start();
    }

    private void CreateNewInitialPopulation()
    {
        // now that we have the middle point setup we can start to clone our new population
        var totalPopulation = PopulationSize;
        for (int i = 0; i < totalPopulation; i++)
        {
            CreateRandomParameterAt(i);
        }
    }

    private void CreateRandomParameterAt(int i)
    {
        // add in a population of mutents from the middle
        Population[i] = (new ParameterSet() { Value = 0, Parameters = Parameters.Clone() as ParameterSetting[] });
        for (int j = 0; j < Population[i].Parameters.Length; j++)
        {
            Population[i].Parameters[j].Current = ((Parameters[j].Stop - Parameters[j].Start) * (float)RandomGenerator.NextDouble()) + Parameters[j].Start;
        }
        Population[i].ProcessedBy = null;
        Population[i].Processing = false;
        Population[i].Processed = false;
        Population[i].Value = float.NegativeInfinity;
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
                        // if we actually got a message then we are ready to start fireing off tasks
                        foreach (var client in ConnectedClients)
                        {
                            try
                            {
                                if (client.Connected)
                                {
                                    SendNextParameter(client);
                                }
                            }
                            // ReSharper disable once EmptyGeneralCatchClause
                            catch
                            {
                            }
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
        if (StartGeneration != null)
        {
            StartGeneration.Dispose();
        }
    }

    private string GetFileLocation(string input)
    {
        if (Path.IsPathRooted(input))
        {
            return input;
        }
        return Path.Combine(InputBaseDirectory, input);
    }

    private string GetVariableName(string modeName, string parameterName)
    {
        ITashaMode selectedMode = null;
        foreach (var mode in AllModes)
        {
            if (mode.ModeName == modeName)
            {
                selectedMode = mode;
                break;
            }
        }
        if (selectedMode == null)
        {
            throw new XTMFRuntimeException(this, "We were unable to find a mode with the name " + modeName + " while trying to load the parameters to estimate!");
        }
        // Search for a field or property that has an attribute with this name
        var modeType = selectedMode.GetType();
        foreach (var f in modeType.GetProperties())
        {
            // search the attributes
            var attributes = f.GetCustomAttributes(true);
            foreach (var at in attributes)
            {
                // if we find an attribute from XTMF
                ParameterAttribute parameter;
                if ((parameter = (at as ParameterAttribute)) != null)
                {
                    // Check to see if this is our parameter
                    if (parameter.Name == parameterName)
                    {
                        return f.Name;
                    }
                }
            }
        }
        foreach (var f in modeType.GetFields())
        {
            // search the attributes
            var attributes = f.GetCustomAttributes(true);
            foreach (var at in attributes)
            {
                // if we find an attribute from XTMF
                ParameterAttribute parameter;
                if ((parameter = (at as ParameterAttribute)) != null)
                {
                    // Check to see if this is our parameter
                    if (parameter.Name == parameterName)
                    {
                        return f.Name;
                    }
                }
            }
        }
        // If we get here then we did not find it!
        throw new XTMFRuntimeException(this, "We were unable to find a parameter with the name \"" + parameterName + "\" in the mode " + modeName);
    }

    private void InitializeHost()
    {
        string error = null;
        Configuration.DeleteAllProgressReport();
        ModelSystemToExecute = Host.CreateModelSystem(RemoteModelSystemName, ref error);
        if (ModelSystemToExecute == null)
        {
            throw new XTMFRuntimeException(this, error == null ? "We were unable to create the Client Model System!" : error);
        }
        Host.NewClientConnected += NewClientConnected;
        Host.ClientDisconnected += ClientDisconnected;
        Host.RegisterCustomReceiver(0, ClientReady);
        Host.RegisterCustomMessageHandler(0, ClientReady2);
        Host.RegisterCustomReceiver(1, ReadEvaluation);
        Host.RegisterCustomMessageHandler(1, ProcessEvaluation);
        Host.RegisterCustomSender(1, SendParametersToEvaluate);
        Host.RegisterCustomReceiver(2, ClientWantsHouseholds);
        Host.RegisterCustomMessageHandler(2, ClientWantsHouseholds2);
        Host.RegisterCustomSender(2, SendHouseholds);
    }

    private void LoadInitialPopulation()
    {
        if (!PreviousRunFileName.ContainsFileName())
        {
            throw new XTMFRuntimeException(this, "The previous evaluation file is not set to a file!");
        }
        var numberOfColumns = Parameters.Length + 2;
        using (CsvReader reader = new(PreviousRunFileName.GetFileName(InputBaseDirectory)))
        {
            int length;
            // process header
            while (!reader.EndOfFile)
            {
                if ((length = reader.LoadLine()) != numberOfColumns)
                {
                    if (length == 0) continue;
                    throw new XTMFRuntimeException(this, "There was an unexpected number of Columns, we were expecting " + numberOfColumns + " however we found " + length);
                }
                break;
            }
            int highestGenerationFound = int.MinValue;
            int currentPosition = 0;
            // process body
            while (!reader.EndOfFile)
            {
                if ((length = reader.LoadLine()) != numberOfColumns)
                {
                    if (length == 0) continue;
                    throw new XTMFRuntimeException(this, "There was an unexpected number of Columns, we were expecting " + numberOfColumns + " however we found " + length);
                }
                reader.Get(out int generation, 0);
                if (highestGenerationFound < generation)
                {
                    highestGenerationFound = generation;
                }
                Population[currentPosition] = (new ParameterSet() { Value = 0 });
                if (Population[currentPosition].Parameters == null)
                {
                    Population[currentPosition].Parameters = (ParameterSetting[])Parameters.Clone();
                }
                reader.Get(out Population[currentPosition].Value, 1);
                Population[currentPosition].Processed = true;
                for (int j = 0; j < Population[currentPosition].Parameters.Length; j++)
                {
                    reader.Get(out Population[currentPosition].Parameters[j].Current, j + 2);
                }
                currentPosition = (currentPosition + 1) % Population.Length;
            }
            for (int i = 0; i < PopulationSize; i++)
            {
                if (Population[i].Parameters == null)
                {
                    CreateRandomParameterAt(currentPosition);
                }
            }
            CurrentIteration = highestGenerationFound + 1;
            TotalIterations += highestGenerationFound;
        }
        // now that we have everything loaded in we can produce the next generation
        GenerateNextGeneration();
    }

    private void LoadInstructions()
    {
        XmlDocument doc = new();
        doc.Load(GetFileLocation(ParameterInstructions));
        List<ParameterSetting> parameters = [];
        var childNodes = doc["Root"]?.ChildNodes;
        if (childNodes == null)
        {
            return;
        }
        foreach (XmlNode child in childNodes)
        {
            if (child.Name == "Parameter")
            {
                ParameterSetting current = new();
                var childAttributes = child.Attributes;
                if (childAttributes != null)
                {
                    current.Start = float.Parse(childAttributes["Start"].InnerText);
                    current.Stop = float.Parse(childAttributes["Stop"].InnerText);
                    current.Current = current.Start;
                    if (child.HasChildNodes)
                    {
                        var nodes = child.ChildNodes;
                        current.Names = new string[nodes.Count];
                        for (int i = 0; i < nodes.Count; i++)
                        {
                            var grandChildAttributes = nodes[i].Attributes;
                            if (grandChildAttributes != null)
                            {
                                var modeName = grandChildAttributes["Mode"].InnerText;
                                var parameterName = grandChildAttributes["Name"].InnerText;
                                current.Names[i] = String.Concat(modeName, '.', parameterName);
                            }
                        }
                    }
                    else
                    {
                        var modeName = childAttributes["Mode"].InnerText;
                        var parameterName = String.Concat(modeName, '.', childAttributes["Name"].InnerText);
                        current.Names = new[] { parameterName };
                    }
                    if (!ValidateParameterNames || ValidateParameterName(current))
                    {
                        parameters.Add(current);
                    }
                    else
                    {
                        throw new XTMFRuntimeException(this,
                            "This parameter is invalid.  Please make sure the mode exists and that it has a parameter with the given name!\r\n" +
                            child.OuterXml);
                    }
                }
            }
        }
        Parameters = parameters.ToArray();
    }

    private void NewClientConnected(IRemoteXTMF obj)
    {
        // Setup a new model system for them to execute
        lock (this)
        {
            obj.SendModelSystem(ModelSystemToExecute);
            Configuration.CreateProgressReport(obj.UniqueID == null ? "Remote Host" : obj.UniqueID, delegate
            {
                return obj.Progress;
            }, new Tuple<byte, byte, byte>(50, 50, 150));
        }
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
    /// <param name="r"></param>
    /// <returns></returns>
    private object ReadEvaluation(Stream memory, IRemoteXTMF r)
    {
        BinaryReader reader = new(memory);
        ResultMessage ret = new();
        var generation = reader.ReadInt32();
        ret.ProcessedIndex = reader.ReadInt32();
        if (ret.ProcessedIndex == -1)
        {
            SendNextParameter(r);
            return null;
        }
        ret.ProcessedValue = reader.ReadSingle();
        SendNextParameter(r);
        return CurrentIteration == generation ? ret : null;
    }

    private void Save(int index)
    {
        lock (this)
        {
            var run = Population[index];
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
                        writer.Write("Value");
                        for (int i = 0; i < run.Parameters.Length; i++)
                        {
                            for (int j = 0; j < run.Parameters[i].Names.Length; j++)
                            {
                                writer.Write(',');
                                writer.Write(run.Parameters[i].Names[j]);
                            }
                        }
                        writer.WriteLine();
                    }
                    writer.Write(CurrentIteration);
                    writer.Write(',');
                    writer.Write(run.Value);
                    for (int i = 0; i < run.Parameters.Length; i++)
                    {
                        for (int j = 0; j < run.Parameters[i].Names.Length; j++)
                        {
                            writer.Write(',');
                            writer.Write(run.Parameters[i].Current);
                        }
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

    private void SendAttached(BinaryWriter writer, IAttachable att)
    {
        var keys = att.Keys.ToList();
        writer.Write(keys.Count);
        foreach (var key in keys)
        {
            writer.Write(key);
            var o = att[key];
            if (o == null)
            {
                writer.Write("System.Null");
                writer.Write(String.Empty);
            }
            else
            {
                writer.Write(o.GetType().FullName);
                writer.Write(o.ToString());
            }
        }
    }

    private void SendHouseholds(object o, IRemoteXTMF r, Stream s)
    {
        s.Write(HouseholdData, 0, HouseholdData.Length);
        s.Flush();
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
    /// <param name="r"></param>
    /// <param name="s"></param>
    private void SendParametersToEvaluate(object o, IRemoteXTMF r, Stream s)
    {
        lock (this)
        {
            var message = (ResultMessage)o;
            var index = message.ProcessedIndex;
            var toProcess = Population[index];
            var parameters = toProcess.Parameters;
            BinaryWriter writer = new(s);
            writer.Write(CurrentIteration);
            writer.Write(index);
            int total = 0;
            for (int i = 0; i < parameters.Length; i++)
            {
                total += parameters[i].Names.Length;
            }
            writer.Write(total);
            for (int i = 0; i < parameters.Length; i++)
            {
                for (int j = 0; j < parameters[i].Names.Length; j++)
                {
                    writer.Write(parameters[i].Names[j]);
                    writer.Write(parameters[i].Current);
                }
            }
            writer.Flush();
        }
    }

    private bool ValidateParameterName(ParameterSetting current)
    {
        for (int i = 0; i < current.Names.Length; i++)
        {
            var parts = current.Names[i].Split('.');
            if (parts.Length != 2)
            {
                throw new XTMFRuntimeException(this, "We were unable to validate the parameter name '" + current.Names[i] + "'!");
            }
            if (GetVariableName(parts[0], parts[1]) == null)
            {
                return false;
            }
        }
        return true;
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

    protected struct ParameterSetting
    {
        internal float Current;
        internal string[] Names;
        internal float Start;
        internal float Stop;
    }

    protected class CompareParameterSet : IComparer<ParameterSet>
    {
        public int Compare(ParameterSet x, ParameterSet y)
        {
            // we want it in desc order (highest @ 0)
            if (x.Value < y.Value) return 1;
            if (x.Value == y.Value)
            {
                return 0;
            }
            return -1;
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

    #region TashaIgnored

    [DoNotAutomate]
    public ITashaMode AutoMode
    {
        get
        {
            return null;
        }

        set
        {
            throw new NotImplementedException();
        }
    }

    [SubModelInformation(Description = "The auto vehicle type.", Required = true)]
    public IVehicleType AutoType
    {
        get;
        set;
    }

    public Time EndOfDay
    {
        get;
        set;
    }

    public int HouseholdIterations
    {
        get;
        set;
    }

    [DoNotAutomate]
    public ITashaModeChoice ModeChoice
    {
        get;
        set;
    }

    [DoNotAutomate]
    public List<ITashaMode> NonSharedModes
    {
        get
        {
            var ret = new List<ITashaMode>();
            foreach (var mode in AllModes)
            {
                if (!(mode is ISharedMode))
                {
                    ret.Add(mode);
                }
            }
            return ret;
        }

        set
        {

        }
    }

    [DoNotAutomate]
    public List<ITashaMode> OtherModes
    {
        get;
        set;
    }

    public string OutputBaseDirectory
    {
        get;
        set;
    }

    public bool Parallel
    {
        get;
        set;
    }

    [DoNotAutomate]
    public List<IPostHousehold> PostHousehold
    {
        get;
        set;
    }

    [DoNotAutomate]
    public List<IPostIteration> PostIteration
    {
        get;
        set;
    }

    [DoNotAutomate]
    public List<ISelfContainedModule> PostRun
    {
        get;
        set;
    }

    [DoNotAutomate]
    public List<IPostScheduler> PostScheduler
    {
        get;
        set;
    }

    [DoNotAutomate]
    public List<IPreIteration> PreIteration
    {
        get;
        set;
    }

    [DoNotAutomate]
    public List<ISelfContainedModule> PreRun
    {
        get;
        set;
    }

    [DoNotAutomate]
    public List<ISharedMode> SharedModes
    {
        get
        {
            var ret = new List<ISharedMode>();
            foreach (var mode in AllModes)
            {
                if (mode is ISharedMode sm)
                {
                    ret.Add(sm);
                }
            }
            return ret;
        }

        set
        {

        }
    }

    public Time StartOfDay
    {
        get;
        set;
    }

    public ITrip CreateTrip(ITripChain chain, IZone originalZone, IZone destinationZone, Activity purpose, Time startTime)
    {
        throw new NotImplementedException();
    }

    public int GetIndexOfMode(ITashaMode mode)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool all)
    {
        if (StartGeneration != null)
        {
            StartGeneration.Dispose();
            StartGeneration = null;
        }
    }

    #endregion TashaIgnored
}