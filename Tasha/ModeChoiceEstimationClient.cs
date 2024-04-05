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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tasha.Common;
using Tasha.Internal;
using Tasha.Scheduler;
using TMG;
using XTMF;
using XTMF.Networking;

namespace Tasha;

public class ModeChoiceEstimationClient : ITashaRuntime
{
    public IClient Client;

    private static Tuple<byte, byte, byte> _ProgressColour = new(50, 150, 50);

    private bool Exit;

    private float[] HouseholdEvaluation;

    private MessageQueue<TashaHousehold[]> HouseholdMessages;

    private TashaHousehold[] Households;

    private int ProcessedSoFar;

    private MessageQueue<ParameterInstructions> ServerMessages;

    [DoNotAutomate]
    public List<ITashaMode> AllModes
    {
        get;
        set;
    }

    [SubModelInformation(Description = "The auto mode.", Required = true)]
    public ITashaMode AutoMode
    {
        get;
        set;
    }

    [SubModelInformation(Description = "The type of vehicle that auto is", Required = true)]
    public IVehicleType AutoType
    {
        get;
        set;
    }

    [RunParameter("End of Day", "28:00", typeof(Time), "The time that Tasha will end at.")]
    public Time EndOfDay
    {
        get;
        set;
    }

    [RunParameter("Household Iterations", 50, "The number of mode assignments per trip that will be calculated.")]
    public int HouseholdIterations
    {
        get;
        set;
    }

    [DoNotAutomate]
    public IDataLoader<ITashaHousehold> HouseholdLoader
    {
        get;
        set;
    }

    [RunParameter("Input Directory", "../../Input", "The directory that the input files will be in.")]
    public string InputBaseDirectory
    {
        get;
        set;
    }

    public int TotalIterations
    {
        get;
        set;
    }

    [SubModelInformation(Description = "The mode choice algorithm", Required = true)]
    public ITashaModeChoice ModeChoice
    {
        get;
        set;
    }

    public string Name
    {
        get;
        set;
    }

    [SubModelInformation(Description = "The different data for the modes.", Required = false)]
    public IList<INetworkData> NetworkData
    {
        get;
        set;
    }

    [DoNotAutomate]
    public List<ITashaMode> NonSharedModes
    {
        get;
        set;
    }

    [SubModelInformation(Description = "The non shared modes besides auto", Required = false)]
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

    [RunParameter("Parallel", true, "Should we run in parallel? (Non deterministic results but much faster)")]
    public bool Parallel
    {
        get;
        set;
    }

    [SubModelInformation(Description = "The modules to run after a household has been processed.", Required = false)]
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

    public float Progress
    {
        get;
        set;
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return _ProgressColour; }
    }

    [RunParameter("Random Seed", 12345, "The seed for the random number generator.")]
    public int RandomSeed
    {
        get;
        set;
    }

    [SubModelInformation(Description = "The available resources for this model system.", Required = false)]
    public List<IResource> Resources { get; set; }

    [SubModelInformation(Description = "The modes that are shared between individuals", Required = false)]
    public List<ISharedMode> SharedModes
    {
        get;
        set;
    }

    [RunParameter("Start of Day", "4:00", typeof(Time), "The time that Tasha will start at.")]
    public Time StartOfDay
    {
        get;
        set;
    }

    [SubModelInformation(Description = "The types of vehicles used.", Required = false)]
    public List<IVehicleType> VehicleTypes
    {
        get;
        set;
    }

    [SubModelInformation(Description = "The zone system that we are going to be using.", Required = true)]
    public IZoneSystem ZoneSystem
    {
        get;
        set;
    }

    public ITrip CreateTrip(ITripChain chain, IZone originalZone, IZone destinationZone, Activity purpose, Time startTime)
    {
        throw new NotImplementedException();
    }

    public bool ExitRequest()
    {
        Exit = true;
        return true;
    }

    public int GetIndexOfMode(ITashaMode mode)
    {
        var numberOfModes = AllModes.Count;
        for (int i = 0; i < numberOfModes; i++)
        {
            if (AllModes[i] == mode)
            {
                return i;
            }
        }
        return -1;
    }

    public bool RuntimeValidation(ref string error)
    {
        if (Client == null)
        {
            error = "The ModeChoiceEstimationClient Module requires you to be running in an XTMF environment that supports Client Networking.";
            return false;
        }
        GenerateAllModeList();
        return true;
    }

    public void Start()
    {
        // Load up our zone system and our modes
        ZoneSystem.LoadData();
        foreach (var network in NetworkData)
        {
            network.LoadData();
        }
        LoadModes();
        using (ServerMessages = new MessageQueue<ParameterInstructions>())
        {
            using (HouseholdMessages = new MessageQueue<TashaHousehold[]>())
            {
                InitializeNetworking();
                if (LoadHouseholds())
                {
                    HouseholdEvaluation = new float[Households.Length];
                    ProcessTashaRequests();
                    HouseholdEvaluation = null;
                }
            }
        }
        foreach (var network in NetworkData)
        {
            network.UnloadData();
        }
        // unload the modes
        ZoneSystem.UnloadData();
    }

    private void AssignParameter(ITashaMode mode, string parameter, float value)
    {
        parameter = GetVariableName(mode, parameter);
        Type modeType = mode.GetType();
        var fieldInfo = modeType.GetField(parameter);
        if (fieldInfo != null)
        {
            fieldInfo.SetValue(mode, value);
            return;
        }
        var propertyInfo = modeType.GetProperty(parameter);
        propertyInfo?.SetValue(mode, value, null);
    }

    private float EvaluateHousehold(TashaHousehold household)
    {
        double fitness = 0;
        var householdIterations = HouseholdIterations;
        foreach (var p in household.Persons)
        {
            foreach (var chain in p.TripChains)
            {
                foreach (var trip in chain.Trips)
                {
                    var value = Math.Log((EvaluateTrip(trip) + 1) / (householdIterations + 1));
                    fitness += value;
                    Array.Clear(trip.ModesChosen, 0, trip.ModesChosen.Length);
                    //trip.Release();
                }
                //chain.Release();
            }
            //p.Release();
        }
        //household.Release();
        return (float)fitness;
    }

    private float EvaluateTrip(ITrip trip)
    {
        int correct = 0;
        var observedMode = ((SchedulerTrip)trip).ObservedMode;
        foreach (var choice in trip.ModesChosen)
        {
            if (choice == observedMode)
            {
                correct++;
            }
        }
        return correct;
    }

    private ITripChain FindRepTripChain(SchedulerTripChain chain, ITashaHousehold tashaHousehold)
    {
        foreach (var person in tashaHousehold.Persons)
        {
            foreach (var tc in person.TripChains)
            {
                if (tc.JointTripID == chain.JointTripID && tc.JointTripRep)
                {
                    return tc;
                }
            }
        }
        throw new XTMFRuntimeException(this, "We were unable to find a joint trip representative's trip chain!");
    }

    private void GenerateAllModeList()
    {
        AllModes = [];
        NonSharedModes = [];
        AllModes.Add(AutoMode);
        NonSharedModes.Add(AutoMode);
        foreach (var mode in OtherModes)
        {
            AllModes.Add(mode);
            NonSharedModes.Add(mode);
        }
        foreach (var mode in SharedModes)
        {
            AllModes.Add(mode);
        }
    }

    private string GetVariableName(ITashaMode selectedMode, string parameterName)
    {
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
                if ((parameter = ((at as ParameterAttribute))) != null)
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
                if ((parameter = ((at as ParameterAttribute))) != null)
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
        throw new XTMFRuntimeException(this, "We were unable to find a parameter with the name \"" + parameterName + "\" in the mode " + selectedMode.ModeName);
    }

    private void InitializedModeParameters(ParameterInstructions job)
    {
        var numberOfParameters = job.Names.Length;
        var modes = AllModes;
        var numberOfModes = modes.Count;
        System.Threading.Tasks.Parallel.For(0, numberOfParameters, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
            delegate (int i)
        {
            int endOfMode;
            string mode = job.Names[i].Substring(0, endOfMode = job.Names[i].IndexOf('.'));
            string parameter = job.Names[i].Substring(endOfMode + 1);
                // find the matching mode
                for (int j = 0; j < numberOfModes; j++)
            {
                if (mode == modes[j].ModeName)
                {
                    AssignParameter(modes[j], parameter, job.Values[i]);
                    break;
                }
            }
        });
    }

    private void InitializeNetworking()
    {
        Client.RegisterCustomSender(0, SendReady);
        Client.RegisterCustomMessageHandler(1, ReceiveParameters);
        Client.RegisterCustomReceiver(1, ProcessParameters);
        Client.RegisterCustomSender(1, SendResult);
        Client.RegisterCustomSender(2, SendRequestHouseholds);
        Client.RegisterCustomReceiver(2, ReceiveHouseholds);
        Client.RegisterCustomMessageHandler(2, NotifyHouseholdsLoaded);
    }

    private TashaHousehold LoadHousehold(BinaryReader reader, Datastructure.SparseArray<IZone> zoneArray)
    {
        var household = new TashaHousehold();
        int numberOfPeople;
        household.HouseholdId = reader.ReadInt32();
        // Learn how many people this household has and their number of vehicles
        household.Persons = new ITashaPerson[(numberOfPeople = reader.ReadInt32())];
        var vehicleList = new List<IVehicle>();
        // Produce the vehicles, all auto since it is the only type of resource we have
        for (int i = 0; i < VehicleTypes.Count; i++)
        {
            var numberOfVehicles = reader.ReadInt32();
            for (int j = 0; j < numberOfVehicles; j++)
            {
                vehicleList.Add(TashaVehicle.MakeVehicle(VehicleTypes[i]));
            }
        }
        household.Vehicles = vehicleList.ToArray();
        household.HomeZone = zoneArray[reader.ReadInt32()];
        LoadKeys(reader, household);
        // now we can go and load the people
        for (int i = 0; i < numberOfPeople; i++)
        {
            household.Persons[i] = LoadPerson(reader, zoneArray, household, i);
        }
        // Link in the joint trip chain trip chains
        foreach (var person in household.Persons)
        {
            foreach (var tc in person.TripChains)
            {
                if (tc.JointTrip)
                {
                    if (tc.JointTripRep)
                    {
                        ((SchedulerTripChain)tc).GetRepTripChain = tc;
                    }
                    else
                    {
                        ((SchedulerTripChain)tc).GetRepTripChain = FindRepTripChain((SchedulerTripChain)tc, person.Household);
                    }
                }
            }
        }
        return household;
    }

    private bool LoadHouseholds()
    {
        // Request the household data
        Client.SendCustomMessage(null, 2);
        while (!Exit)
        {
            var households = HouseholdMessages.GetMessageOrTimeout(200);
            if (households != null)
            {
                Households = households;
                return true;
            }
            Thread.MemoryBarrier();
            GC.Collect();
        }
        return false;
    }

    private void LoadKeys(BinaryReader reader, IAttachable att)
    {
        var numberOfKeys = reader.ReadInt32();
        for (int i = 0; i < numberOfKeys; i++)
        {
            var name = reader.ReadString();
            var type = reader.ReadString();
            var text = reader.ReadString();
            switch (type)
            {
                case "System.String":
                    att.Attach(name, text);
                    break;

                case "System.Single":
                    att.Attach(name, float.Parse(text));
                    break;

                case "System.Int32":
                    att.Attach(name, int.Parse(text));
                    break;
            }
        }
    }

    private void LoadModes()
    {
        AllModes = [];
        NonSharedModes = [];
        AllModes.Add(AutoMode);
        NonSharedModes.Add(AutoMode);
        foreach (var mode in OtherModes)
        {
            AllModes.Add(mode);
            NonSharedModes.Add(mode);
        }
        foreach (var mode in SharedModes)
        {
            AllModes.Add(mode);
        }
        VehicleTypes.Add(AutoType);
    }

    private TashaPerson LoadPerson(BinaryReader reader, Datastructure.SparseArray<IZone> zoneArray, TashaHousehold household, int personID)
    {
        TashaPerson person = new()
        {
            Household = household,
            Id = personID,
            Age = reader.ReadInt32(),
            Female = reader.ReadBoolean(),
            EmploymentStatus = (TTSEmploymentStatus)reader.ReadInt32(),
            Occupation = (Occupation)reader.ReadInt32(),
            EmploymentZone = zoneArray[reader.ReadInt32()],
            StudentStatus = (StudentStatus)reader.ReadInt32(),
            SchoolZone = zoneArray[reader.ReadInt32()],
            Licence = reader.ReadBoolean(),
            FreeParking = reader.ReadBoolean()
        };
        int numberOfTripChains;
        LoadKeys(reader, person);
        person.TripChains = new List<ITripChain>(numberOfTripChains = reader.ReadInt32());
        for (int i = 0; i < numberOfTripChains; i++)
        {
            person.TripChains.Add(LoadTripChain(reader, zoneArray, person));
        }
        return person;
    }

    private SchedulerTrip LoadTrip(BinaryReader reader, Datastructure.SparseArray<IZone> zoneArray, SchedulerTripChain chain, int tripNumber)
    {
        SchedulerTrip trip = SchedulerTrip.GetTrip(HouseholdIterations);
        trip.TripNumber = tripNumber;
        trip.TripChain = chain;
        // figure out where we are going
        trip.OriginalZone = zoneArray[reader.ReadInt32()];
        trip.DestinationZone = zoneArray[reader.ReadInt32()];
        trip.Purpose = (Activity)reader.ReadInt32();
        // And learn when we are leaving, and at what time we need to get there
        Time time = new()
        {
            // The activity's start time
            Hours = reader.ReadInt32(),
            Minutes = reader.ReadInt32(),
            Seconds = reader.ReadInt32()
        };
        trip.ActivityStartTime = time;
        // Get the observed mode
        var modeName = reader.ReadString();
        for (int i = 0; i < AllModes.Count; i++)
        {
            if (modeName == AllModes[i].ModeName)
            {
                trip.ObservedMode = AllModes[i];
            }
        }
        LoadKeys(reader, trip);
        return trip;
    }

    private SchedulerTripChain LoadTripChain(BinaryReader reader, Datastructure.SparseArray<IZone> zoneArray, TashaPerson person)
    {
        SchedulerTripChain chain = SchedulerTripChain.GetTripChain(person);
        chain.JointTripID = reader.ReadInt32();
        chain.JointTripRep = reader.ReadBoolean();
        LoadKeys(reader, chain);
        int numberOfTrips = reader.ReadInt32();
        for (int i = 0; i < numberOfTrips; i++)
        {
            SchedulerTrip trip = LoadTrip(reader, zoneArray, chain, i);
            // Now that we have all of the data that we need, add ourselves to the trip chain
            chain.Trips.Add(trip);
        }
        return chain;
    }

    private void NotifyHouseholdsLoaded(object householdsObject)
    {
        HouseholdMessages.Add(householdsObject as TashaHousehold[]);
    }

    private object ProcessParameters(Stream customMessage)
    {
        // We do not use a using statement here because we do not want the reader to close the base stream
        BinaryReader reader = new(customMessage);
        // first read in the generation and what index we are processing
        var generation = reader.ReadInt32();
        var index = reader.ReadInt32();
        // Then we need to know how many parameters there will be
        var numberOfParameters = reader.ReadInt32();
        var names = new string[numberOfParameters];
        var values = new float[numberOfParameters];
        for (int i = 0; i < numberOfParameters; i++)
        {
            // Read in the name of the parameter
            names[i] = reader.ReadString();
            // and the value for it
            values[i] = reader.ReadSingle();
        }
        // Make sure to not use the close method or the base stream will also be closed
        // Once we have everything create the processing instruction that we will need
        return new ParameterInstructions() { Generation = generation, Index = index, Names = names, Values = values };
    }

    private void ProcessTashaRequests()
    {
        // Tell the host that we are ready
        Client.SendCustomMessage(null, 0);
        ModeChoice.LoadOneTimeLocalData();
        ModeChoice.IterationStarted(0, 1);
        // now lets wait for some parameters
        while (!Exit)
        {
            var queue = ServerMessages;
            if (queue == null)
            {
                return;
            }
            var job = queue.GetMessageOrTimeout(200);
            if (job != null)
            {
                job.Result = RunJob(job);
                ReportResult(job);
                // after we have reported clean the memory
                GC.Collect();
            }
            Thread.MemoryBarrier();
        }
        ModeChoice.IterationFinished(0, 1);
    }

    private object ReceiveHouseholds(Stream fromHost)
    {
        var reader = new BinaryReader(fromHost);
        int numberOfHouseholds = reader.ReadInt32();
        int numberOfVehicles = reader.ReadInt32();
        if (numberOfVehicles != VehicleTypes.Count)
        {
            throw new XTMFRuntimeException(this, "We were expecting to have '" + VehicleTypes.Count + "' different types of vehicles but the host has '" + numberOfVehicles + "'");
        }
        for (int i = 0; i < numberOfVehicles; i++)
        {
            string temp;
            if (VehicleTypes[i].VehicleName != (temp = reader.ReadString()))
            {
                throw new XTMFRuntimeException(this, "We were expecting the vehicle type to be named '" + VehicleTypes[i].VehicleName + "' and instead found '" + temp + "'");
            }
        }
        TashaHousehold[] households = new TashaHousehold[numberOfHouseholds];
        var zoneArray = ZoneSystem.ZoneArray;
        for (int i = 0; i < numberOfHouseholds; i++)
        {
            households[i] = LoadHousehold(reader, zoneArray);
        }
        return households;
    }

    private void ReceiveParameters(object parametersObject)
    {
        // queue up these parameters for processing
        var parameters = parametersObject as ParameterInstructions;
        // if they are not actually parameters just return
        if (parameters == null)
        {
            return;
        }
        ServerMessages.Add(parameters);
    }

    private void ReportResult(ParameterInstructions job)
    {
        Client.SendCustomMessage(job, 1);
    }

    private float RunJob(ParameterInstructions job)
    {
        InitializedModeParameters(job);
        ProcessedSoFar = 0;
        Thread.MemoryBarrier();
        if (Parallel)
        {
            System.Threading.Tasks.Parallel.For(0, Households.Length, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }
              , RunTasha);
            //System.Threading.Tasks.Parallel.ForEach( this.Households, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
            //RunTasha );
        }
        else
        {
            for (int i = 0; i < Households.Length; i++)
            {
                RunTasha(i);
            }
        }

        return HouseholdEvaluation.AsParallel().Sum();
    }

    private void RunTasha(int householdIndex)
    {
        var hhld = Households[householdIndex];
        try
        {
            if (ModeChoice.Run(hhld))
            {
                HouseholdEvaluation[householdIndex] = EvaluateHousehold(hhld);
            }
            else
            {
                SetToMax(householdIndex, hhld);
            }
        }
        catch (Exception e)
        {
            if (e is XTMFRuntimeException)
            {
                Console.WriteLine(e.Message);
            }
            else
            {
                Console.WriteLine(e.Message + "\r\n" + e.StackTrace);
            }
            SetToMax(householdIndex, hhld);
        }
        Interlocked.Increment(ref ProcessedSoFar);
        Progress = ProcessedSoFar / (float)HouseholdEvaluation.Length;
    }

    private void SendReady(object nullGoesHere, Stream toHost)
    {
        // We don't need to send any additional data here
    }

    private void SendRequestHouseholds(object unused, Stream toHost)
    {
        // Do nothing, sending this message number is enough
    }

    private void SendResult(object data, Stream toHost)
    {
        var job = data as ParameterInstructions;
        if (job == null)
        {
            return;
        }
        BinaryWriter writer = new(toHost);
        writer.Write(job.Generation);
        writer.Write(job.Index);
        writer.Write(job.Result);
        writer.Flush();
    }

    private void SetToMax(int householdIndex, TashaHousehold hhld)
    {
        double fitness = 0;
        var ammount = (float)Math.Log(1.00 / (HouseholdIterations + 1));
        foreach (var p in hhld.Persons)
        {
            foreach (var chain in p.TripChains)
            {
                foreach (var trip in chain.Trips)
                {
                    fitness += ammount;
                    Array.Clear(trip.ModesChosen, 0, trip.ModesChosen.Length);
                }
            }
        }
        HouseholdEvaluation[householdIndex] = (float)fitness;
    }

    private class ParameterInstructions
    {
        internal int Generation;
        internal int Index;
        internal string[] Names;
        internal float Result;
        internal float[] Values;
    }
}