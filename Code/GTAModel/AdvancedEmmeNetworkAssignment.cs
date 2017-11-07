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
    [ModuleInformation(Description = @"This module executes a list of Emme Tools in sequence, at four different points of the Model System
                             execution: <ul>
                                <li><b>ModelSystem Setup:</b> These tools are executed before any zonal information is loaded.
                                <li><b>Initial Run:</b> These tools are executed prior to the first outer-loop iteration but after the zonal data has been loaded.</li>
                                <li><b>Iteration:</b> These tools are executed once per iteration as the last step.</li>
                                <li><b>Final Iteration:</b> These tools are executed after all of the iterations have been completed.</li>
                            </ul>")]
    public sealed class AdvancedEmmeNetworkAssignment : INetworkAssignment, IDisposable
    {
        [RunParameter("Execute", true, "Flag for enabling/disabling execution. If set to 'false', Emme will not be launched. Used for debugging.")]
        public bool Execute;

        [SubModelInformation(Description = "Emme Tools executed posterior to the last outer-loop iteration", Required = false)]
        public List<IEmmeTool> FinalIteration;

        [SubModelInformation(Description = "Emme Tools executed prior to the first outer-loop iteration", Required = false)]
        public List<IEmmeTool> InitialRun;

        [SubModelInformation(Description = "Emme Tools executed during each outer-loop iteration.", Required = false)]
        public List<IEmmeTool> IterationRuns;

        [SubModelInformation(Description = "Emme Tools executed before any zonal information is loaded.", Required = false)]
        public List<IEmmeTool> ModelSystemSetup;

        [RunParameter("Performance Analysis", false, "Flag for logging the performance (runtime) of this module")]
        public bool PerformanceAnalysis;

        [RunParameter("Emme Project File", "*.emp", "The path to the Emme project file (.emp)")]
        public string EmmeProjectFile;

        private Tuple<byte, byte, byte> _progressColour = new Tuple<byte, byte, byte>(255, 173, 28);
        private ModellerController Controller;

        private float CurrentProgress;
        private string CurrentToolStatus = "";
        private Func<float> GetToolProgress = (() => 0.0f);
        private float ProgressIncrement;

        public string Name
        {
            get;
            set;
        }

        public float Progress => CurrentProgress + ProgressIncrement * GetToolProgress();

        public Tuple<byte, byte, byte> ProgressColour => _progressColour;

        public void RunInitialAssignments()
        {
            ExecuteToolList(InitialRun);
        }

        public void RunModelSystemSetup()
        {
            ExecuteToolList(ModelSystemSetup);
        }

        public void RunNetworkAssignment()
        {
            ExecuteToolList(IterationRuns);
        }

        public void RunPostAssignments()
        {
            ExecuteToolList(FinalIteration);
            if (Controller != null)
            {
                Controller.Dispose();
                Controller = null;
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public override string ToString()
        {
            return CurrentToolStatus;
        }

        private void ExecuteToolList(List<IEmmeTool> tools)
        {
            if (Execute)
            {
                if (Controller == null)
                {
                    Controller = new ModellerController(this, EmmeProjectFile, PerformanceAnalysis);
                }
                CurrentProgress = 0.0f;
                ProgressIncrement = 1.0f / tools.Count;
                foreach (var tool in tools)
                {
                    CurrentToolStatus = tool.Name;
                    GetToolProgress = (() => tool.Progress);
                    tool.Execute(Controller);
                    CurrentProgress += ProgressIncrement;
                    GetToolProgress = (() => 0.0f);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        ~AdvancedEmmeNetworkAssignment()
        {
            Dispose(false);
        }

        private void Dispose(bool all)
        {
            if (all)
            {
                GC.SuppressFinalize(this);
            }
            Controller?.Dispose();
            Controller = null;
        }
    }
}