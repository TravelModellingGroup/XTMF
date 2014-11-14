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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datastructure;
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha
{
    [ModuleInformation(Description = "This is the base for all Tasha Application Runs")]
    public class TashaRuntime(private IConfiguration XTMFConfiguration) : ITashaRuntime
    {
        private const string FinishedProcessingHouseholds = "Finished processing households...";
        private const string StartingHouseholds = "Starting to process households...";
        private const string Initializing = "Initializing Model";
        private IModelSystemStructure ClientStructure;

        [RunParameter("Activity Level File", "ActivityLevel.zfc", "The location of the Activity Level file.")]
        public string ActivityLevelFile;

        [RunParameter("Load All Households", false, "Load all of the households before running (uses more RAM)")]
        public bool LoadAllHouseholds;

        [RunParameter("Mode Parameter File", "", "The parameter file to use (from estimation).")]
        public string ModeParameterFile;

        [RunParameter("Mode Parameter Row", 1, "The row to use for reading the parameters.")]
        public int ModeParameterFileRow;

        [RunParameter("Estimated Households", 148112, "A Guess at the number of households (for progress)")]
        public int NumberOfHouseholds;

        [RunParameter("Override Mode Parameters", false, "Override the mode parameters and use ones from file.")]
        public bool OverrideModeParameters;

        [RunParameter("Recycle Household Data", true, "Should we recycle the households (true) or leave their data in tact between iterations?")]
        public bool RecycleHouseholdData;

        [RunParameter("Skip Loading Households", false, "Should we skip loading households?  This should be false unless used to simulate a Tasha structure.")]
        public bool SkipLoadingHouseholds;

        private float CompletedIterationPercentage;

        private int CurrentHousehold = 0;

        private float IterationPercentage;

        [DoNotAutomate]
        public List<ITashaMode> AllModes { get; private set; }

        [SubModelInformation(Description = "The Auto mode to use for Tasha", Required = true)]
        public ITashaMode AutoMode { get; set; }

        [SubModelInformation(Description = "The type of vehicle used for auto trips", Required = true)]
        public IVehicleType AutoType { get; set; }

        public Time EndOfDay { get; set; }

        [SubModelInformation(Description = "The model that will load our household", Required = true)]
        public IDataLoader<ITashaHousehold> HouseholdLoader { get; set; }

        [RunParameter("Input Base Directory", "../../Input", "The base directory for input.")]
        public string InputBaseDirectory { get; set; }

        [RunParameter("Iterations", 3, "The number of complete iterations Tasha should do.")]
        public int Iterations { get; set; }

        [SubModelInformation(Description = "The ModeChoice Module", Required = false)]
        public ITashaModeChoice ModeChoice { get; set; }

        public string Name { get; set; }

        [SubModelInformation(Description = "All of the network information for the Tasha Model System", Required = false)]
        public IList<INetworkData> NetworkData { get; set; }

        [DoNotAutomate]
        public List<ITashaMode> NonSharedModes { get; set; }

        [SubModelInformation(Description = "A collection of modes other than shared modes and auto", Required = false)]
        public List<ITashaMode> OtherModes { get; set; }

        [RunParameter("Output Base Directory", "Output", "The base directory for output.")]
        public string OutputBaseDirectory { get; set; }

        [RunParameter("Parallel", true, "Run in parallel (Results will not be deterministic")]
        public bool Parallel { get; set; }

        [SubModelInformation(Description = "A collection of modules to run after a household has been completed", Required = false)]
        public List<IPostHousehold> PostHousehold { get; set; }

        [SubModelInformation(Description = "A collection of modules to run after an iteration has completed", Required = false)]
        public List<IPostIteration> PostIteration { get; set; }

        [SubModelInformation(Description = "A Collection of models that will run after the Tasha Method.", Required = false)]
        public List<ISelfContainedModule> PostRun { get; set; }

        [SubModelInformation(Description = "A collection of modules to run after the scheduler has finished on a household", Required = false)]
        public List<IPostScheduler> PostScheduler { get; set; }

        [SubModelInformation(Description = "A Collection of models that will run before the Tasha Method.", Required = false)]
        public List<IPreIteration> PreIteration { get; set; }

        [SubModelInformation(Description = "A Collection of models that will run before the Tasha Method.", Required = false)]
        public List<ISelfContainedModule> PreRun { get; set; }

        private Func<float> _Progress = () => 0f;
        public float Progress
        {
            get { return _Progress(); }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>(50, 150, 50); }
        }

        [RunParameter("Random Seed", 12345, "A number that is used to setup the random number generator for Tasha.")]
        public int RandomSeed { get; set; }

        [SubModelInformation(Description = "The available resources for this model system.", Required = false)]
        public List<IResource> Resources { get; set; }

        [SubModelInformation(Description = "The Scheduler Module", Required = false)]
        public ITashaScheduler Scheduler { get; set; }

        [SubModelInformation(Description = "A collection of modes that can be shared.", Required = false)]
        public List<ISharedMode> SharedModes { get; set; }

        public Time StartOfDay { get; set; }

        [SubModelInformation(Description = "A collection of vehicles that are used by the modes", Required = false)]
        public List<IVehicleType> VehicleTypes { get; set; }

        [SubModelInformation(Description = "The model that will load all of our zones", Required = true)]
        public IZoneSystem ZoneSystem { get; set; }

        public ITrip CreateTrip(ITripChain chain, IZone originalZone, IZone destinationZone, Activity purpose, Time startTime)
        {
            throw new NotImplementedException();
        }

        private volatile bool _ExitRequested = false;

        public bool ExitRequest()
        {
            _ExitRequested = true;
            return true;
        }

        public int GetIndexOfMode(ITashaMode mode)
        {
            if(AllModes == null) return -1;
            var length = AllModes.Count;
            for(int i = 0; i < length; i++)
            {
                if(AllModes[i] == mode)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// This is called before the start method as a way to pre-check that all of the parameters that are selected
        /// are in fact valid for this module.
        /// </summary>
        /// <param name="error">A string that should be assigned a detailed error</param>
        /// <returns>If the validation was successful or if there was a problem</returns>
        public bool RuntimeValidation(ref string error)
        {
            GenerateAllModeList();
            IModelSystemStructure ourStructure = null;
            foreach(var mst in XTMFConfiguration.ProjectRepository.ActiveProject.ModelSystemStructure)
            {
                if(FindUs(mst, ref ourStructure))
                {
                    ClientStructure = ourStructure;
                    break;
                }
            }
            if(ClientStructure == null)
            {
                error = "In '" + Name + "' we were unable to find the Client Model System!";
                return false;
            }
            return true;
        }

        public void Start()
        {
            if(!VehicleTypes.Contains(AutoType))
            {
                VehicleTypes.Add(AutoType);
            }
            ZoneSystem.LoadData();
            if(PreRun != null)
            {
                foreach(var module in PreRun)
                {
                    module.Start();
                }
            }
            IterationPercentage = 1f / Iterations;
            if(PostScheduler != null)
            {
                foreach(var module in PostScheduler)
                {
                    module.Load(Iterations);
                }
            }
            if(PostHousehold != null)
            {
                foreach(var module in PostHousehold)
                {
                    module.Load(Iterations);
                }
            }

            if(OverrideModeParameters)
            {
                InitializeParameters();
            }
            LoadNetworkData(0);
            if(Scheduler != null)
            {
                Scheduler.LoadOneTimeLocalData();
            }

            if(ModeChoice != null)
            {
                ModeChoice.LoadOneTimeLocalData();
            }

            for(int i = 0; i < Iterations; i++)
            {
                CurrentHousehold = 0;
                CompletedIterationPercentage = i * IterationPercentage;
                if(LoadAllHouseholds)
                {
                    if(!SkipLoadingHouseholds)
                    {
                        HouseholdLoader.LoadData();
                    }
                }
                RunIteration(i);
            }
            if(PostRun != null)
            {
                foreach(var module in PostRun)
                {
                    module.Start();
                }
            }
            ZoneSystem.UnloadData();
        }

        private bool FindUs(IModelSystemStructure mst, ref IModelSystemStructure modelSystemStructure)
        {
            if(mst.Module == this)
            {
                modelSystemStructure = mst;
                return true;
            }
            if(mst.Children != null)
            {
                foreach(var child in mst.Children)
                {
                    if(FindUs(child, ref modelSystemStructure))
                    {
                        return true;
                    }
                }
            }
            // Then we didn't find it in this tree
            return false;
        }

        private void InitializeParameters()
        {
            using (var reader = new CsvReader(GetFullPath(ModeParameterFile)))
            {
                int numberOfParameters;
                reader.LoadLine(out numberOfParameters);
                var headers = new string[numberOfParameters];
                for(int i = 2; i < numberOfParameters; i++)
                {
                    reader.Get(out headers[i], i);
                }
                for(int i = 0; i < ModeParameterFileRow; i++)
                {
                    reader.LoadLine();
                }
                if(reader.LoadLine(out var lineSize))
                {
                    if(lineSize < numberOfParameters) numberOfParameters = lineSize;
                    for(int i = 2; i < numberOfParameters; i++)
                    {
                        reader.Get(out float temp, i);
                        AssignValue(headers[i], temp);
                    }
                }
                else
                {
                    throw new XTMFRuntimeException("In '" + Name + "' there was no parameter row '" + ModeParameterFileRow + "' in '" + ModeParameterFile + "';");
                }

            }
        }

        private void AssignValue(string parameterName, float value)
        {
            string[] parts = SplitNameToParts(parameterName);
            AssignValue(parts, 0, ClientStructure, value);
        }

        private void AssignValue(string[] parts, int currentIndex, IModelSystemStructure currentStructure, float value)
        {
            if(currentIndex == parts.Length - 1)
            {
                AssignValue(parts[currentIndex], currentStructure, value, parts);
                return;
            }
            if(currentStructure.Children != null)
            {
                for(int i = 0; i < currentStructure.Children.Count; i++)
                {
                    if(currentStructure.Children[i].Name == parts[currentIndex])
                    {
                        AssignValue(parts, currentIndex + 1, currentStructure.Children[i], value);
                        return;
                    }
                }
            }
            throw new XTMFRuntimeException("Unable to find a child module in '" + parts[currentIndex] + "' named '" + parts[currentIndex + 1]
                + "' in order to assign parameters! \r\n" + parts.Aggregate((previous, next) => previous + " " + next));
        }

        private void AssignValue(string variableName, IModelSystemStructure currentStructure, float value, string[] allParts)
        {
            if(currentStructure == null)
            {
                throw new XTMFRuntimeException("Unable to assign '" + variableName + "', the module is null!");
            }
            var p = currentStructure.Parameters;
            if(p == null)
            {
                throw new XTMFRuntimeException("The structure '" + currentStructure.Name + "' has no parameters!");
            }
            var parameters = p.Parameters;
            bool any = false;
            if(parameters != null)
            {
                for(int i = 0; i < parameters.Count; i++)
                {
                    if(parameters[i].Name == variableName)
                    {
                        var type = currentStructure.Module.GetType();
                        if(parameters[i].OnField)
                        {
                            var field = type.GetField(parameters[i].VariableName);
                            field.SetValue(currentStructure.Module, value);
                            any = true;
                        }
                        else
                        {
                            var field = type.GetProperty(parameters[i].VariableName);
                            field.SetValue(currentStructure.Module, value, null);
                            any = true;
                        }
                    }
                }
            }
            if(!any)
            {
                throw new XTMFRuntimeException("Unable to find a parameter named '" + variableName
                    + "' for module '" + currentStructure.Name + "' in order to assign it a parameter! \r\n" + allParts.Aggregate((previous, next) => previous + " " + next));
            }
        }

        private string[] SplitNameToParts(string parameterName)
        {
            List<string> parts = new List<string>();
            var stringLength = parameterName.Length;
            StringBuilder builder = new StringBuilder();
            for(int i = 0; i < stringLength; i++)
            {
                switch(parameterName[i])
                {
                    case '.':
                        parts.Add(builder.ToString());
                        builder.Clear();
                        break;
                    case '\\':
                        if(i + 1 < stringLength)
                        {
                            if(parameterName[i + 1] == '.')
                            {
                                builder.Append('.');
                                i += 2;
                            }
                            else if(parameterName[i + 1] == '\\')
                            {
                                builder.Append('\\');
                            }
                        }
                        break;
                    default:
                        builder.Append(parameterName[i]);
                        break;
                }
            }
            parts.Add(builder.ToString());
            return parts.ToArray();
        }

        private Func<string> _Status = () => Initializing;

        public override string ToString()
        {
            var status = _Status;
            return _ExitRequested ? (status == null ? "Exiting" : "Exiting:\r\n" +status()) : (status == null ? Initializing : status());
        }

        private static void RecycleTrips(ITripChain tc)
        {
            var trips = tc.Trips;
            for(int i = 0; i < trips.Count; i++)
            {
                var md = Tasha.ModeChoice.ModeData.Get(trips[i]);
                if(md != null)
                {
                    md.Recycle();
                }
            }
        }

        private void GenerateAllModeList()
        {
            AllModes = new List<ITashaMode>();
            NonSharedModes = new List<ITashaMode>();
            AllModes.Add(AutoMode);
            NonSharedModes.Add(AutoMode);
            foreach(var mode in OtherModes)
            {
                AllModes.Add(mode);
                NonSharedModes.Add(mode);
            }
            foreach(var mode in SharedModes)
            {
                AllModes.Add(mode);
            }
        }

        private string GetFullPath(string localPath)
        {
            var fullPath = localPath;
            if(!Path.IsPathRooted(fullPath))
            {
                fullPath = Path.Combine(InputBaseDirectory, fullPath);
            }
            return fullPath;
        }

        private string GetVariableName(ITashaMode selectedMode, string parameterName)
        {
            // Search for a field or property that has an attribute with this name
            var modeType = selectedMode.GetType();
            foreach(var f in modeType.GetProperties())
            {
                // search the attributes
                var attributes = f.GetCustomAttributes(true);
                foreach(var at in attributes)
                {
                    // if we find an attribute from XTMF
                    ParameterAttribute parameter;
                    if((parameter = ((at as ParameterAttribute))) != null)
                    {
                        // Check to see if this is our parameter
                        if(parameter.Name == parameterName)
                        {
                            return f.Name;
                        }
                    }
                }
            }
            foreach(var f in modeType.GetFields())
            {
                // search the attributes
                var attributes = f.GetCustomAttributes(true);
                foreach(var at in attributes)
                {
                    // if we find an attribute from XTMF
                    ParameterAttribute parameter;
                    if((parameter = ((at as ParameterAttribute))) != null)
                    {
                        // Check to see if this is our parameter
                        if(parameter.Name == parameterName)
                        {
                            return f.Name;
                        }
                    }
                }
            }
            // If we get here then we did not find it!
            throw new XTMFRuntimeException("We were unable to find a parameter with the name \"" + parameterName + "\" in the mode " + selectedMode.ModeName);
        }

        private void ReleaseModeData(ITashaHousehold hhld)
        {
            var persons = hhld.Persons;
            for(int i = 0; i < persons.Length; i++)
            {
                var chains = persons[i].TripChains;
                for(int j = 0; j < chains.Count; j++)
                {
                    RecycleTrips(chains[j]);
                }
                chains = persons[i].AuxTripChains;
                for(int j = 0; j < chains.Count; j++)
                {
                    RecycleTrips(chains[j]);
                }
            }
        }

        private void Run(int i, ITashaHousehold hhld)
        {
            if(_ExitRequested)
            {
                return;
            }
            if(Scheduler != null)
            {
                Scheduler.Run(hhld);
                if(PostScheduler != null)
                {
                    foreach(var module in PostScheduler)
                    {
                        module.Execute(hhld);
                    }
                }
            }

            if(ModeChoice != null)
            {
                if(!ModeChoice.Run(hhld))
                {
                    Interlocked.Increment(ref FailedModeChoice);
                }
            }

            if(PostHousehold != null)
            {
                foreach(var module in PostHousehold)
                {
                    module.Execute(hhld, i);
                }
            }
            System.Threading.Interlocked.Increment(ref CurrentHousehold);

            if(RecycleHouseholdData)
            {
                ReleaseModeData(hhld);
                hhld.Recycle();
            }
        }

        public static int FailedModeChoice = 0;

        private void RunIteration(int i)
        {
            if(i > 0)
            {
                LoadNetworkData(i);
            }
            _Status = () => "Processing pre-iteration logic for iteration " + (i + 1).ToString() + " of " + Iterations.ToString();
            RunPreIterationModules(i);
            RunStartIteration(i);
            RunIterationSensitiveStart(i);
            _Progress = () => (Math.Min(((float)CurrentHousehold / NumberOfHouseholds), 1.0f) / Iterations + CompletedIterationPercentage);
            if(!SkipLoadingHouseholds)
            {
                _Status = () => "Processing households for iteration " + (i + 1).ToString() + " of " + Iterations.ToString();
                if(Parallel)
                {
                    RunParallel(i);
                }
                else
                {
                    RunSerial(i);
                }
            }
            RunIterationSensitiveEnd(i);
            UnloadNetworkData();
            _Status = () => "Processing post-iteration logic for iteration " + (i + 1).ToString() + " of " + Iterations.ToString();
            RunFinishedIteration(i);
            RunPostIteration(i);
        }

        private void RunIterationSensitiveStart(int i)
        {
            foreach(var mode in AllModes)
            {
                var sensitive = mode as IIterationSensitive;
                if(sensitive != null)
                {
                    sensitive.IterationStarting(i, Iterations);
                }
            }
        }

        private void RunIterationSensitiveEnd(int i)
        {
            foreach(var mode in AllModes)
            {
                var sensitive = mode as IIterationSensitive;
                if(sensitive != null)
                {
                    sensitive.IterationEnding(i, Iterations);
                }
            }
        }

        private void RunPostIteration(int i)
        {
            if(PostIteration != null)
            {
                foreach(var module in PostIteration)
                {
                    module.Execute(i, Iterations);
                }
            }
        }

        private void RunFinishedIteration(int i)
        {
            if(ModeChoice != null)
            {
                if(!_ExitRequested)
                {
                    ModeChoice.IterationFinished(i, Iterations);
                }
            }
            if(PostScheduler != null)
            {
                foreach(var module in PostScheduler)
                {
                    if(!_ExitRequested)
                    {
                        module.IterationFinished(i);
                    }
                }
            }
            if(PostHousehold != null)
            {
                foreach(var module in PostHousehold)
                {
                    if(!_ExitRequested)
                    {
                        module.IterationFinished(i);
                    }
                }
            }
        }

        private void UnloadNetworkData()
        {
            if(NetworkData != null)
            {
                foreach(var network in NetworkData)
                {
                    if(!_ExitRequested)
                    {
                        network.UnloadData();
                    }
                }
            }
        }

        private void RunStartIteration(int i)
        {
            if(ModeChoice != null)
            {
                if(!_ExitRequested)
                {
                    ModeChoice.IterationStarted(i, Iterations);
                }
            }
            if(PostScheduler != null)
            {
                foreach(var module in PostScheduler)
                {
                    if(!_ExitRequested)
                    {
                        module.IterationStarting(i);
                    }
                }
            }
            if(PostHousehold != null)
            {
                foreach(var module in PostHousehold)
                {
                    if(!_ExitRequested)
                    {
                        module.IterationStarting(i);
                    }
                }
            }
        }

        private void RunPreIterationModules(int i)
        {
            if(PreIteration != null)
            {
                foreach(var module in PreIteration)
                {
                    if(!_ExitRequested)
                    {
                        module.Execute(i, Iterations);
                    }
                }
            }
        }

        private void LoadNetworkData(int iteration)
        {
            if(NetworkData != null)
            {
                _Status = () => "Loading Network Data for iteration " + (iteration + 1).ToString() + " of " + Iterations;
                System.Threading.Tasks.Parallel.ForEach(NetworkData,
                    delegate (INetworkData network)
                {
                    if(!_ExitRequested)
                    {
                        network.LoadData();
                    }
                });
            }
        }

        private void RunParallel(int iteration)
        {
            if(LoadAllHouseholds)
            {
                var hhlds = HouseholdLoader.ToArray();
                NumberOfHouseholds = hhlds.Length;
                if(hhlds == null) return;
                Console.WriteLine(StartingHouseholds);
                System.Threading.Tasks.Parallel.For(0, hhlds.Length,
                   delegate (int i)
                {
                    ITashaHousehold hhld = hhlds[i];
                    Run(iteration, hhld);
                });
                Console.WriteLine(FinishedProcessingHouseholds);
            }
            else
            {
                Console.WriteLine(StartingHouseholds);
                System.Threading.Tasks.Parallel.ForEach(HouseholdLoader, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, delegate (ITashaHousehold household)
                {
                    Run(iteration, household);
                });
                if(CurrentHousehold != NumberOfHouseholds)
                {
                    NumberOfHouseholds = CurrentHousehold;
                }
                Console.WriteLine(FinishedProcessingHouseholds);
            }
        }

        private void RunSerial(int iteration)
        {
            if(LoadAllHouseholds)
            {
                var households = HouseholdLoader.ToArray();
                NumberOfHouseholds = households.Length;
                Console.WriteLine(StartingHouseholds);
                for(int i = 0; i < households.Length; i++)
                {
                    ITashaHousehold hhld = households[i];
                    Run(iteration, hhld);
                }
            }
            else
            {
                if(iteration >= 1)
                {
                    HouseholdLoader.Reset();
                }
                Console.WriteLine(StartingHouseholds);
                foreach(var hhld in HouseholdLoader)
                {
                    Run(iteration, hhld);
                }
                Console.WriteLine(FinishedProcessingHouseholds);
            }
        }
    }
}