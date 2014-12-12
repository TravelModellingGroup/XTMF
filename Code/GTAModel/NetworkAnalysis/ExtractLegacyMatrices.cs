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
using System.IO;
using System.Text;
using TMG.Emme;
using XTMF;

namespace TMG.GTAModel.NetworkAnalysis
{
    [ModuleInformation( Description = "Extracts average in-vehicle, walking, waiting, boarding time, and cost matrices from" +
            " a legacy fare-based assignment. Matrices will be multiplied by a feasibility matrices (where 0 = infeasible and 1 = feasible)." )]
    public class ExtractLegacyMatrices : IEmmeTool
    {
        [RunParameter( "Boarding Matrix Number", 0, "The number of the new or existing matrix to extract boarding times into. Set to 0 to forego extraction." )]
        public int BoardingMatrixNumber;

        [RunParameter( "Cost Matrix Number", 0, "The number of the new or existing matrix to extract transit costs into. Set to 0 to forego extraction." )]
        public int CostMatixNumber;

        [RunParameter( "Fare Perception", 0.0f, "The fare perception used in the transit assignment. Set to 0 to dsiable cost matrix extraction." )]
        public float FarePerception;

        [RunParameter( "In Vehicle Matrix Number", 0, "The number of the new or existing matrix to extract in vehicle times into. Set to 0 to forego extraction." )]
        public int InVehicleMatrixNumber;

        [RunParameter( "Mode List", "", "String of modes used in the transit assignment." )]
        public string ModeString;

        [RootModule]
        public IModelSystemTemplate Root;

        [RunParameter( "Scenario Number", 0, "The number of the Emme scenario with transit assignment results." )]
        public int ScenarioNumber;

        [RunParameter( "Total Time Cutoff", 150.0f, "Maximum total transit time for feasible transit OD pairs." )]
        public float TotalTimeCutoff;

        [RunParameter( "Wait Matrix Number", 0, "The number of the new or existing matrix to extract waiting times into. Set to 0 to forego extraction." )]
        public int WaitMtrixNumber;

        [RunParameter( "Wait Time Cutoff", 40.0f, "Maximum wait time for feasible transit OD pairs." )]
        public float WaitTimeCutoff;

        [RunParameter( "Walk Matrix Number", 0, "The number of the new or existing matrix to extract walk times into. Set to 0 to forego extraction." )]
        public int WalkMatrixNumber;

        [RunParameter( "Walk Time Cutoff", 40.0f, "Maximum walk time for feasible transit OD pairs." )]
        public float WalkTimeCutoff;

        private Tuple<byte, byte, byte> _progressColour = new Tuple<byte, byte, byte>( 255, 173, 28 );
        private const string ToolName = "tmg.analysis.transit.strategy_analysis.extract_constrained_LOS_matrices";
        private const string AlternateToolName = "TMG2.Analysis.Transit.Strategies.ExtractConstrainedLOSMatrices";

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            private set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return _progressColour; }
        }

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if ( mc == null )
                throw new XTMFRuntimeException( "Controller is not a modeller controller!" );

            var runName = Path.GetFileName( Directory.GetCurrentDirectory() );

            var sb = new StringBuilder();
            sb.AppendFormat( "{0} {1} {2} {3} {4}", this.ScenarioNumber, this.ModeString, this.WalkTimeCutoff, this.WaitTimeCutoff,
                this.TotalTimeCutoff );
            sb.AppendFormat( this.InVehicleMatrixNumber == 0 ? " null" : " mf{0}", this.InVehicleMatrixNumber );
            sb.AppendFormat( this.CostMatixNumber == 0 ? " null" : " mf{0}", this.CostMatixNumber );
            sb.AppendFormat( this.WalkMatrixNumber == 0 ? " null" : " mf{0}", this.WalkMatrixNumber );
            sb.AppendFormat( this.WaitMtrixNumber == 0 ? " null" : " mf{0}", this.WaitMtrixNumber );
            sb.AppendFormat( this.BoardingMatrixNumber == 0 ? " null" : " mf{0}", this.BoardingMatrixNumber );
            sb.AppendFormat( " {0} \"{1}\"", this.FarePerception, runName );

            var toolName = ToolName;
            if (!mc.CheckToolExists(toolName))
            {
                toolName = AlternateToolName;
            }

            string result = null;
            return mc.Run(toolName, sb.ToString(), (p => this.Progress = p), ref result);

            /*
            Call args:
             *
             * xtmf_ScenarioNumber, xtmf_ModeString,
                 WalkTimeCutoff, WaitTimeCutoff, TotalTimeCutoff,
                 InVehicleTimeMatrixId, CostMatrixId,
                 WalkTimeMatrixId, WaitTimeMatrixId, BoardingTimeMatrixId,
                 FarePerception, RunTitle
             *
             * xtmf_ScenarioNumber, xtmf_ModeString,
                 WalkTimeCutoff, WaitTimeCutoff, TotalTimeCutoff,
                 InVehicleTimeMatrixId, CostMatrixId,
                 WalkTimeMatrixId, WaitTimeMatrixId, BoardingTimeMatrixId,
                 FarePerception, RunTitle
             *
            */
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}