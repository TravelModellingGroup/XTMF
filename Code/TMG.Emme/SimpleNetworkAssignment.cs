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
using XTMF;

namespace TMG.Emme
{
    [ModuleInformation(Description=
        @"This module provides a basic option for GTAModel/Tasha/Network Estimation Network Assignment models. 
Though not currently used for GTAModel or Tasha anymore it provides access to both the Emme macro system and 
the modeller system, an optional tool/macro to initialize the database with before running the first assignment 
and another tool for execution. This module requires the root module of the model system to be a descendant of 
‘I4StepModel’ in order to access the Network Data." )]
    public class SimpleNetworkAssignment : INetworkAssignment, IDisposable
    {
        [RunParameter( "Auto Network Name", "Auto", "The name of the auto network to use." )]
        public string AutoNetworkName;

        [RunParameter( "Assignment Macro", "TA_test3.mac", "The name of the macro to call to assign to EMME" )]
        public string EmmeAssignmentMacro;

        [RunParameter( "Emme Flows File", @"C:\Users\James\Documents\Project\Macro", "The name of the auto network to use." )]
        public string EmmeFlowsFileName;

        [RunParameter( "Macro Parameters", "1 0 1", "The parameters to the emme macro to call" )]
        public string EmmeParameters;

        [RunParameter( "Emme Project Folder", @"C:\Users\James\Documents\Project\Macro", "The name of the auto network to use." )]
        public string EmmeProjectFolder;

        [RunParameter( "Modeller Performance Test", false, "Store the execution time in the modeller logbook for how long it takes to run the model system." )]
        public bool ModellerPerformanceAnalysis;

        [RootModule]
        public I4StepModel Parent;

        [RunParameter( "Run Emme", true, "Run Emme?" )]
        public bool RunEMME;

        [RunParameter( "Startup Macro Parameters", "", "The parameters to use if we have a start-up macro." )]
        public string StartupMacroArguments;

        [RunParameter( "Startup Macro", "", "The macro to run to start up EMME, leave empty if none." )]
        public string StartupMacroName;

        [RunParameter( "Use Modeller", false, "Switch the Network assignment to use modeller instead of emme macros." )]
        public bool UseModeller;

        private Controller Controller;

        public SimpleNetworkAssignment()
        {
        }

        ~SimpleNetworkAssignment()
        {
            this.Dispose( true );
        }

        public float[][][] Flows { get; set; }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public void RunInitialAssignments()
        {
        }

        public void RunNetworkAssignment()
        {
            INetworkData autoData = GetAutoNetwork();
            if ( this.RunEMME )
            {
                do
                {
                    // If we don't have a working controller try to re-initialize it, and re-startup the system
                    if ( this.Controller == null )
                    {
                        // Pick which controller to use
                        InitializeController();
                        ExecuteStartupMacro();
                    }
                    // Now that the system has been initialized run the macro
                    if ( !Controller.Run( this.EmmeAssignmentMacro, this.EmmeParameters ) )
                    {
                        // if we failed to run the macro, kill the controller and try again
                        this.Controller.Close();
                        this.Controller = null;
                        continue;
                    }
                    // if we have gotten here then we successfully ran, and can continue on
                    break;
                } while ( true );
            }
        }

        public void RunPostAssignments()
        {
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( this.Parent == null )
            {
                error = "The parent model was never set!";
                return false;
            }
            if ( this.AutoNetworkName != null && this.AutoNetworkName != String.Empty )
            {
                INetworkData autoData = null;
                if ( this.Parent.NetworkData == null )
                {
                    error = "There was no Network Data in the Model System to load from!";
                    return false;
                }
                for ( int i = 0; i < this.Parent.NetworkData.Count; i++ )
                {
                    if ( this.Parent.NetworkData[i].NetworkType == this.AutoNetworkName )
                    {
                        autoData = this.Parent.NetworkData[i];
                        break;
                    }
                }
                if ( autoData == null )
                {
                    error = "There was no auto network!";
                    return false;
                }
            }
            return true;
        }

        private void ExecuteStartupMacro()
        {
            if ( !String.IsNullOrEmpty( this.StartupMacroName ) )
            {
                if ( !Controller.Run( this.StartupMacroName, this.StartupMacroArguments ) )
                {
                    throw new XTMFRuntimeException( "We were unable to startup EMME!  Please make sure that you are connected to the EMME license server." );
                }
            }
        }

        private INetworkData GetAutoNetwork()
        {
            INetworkData autoData = null;
            if ( !String.IsNullOrWhiteSpace( this.AutoNetworkName ) )
            {
                try
                {
                    if ( this.Parent.NetworkData != null )
                    {
                        for ( int i = 0; i < this.Parent.NetworkData.Count; i++ )
                        {
                            if ( this.Parent.NetworkData[i].NetworkType == this.AutoNetworkName )
                            {
                                autoData = this.Parent.NetworkData[i];
                                break;
                            }
                        }
                    }
                }
                catch
                {
                }
            }
            return autoData;
        }

        private void InitializeController()
        {
            this.Controller = UseModeller ?
                  new ModellerController( this.EmmeProjectFolder, this.ModellerPerformanceAnalysis )
                : new Controller( this.EmmeProjectFolder, false );
        }

        private void SaveData(INetworkData autoData)
        {
        }


        public void RunModelSystemSetup()
        {

        }

        public void Dispose()
        {
            this.Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose(bool all)
        {
            if ( this.Controller != null )
            {
                this.Controller.Close();
                this.Controller = null;
            }
        }
    }
}