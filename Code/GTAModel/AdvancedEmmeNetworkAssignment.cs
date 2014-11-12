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
using TMG.Emme;
using XTMF;

namespace TMG.GTAModel
{
    [ModuleInformation( Description = @"This module executes a list of Emme Tools in sequence, at four different points of the Model System
                             execution: <ul>
                                <li><b>ModelSystem Setup:</b> These tools are executed before any zonal information is loaded.
                                <li><b>Initial Run:</b> These tools are executed prior to the first outer-loop iteration but after the zonal data has been loaded.</li>
                                <li><b>Iteration:</b> These tools are executed once per iteration as the last step.</li>
                                <li><b>Final Iteration:</b> These tools are executed after all of the iterations have been completed.</li>
                            </ul>" )]
    public sealed class AdvancedEmmeNetworkAssignment : INetworkAssignment, IDisposable
    {
        [RunParameter( "Execute", true, "Flag for enabling/disabling execution. If set to 'false', Emme will not be launched. Used for debugging." )]
        public bool Execute;

        [SubModelInformation( Description = "Emme Tools executed posterior to the last outer-loop iteration", Required = false )]
        public List<IEmmeTool> FinalIteration;

        [SubModelInformation( Description = "Emme Tools executed prior to the first outer-loop iteration", Required = false )]
        public List<IEmmeTool> InitialRun;

        [SubModelInformation( Description = "Emme Tools executed during each outer-loop iteration.", Required = false )]
        public List<IEmmeTool> IterationRuns;

        [SubModelInformation( Description = "Emme Tools executed before any zonal information is loaded.", Required = false )]
        public List<IEmmeTool> ModelSystemSetup;

        [RunParameter( "Performance Analysis", false, "Flag for logging the performance (runtime) of this module" )]
        public bool PerformanceAnalysis;

        [RunParameter( "Emme Project File", "*.emp", "The path to the Emme project file (.emp)" )]
        public string EmmeProjectFile;

        private Tuple<byte, byte, byte> _progressColour = new Tuple<byte, byte, byte>( 255, 173, 28 );
        private ModellerController Controller;

        private float currentProgress = 0.0f;
        private string currentToolStatus = "";
        private Func<float> getToolProgress = ( () => 0.0f );
        private float progressIncrement = 0.0f;

        ~AdvancedEmmeNetworkAssignment()
        {
            this.Dispose( true );
        }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get
            {
                return this.currentProgress + this.progressIncrement * this.getToolProgress();
            }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return _progressColour; }
        }

        public void RunInitialAssignments()
        {
            this.ExecuteToolList( this.InitialRun );
        }

        public void RunModelSystemSetup()
        {
            this.ExecuteToolList( this.ModelSystemSetup );
        }

        public void RunNetworkAssignment()
        {
            this.ExecuteToolList( this.IterationRuns );
        }

        public void RunPostAssignments()
        {
            this.ExecuteToolList( this.FinalIteration );
            if ( this.Controller != null )
            {
                this.Controller.Dispose();
                this.Controller = null;
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public override string ToString()
        {
            return this.currentToolStatus;
        }

        private void ExecuteToolList(List<IEmmeTool> tools)
        {
            if ( this.Execute )
            {
                if ( this.Controller == null )
                {
                    this.Controller = new ModellerController( this.EmmeProjectFile, this.PerformanceAnalysis );
                }
                this.currentProgress = 0.0f;
                this.progressIncrement = 1.0f / tools.Count;
                foreach ( var tool in tools )
                {
                    this.currentToolStatus = tool.Name;
                    getToolProgress = ( () => tool.Progress );
                    tool.Execute( this.Controller );
                    currentProgress += progressIncrement;
                    getToolProgress = ( () => 0.0f );
                }
            }
        }

        public void Dispose()
        {
            this.Dispose( true );
            GC.SuppressFinalize( true );
        }

        private void Dispose(bool all)
        {
            if ( this.Controller != null )
            {
                this.Controller.Dispose();
                this.Controller = null;
            }
        }
    }
}