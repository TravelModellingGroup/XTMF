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
using System.Text;
using TMG.Emme;
using XTMF;

namespace TMG.GTAModel.NetworkAnalysis
{
    [ModuleInformation( Name = "Extract Feasibility Matrix",
                        Description = "Extracts a feasibility matrix (where 1 is feasible and 0 is infeasible), based " +
                        "on cut-off values for walking, waiting, and total times." )]
    public class ExtractFeasibilityMatrix : IEmmeTool
    {
        [RunParameter( "Matrix Result Number", 8, "The number of the FULL matrix in which to store the feasibility matrix." )]
        public int MatrixResultNumber;

        [Parameter( "Modes", "blmstuvwy", "The modes used in the transit assignment." )]
        public string Modes;

        [RunParameter( "Scenario Number", 0, "The number of the scenario with transit assignment results to analyze." )]
        public int ScenarioNumber;

        [RunParameter( "Total Time Cutoff", 150.0f, "The threshold of total (wait + walk + ivtt) time, in minutes." )]
        public float TotalCutoff;

        [RunParameter( "Wait Time Cutoff", 40.0f, "The threshold of waiting time, in minutes." )]
        public float WaitCutoff;

        [RunParameter( "Walk Time Cutoff", 40.0f, "The threshold of walking time, in minutes" )]
        public float WalkCutoff;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>( 100, 100, 150 );
        private const string ToolName = "tmg.analysis.transit.strategy_analysis.extract_feasibility_matrix";
        private const string AlternateToolName = "TMG2.Analysis.Transit.Strategies.ExtractFeasibilityMatrix";

        public string Name
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

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if ( mc == null )
                throw new XTMFRuntimeException( "Controller is not a modeller controller!" );

            var sb = new StringBuilder();
            sb.AppendFormat( "{0} {1} {2} {3} {4} {5}",
                ScenarioNumber, WalkCutoff, WaitCutoff, TotalCutoff, Modes, MatrixResultNumber );
            string result = null;

            var toolName = ToolName;
            if (!mc.CheckToolExists(toolName))
            {
                toolName = AlternateToolName;
            }

            return mc.Run(toolName, sb.ToString(), (p => Progress = p), ref result);
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}