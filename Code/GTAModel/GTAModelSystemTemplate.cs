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
using XTMF;

namespace TMG.GTAModel
{
    [ModuleInformation( Description =
        "The Model System Template for GTAModel V2/2.5 based Models."
        + @" This module is the root module for GTAModel. It requires an IZoneSystem, IDemographicsData, 
a list of INetworkData, a list of IModeChoiceNode, an INetworkAssignment, a list of IPurpose, an 
IPopulation, and finally a list of ISelfContainedModule for running at the end of the model system. 
Optionally it can also take in 2 other ISelfContainedModule, one for assigning work zones and another 
for assigning school zones." )]
    public class GTAModelSystemTemplate : IDemographic4StepModelSystemTemplate, IResourceSource
    {
        [RunParameter( "Parallel Post Processing", true, "Process the post run modules in parallel." )]
        public bool ParallelPostProcessing;

        [SubModelInformation( Required = false, Description = "Model systems that should be executed before each iteration" )]
        public List<ISelfContainedModule> PreIteration;

        private int CurrentPurpose;

        private Func<string> GetCurrentStatus = ( () => "" );

        private Func<float> GetProgress = ( () => 0f );

        private string Status;

        [SubModelInformation( Description = "The Algorithm to assign place of school for the population.", Required = false )]
        public ISelfContainedModule AssignSchoolZones { get; set; }

        [SubModelInformation( Description = "The Algorithm to assign place of work for the population.", Required = false )]
        public ISelfContainedModule AssignWorkZones { get; set; }

        public int CurrentIteration { get; set; }

        [SubModelInformation( Description = "The module that contains all of the information that supplements the ZoneSystem", Required = true )]
        public IDemographicsData Demographics { get; set; }

        [RunParameter( "Input Directory", "../../Input", "The directory that our input is located in." )]
        public string InputBaseDirectory
        {
            get;
            set;
        }

        [SubModelInformation( Description = "Controls the setting up and organization of different mode parameters", Required = false )]
        public IModeParameterDatabase ModeParameterDatabase { get; set; }

        [SubModelInformation( Description = "The modes contained in this mode split.", Required = false )]
        public List<IModeChoiceNode> Modes { get; set; }

        public string Name
        {
            get;
            set;
        }

        [SubModelInformation( Description = "The module used to do assignments to the network", Required = true )]
        public INetworkAssignment NetworkAssignment { get; set; }

        [SubModelInformation( Description = "The network data used for this model system.", Required = false )]
        public IList<INetworkData> NetworkData { get; set; }

        public string OutputBaseDirectory
        {
            get;
            set;
        }

        [SubModelInformation( Description = "The population loading algorithm", Required = true )]
        public IPopulation Population
        {
            get;
            set;
        }

        [SubModelInformation( Description = "The modules to execute once GTAModel has finished.", Required = false )]
        public List<ISelfContainedModule> PostRun { get; set; }

        public float Progress
        {
            get
            {
                var progressFunction = GetProgress;
                if ( progressFunction != null )
                {
                    return progressFunction();
                }
                return 0f;
            }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get
            {
                DateTime now = DateTime.Now;
                float ratio = 1;
                return new Tuple<byte, byte, byte>( (byte)( 0 * ratio ), (byte)( 117 * ratio ), (byte)( 255 * ratio ) );
            }
        }

        [SubModelInformation( Description = "The different purposes for running (example \"Work\")", Required = false )]
        public List<IPurpose> Purpose { get; set; }

        [RunParameter( "Total Iterations", 1, "The amount of iterations to run." )]
        public int TotalIterations
        {
            get;
            set;
        }

        [SubModelInformation( Description = "The module that loads the zone system", Required = true )]
        public IZoneSystem ZoneSystem { get; set; }

        private bool ExitRequested = false;

        public bool ExitRequest()
        {
            ExitRequested = true;
            Status = "Exiting after current operation has completed";
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( Purpose == null || Purpose.Count == 0 )
            {
                //error = "There were no Purposes defined for this Model System.  Please recreate it with some purposes.";
                //return false;
            }
            if ( TotalIterations <= 0 )
            {
                error = "The total number of iterations was set to (or less than) zero.  Please give it a positive integer.";
                return false;
            }
            return true;
        }

        public void Start()
        {
            CurrentIteration = 0;
            Status = "Running Model System Setup";
            GetCurrentStatus = ( () => NetworkAssignment.ToString() );
            GetProgress = ( () => NetworkAssignment.Progress );
            NetworkAssignment.RunModelSystemSetup();
            if ( ExitRequested ) { return; }
            LoadZoneData();
            if ( ExitRequested ) { return; }
            Status = "Running Initial Network Assignment";
            GetCurrentStatus = ( () => NetworkAssignment.ToString() );
            GetProgress = ( () => NetworkAssignment.Progress );
            NetworkAssignment.RunInitialAssignments();
            GetCurrentStatus = ( () => "" );
            if ( ExitRequested ) { return; }
            LoadNetworkData();
            if ( ExitRequested ) { return; }
            InitializePopulation();
            if ( ExitRequested ) { return; }
            for ( int iteration = 0; iteration < TotalIterations; iteration++ )
            {
                if ( ExitRequested ) { return; }
                foreach ( var module in PreIteration )
                {
                    Status = String.Concat( "Running Iteration ", ( iteration + 1 ), " of ", TotalIterations, " : ", module.ToString() );
                    GetProgress = ( () => module.Progress );
                    module.Start();
                }
                if ( iteration == 0 )
                {
                    TellModesWeAreStartingNewIteration();
                    ReProcessWorkSchoolZones();
                }
                var purposeLength = Purpose.Count;
                for ( int i = 0; i < purposeLength; i++ )
                {
                    Status = String.Concat( "Running Iteration ", ( iteration + 1 ), " of ", TotalIterations, " : ", Purpose[i].PurposeName );
                    CurrentPurpose = i;
                    GetProgress = ( () => Purpose[i].Progress );
                    Purpose[i].Run();
                    if ( ExitRequested ) { return; }
                }
                GetCurrentStatus = ( () => NetworkAssignment.ToString() );
                GetProgress = ( () => NetworkAssignment.Progress );
                Status = String.Concat( "Running Network Assignment (", ( iteration + 1 ), " of ", TotalIterations, ")" );
                UnloadNetworkData();
                NetworkAssignment.RunNetworkAssignment();
                GetCurrentStatus = ( () => "" );
                TellModesWeAreEndingIteration();
                // if it isn't the last iteration reprocess the locations that people work and go to school
                if ( iteration < TotalIterations - 1 )
                {
                    CurrentIteration++;
                    LoadNetworkData();
                    TellModesWeAreStartingNewIteration();
                    ReProcessWorkSchoolZones();
                }
            }
            CurrentIteration = TotalIterations;
            if ( ExitRequested ) { return; }
            GetCurrentStatus = ( () => NetworkAssignment.ToString() );
            GetProgress = ( () => NetworkAssignment.Progress );
            Status = "Running Final Network Assignment";
            NetworkAssignment.RunPostAssignments();
            GetCurrentStatus = ( () => "" );
            if ( ExitRequested ) { return; }
            Status = "Running Post Run Modules";
            RunPostRunModules();
            GetProgress = ( () => 1f );
            Status = "Shutting Down";
            Population = null;
            Demographics.UnloadData();
            ZoneSystem.UnloadData();
        }

        public override string ToString()
        {
            return String.Concat( Status, ": ", GetCurrentStatus() );
        }

        private string GetFullPath(string localPath)
        {
            var fullPath = localPath;
            if ( !System.IO.Path.IsPathRooted( fullPath ) )
            {
                fullPath = System.IO.Path.Combine( InputBaseDirectory, fullPath );
            }
            return fullPath;
        }

        private void InitializePopulation()
        {
            Status = "Loading Population";
            GetProgress = ( () => Population.Progress );
            Population.Load();
        }

        private void LoadNetworkData()
        {
            foreach ( var dataSource in NetworkData )
            {
                Status = String.Concat( "Loading ", dataSource.NetworkType, " Network" );
                dataSource.LoadData();
            }
        }

        private void LoadZoneData()
        {
            Status = "Loading Zone System";
            ZoneSystem.LoadData();
            Status = "Loading Demographics";
            Demographics.LoadData();
        }

        private void ReProcessWorkSchoolZones()
        {
            if ( AssignWorkZones != null )
            {
                Status = "Assigning Work Zones: Iteration " + ( CurrentIteration + 1 ) + " of " + TotalIterations;
                GetProgress = ( () => AssignWorkZones.Progress );
                AssignWorkZones.Start();
            }
            if ( AssignSchoolZones != null )
            {
                Status = "Assigning School Zones: Iteration " + ( CurrentIteration + 1 ) + " of " + TotalIterations;
                GetProgress = ( () => AssignSchoolZones.Progress );
                AssignSchoolZones.Start();
            }
        }

        private void RunPostRunModules()
        {
            if ( PostRun != null )
            {
                int complete = 0;
                GetProgress = ( () => complete / (float)PostRun.Count );
                // launch all of the post processing in parallel
                if ( ParallelPostProcessing )
                {
                    System.Threading.Tasks.Parallel.ForEach( PostRun,
                        delegate(ISelfContainedModule module)
                        {
                            module.Start();
                            System.Threading.Interlocked.Increment( ref complete );
                        } );
                }
                else
                {
                    foreach ( var module in PostRun )
                    {
                        module.Start();
                        complete++;
                    }
                }
            }
        }

        private void TellModesWeAreEndingIteration()
        {
            foreach ( var mode in Modes )
            {
                var c = mode as IIterationSensitive;
                if ( c != null )
                {
                    c.IterationEnding( CurrentIteration, TotalIterations );
                }
                c = null;
                TellModesWeAreEndingIteration( mode as IModeCategory );
            }
        }

        private void TellModesWeAreEndingIteration(IModeCategory node)
        {
            if ( node != null )
            {
                foreach ( var mode in node.Children )
                {
                    var c = mode as IIterationSensitive;
                    if ( c != null )
                    {
                        c.IterationEnding( CurrentIteration, TotalIterations );
                    }
                    c = null;
                    TellModesWeAreEndingIteration( mode as IModeCategory );
                }
            }
        }

        private void TellModesWeAreStartingNewIteration()
        {
            foreach ( var mode in Modes )
            {
                var c = mode as IIterationSensitive;
                if ( c != null )
                {
                    c.IterationStarting( CurrentIteration, TotalIterations );
                }
                c = null;
                TellModesWeAreStartingNewIteration( mode as IModeCategory );
            }
        }

        private void TellModesWeAreStartingNewIteration(IModeCategory node)
        {
            if ( node != null )
            {
                foreach ( var mode in node.Children )
                {
                    var c = mode as IIterationSensitive;
                    if ( c != null )
                    {
                        Status = String.Concat( "Running Iteration ", ( CurrentIteration + 1 ), " of ", TotalIterations, " : Initializing ", mode.ModeName );
                        GetProgress = () => mode.Progress;
                        c.IterationStarting( CurrentIteration, TotalIterations );
                    }
                    c = null;
                    TellModesWeAreStartingNewIteration( mode as IModeCategory );
                }
            }
        }

        private void UnloadNetworkData()
        {
            foreach ( var dataSource in NetworkData )
            {
                dataSource.UnloadData();
            }
        }

        [SubModelInformation( Required = false, Description = "Used for sharing data across modules." )]
        public List<IResource> Resources { get; set; }
    }
}