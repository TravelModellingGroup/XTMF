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
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.NetworkAnalysis
{
    [ModuleInformation( Name = "Import V3 Boarding Penalties", Description = "Imports boarding penalties into UT3 (line attribute 3)" +
                    " from a file. The file is delimited by ';' and contain the headers 'boarding_penalty' and 'filter_expression'." +
                    " The 'boarding_penalty' column indicates an applied peanlty to be stored in UT3, while the 'filter_expression'" +
                    " indicates the expression used by the Emme Matrix Calculator to filter transit lines." )]
    public class ImportV3BoardingPenalties : IEmmeTool
    {
        private const string ToolName = "tmg.assignment.preprocessing.import_v3_boarding_penalty";
        private const string OldToolName = "TMG2.Assignment.PreProcessing.ImportBoardingPenalties";
        [RunParameter( "Data File", "", typeof( FileFromInputDirectory ), "A absolute filepath to a file which specifies which boarding penalty to apply to particular filter" +
                        " expressions. The file must be delimited by ';' and contain the headers 'boarding_penalty' and 'filter_expression'." +
                        " The 'boarding_penalty' column indicates an applied peanlty to be stored in UT3, while the 'filter_expression'" +
                        " indicates the expression used by the Emme Matrix Calculator to filter transit lines." )]
        public FileFromInputDirectory InputFile;

        [RootModule]
        public IModelSystemTemplate Root;

        [RunParameter( "Scenario Number", 0, "The scenario number in which to process the calculations." )]
        public int ScenarioNumber;

        private static Tuple<byte, byte, byte> _ProgressColour = new( 100, 100, 150 );

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

            var sb = new StringBuilder();
            sb.AppendFormat( "{0} {1}",
                ScenarioNumber, InputFile.GetFileName( Root.InputBaseDirectory ) );
            string result = null;
            if(mc.CheckToolExists(this, ToolName))
            {
                return mc.Run(this, ToolName, sb.ToString(), (p => Progress = p), ref result);
            }
            return mc.Run(this, OldToolName, sb.ToString(), (p => Progress = p), ref result);
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}