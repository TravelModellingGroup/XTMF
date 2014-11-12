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
                var progressFunction = this.GetProgress;
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
            this.ExitRequested = true;
            this.Status = "Exiting after current operation has completed";
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( this.Purpose == null || this.Purpose.Count == 0 )
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
            this.CurrentIteration = 0;
            this.Status = "Running Model System Setup";
            this.GetCurrentStatus = ( () => this.NetworkAssignment.ToString() );
            this.GetProgress = ( () => this.NetworkAssignment.Progress );
            this.NetworkAssignment.RunModelSystemSetup();
            if ( this.ExitRequested ) { return; }
            LoadZoneData();
            if ( this.ExitRequested ) { return; }
            this.Status = "Running Initial Network Assignment";
            this.GetCurrentStatus = ( () => this.NetworkAssignment.ToString() );
            this.GetProgress = ( () => this.NetworkAssignment.Progress );
            this.NetworkAssignment.RunInitialAssignments();
            this.GetCurrentStatus = ( () => "" );
            if ( this.ExitRequested ) { return; }
            LoadNetworkData();
            if ( this.ExitRequested ) { return; }
            InitializePopulation();
            if ( this.ExitRequested ) { return; }
            for ( int iteration = 0; iteration < this.TotalIterations; iteration++ )
            {
                if ( this.ExitRequested ) { return; }
                foreach ( var module in PreIteration )
                {
                    this.Status = String.Concat( "Running Iteration ", ( iteration + 1 ), " of ", this.TotalIterations, " : ", module.ToString() );
                    this.GetProgress = ( () => module.Progress );
                    module.Start();
                }
                if ( iteration == 0 )
                {
                    TellModesWeAreStartingNewIteration();
                    this.ReProcessWorkSchoolZones();
                }
                var purposeLength = this.Purpose.Count;
                for ( int i = 0; i < purposeLength; i++ )
                {
                    this.Status = String.Concat( "Running Iteration ", ( iteration + 1 ), " of ", this.TotalIterations, " : ", this.Purpose[i].PurposeName );
                    this.CurrentPurpose = i;
                    this.GetProgress = ( () => this.Purpose[i].Progress );
                    this.Purpose[i].Run();
                    if ( this.ExitRequested ) { return; }
                }
                this.GetCurrentStatus = ( () => this.NetworkAssignment.ToString() );
                this.GetProgress = ( () => this.NetworkAssignment.Progress );
                this.Status = String.Concat( "Running Network Assignment (", ( iteration + 1 ), " of ", this.TotalIterations, ")" );
                UnloadNetworkData();
                this.NetworkAssignment.RunNetworkAssignment();
                this.GetCurrentStatus = ( () => "" );
                TellModesWeAreEndingIteration();
                // if it isn't the last iteration reprocess the locations that people work and go to school
                if ( iteration < this.TotalIterations - 1 )
                {
                    this.CurrentIteration++;
                    this.LoadNetworkData();
                    TellModesWeAreStartingNewIteration();
                    this.ReProcessWorkSchoolZones();
                }
            }
            this.CurrentIteration = this.TotalIterations;
            if ( this.ExitRequested ) { return; }
            this.GetCurrentStatus = ( () => this.NetworkAssignment.ToString() );
            this.GetProgress = ( () => this.NetworkAssignment.Progress );
            this.Status = "Running Final Network Assignment";
            this.NetworkAssignment.RunPostAssignments();
            this.GetCurrentStatus = ( () => "" );
            if ( this.ExitRequested ) { return; }
            this.Status = "Running Post Run Modules";
            RunPostRunModules();
            this.GetProgress = ( () => 1f );
            this.Status = "Shutting Down";
            this.Population = null;
            this.Demographics.UnloadData();
            this.ZoneSystem.UnloadData();
        }

        public override string ToString()
        {
            return String.Concat( this.Status, ": ", this.GetCurrentStatus() );
        }

        private string GetFullPath(string localPath)
        {
            var fullPath = localPath;
            if ( !System.IO.Path.IsPathRooted( fullPath ) )
            {
                fullPath = System.IO.Path.Combine( this.InputBaseDirectory, fullPath );
            }
            return fullPath;
        }

        private void InitializePopulation()
        {
            this.Status = "Loading Population";
            this.GetProgress = ( () => this.Population.Progress );
            this.Population.Load();
        }

        private void LoadNetworkData()
        {
            foreach ( var dataSource in this.NetworkData )
            {
                this.Status = String.Concat( "Loading ", dataSource.NetworkType, " Network" );
                dataSource.LoadData();
            }
        }

        private void LoadZoneData()
        {
            this.Status = "Loading Zone System";
            this.ZoneSystem.LoadData();
            this.Status = "Loading Demographics";
            this.Demographics.LoadData();
        }

        private void ReProcessWorkSchoolZones()
        {
            if ( this.AssignWorkZones != null )
            {
                this.Status = "Assigning Work Zones: Iteration " + ( this.CurrentIteration + 1 ) + " of " + this.TotalIterations;
                this.GetProgress = ( () => this.AssignWorkZones.Progress );
                this.AssignWorkZones.Start();
            }
            if ( this.AssignSchoolZones != null )
            {
                this.Status = "Assigning School Zones: Iteration " + ( this.CurrentIteration + 1 ) + " of " + this.TotalIterations;
                this.GetProgress = ( () => this.AssignSchoolZones.Progress );
                this.AssignSchoolZones.Start();
            }
        }

        private void RunPostRunModules()
        {
            if ( this.PostRun != null )
            {
                int complete = 0;
                this.GetProgress = ( () => complete / (float)this.PostRun.Count );
                // launch all of the post processing in parallel
                if ( this.ParallelPostProcessing )
                {
                    System.Threading.Tasks.Parallel.ForEach( this.PostRun,
                        delegate(ISelfContainedModule module)
                        {
                            module.Start();
                            System.Threading.Interlocked.Increment( ref complete );
                        } );
                }
                else
                {
                    foreach ( var module in this.PostRun )
                    {
                        module.Start();
                        complete++;
                    }
                }
            }
        }

        private void TellModesWeAreEndingIteration()
        {
            foreach ( var mode in this.Modes )
            {
                var c = mode as IIterationSensitive;
                if ( c != null )
                {
                    c.IterationEnding( this.CurrentIteration, this.TotalIterations );
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
                        c.IterationEnding( this.CurrentIteration, this.TotalIterations );
                    }
                    c = null;
                    TellModesWeAreEndingIteration( mode as IModeCategory );
                }
            }
        }

        private void TellModesWeAreStartingNewIteration()
        {
            foreach ( var mode in this.Modes )
            {
                var c = mode as IIterationSensitive;
                if ( c != null )
                {
                    c.IterationStarting( this.CurrentIteration, this.TotalIterations );
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
                        this.Status = String.Concat( "Running Iteration ", ( this.CurrentIteration + 1 ), " of ", this.TotalIterations, " : Initializing ", mode.ModeName );
                        this.GetProgress = () => mode.Progress;
                        c.IterationStarting( this.CurrentIteration, this.TotalIterations );
                    }
                    c = null;
                    TellModesWeAreStartingNewIteration( mode as IModeCategory );
                }
            }
        }

        private void UnloadNetworkData()
        {
            foreach ( var dataSource in this.NetworkData )
            {
                dataSource.UnloadData();
            }
        }

        [SubModelInformation( Required = false, Description = "Used for sharing data across modules." )]
        public List<IResource> Resources { get; set; }
    }
}