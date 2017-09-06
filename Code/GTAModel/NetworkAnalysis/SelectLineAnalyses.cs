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
    [ModuleInformation( Name = "Select Line Analyses",
                        Description = "Extracts average in-vehicle, walking, waiting, and boarding time " +
                        "matrices from a strategy-based assignment, for matrices flagged by attribute " +
                        "@lflag. /r/n/r/nTo calculate costs, the network must have transfer fares " +
                        "stored in @tfare and in-line fares stored in us3." )]
    public class SelectLineAnalyses : IEmmeTool
    {
        [RunParameter( "Boarding Matrix NUmber", 4, "The number of the FULL matrix in which to store average total boarding times." +
                        " To disable the saving of this matrix, enter a value of '0'." )]
        public int BoardingMatrixNumber;

        [RunParameter( "Cost Matrix Number", 8, "The number of the FULL matrix in which to store average costs (fares)." +
                        " To disable the saving of this matrix, enter a value of '0'." )]
        public int CostMatrixNUmber;

        [RunParameter( "Fare Perception", 0.0f, "The fare perception used in the assignment." )]
        public float FarePerception;

        [RunParameter( "IVTT Matrix Number", 1, "The number of the FULL matrix in which to store average total in-vehicle travel times." +
                        " To disable the saving of this matrix, enter a value of '0'." )]
        public int IVTTMatrixNumber;

        [Parameter( "Modes", "bglmpstuvwy", "A string of modes to analyze." )]
        public string Modes;

        [RunParameter( "Scenario Number", 0, "The number of the scenario with transit assignment (or FBTA) results to analyze." )]
        public int ScenarioNumber;

        [RunParameter( "Total Time Cutoff", 150.0f, "Total time cutoff value for feasibility." )]
        public float TotalTimeCutoff;

        [RunParameter( "Wait Matrix Number", 3, "The number of the FULL matrix in which to store average total wait times." +
                        " To disable the saving of this matrix, enter a value of '0'." )]
        public int WaitMatrixNumber;

        [RunParameter( "Wait Time Cutoff", 40.0f, "Wait time cutoff value for feasibility." )]
        public float WaitTimeCutoff;

        [RunParameter( "Walk Matrix Number", 2, "The number of the FULL matrix in which to store average total walk times." +
                        " To disable the saving of this matrix, enter a value of '0'." )]
        public int WalkMatrixNumber;

        [RunParameter( "Walk Time Cutoff", 40.0f, "Walk time cutoff value for feasibility." )]
        public float WalkTimeCutoff;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>( 100, 100, 150 );
        private const string ToolName = "tmg.analysis.transit.strategy_analysis.select_line_analyses";
        private const string AlternateToolName = "TMG2.Analysis.Transit.Strategies.ExtractSelectLineMatrices";

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
                throw new XTMFRuntimeException(this, "Controller is not a modeller controller!" );

            var args = new StringBuilder();
            args.AppendFormat( "{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10}",
                ScenarioNumber, Modes, IVTTMatrixNumber, WalkMatrixNumber, WaitMatrixNumber, BoardingMatrixNumber, CostMatrixNUmber,
                WalkTimeCutoff, WaitTimeCutoff, TotalTimeCutoff, FarePerception );

            var toolName = ToolName;
            if (!mc.CheckToolExists(this, toolName))
            {
                toolName = AlternateToolName;
            }
            
            string result = null;
            return mc.Run(this, toolName, args.ToString(), (p => Progress = p), ref result);
        }

        public bool RuntimeValidation(ref string error)
        {
            //No checking required
            return true;
        }
    }
}