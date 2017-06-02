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
using TMG.Input;
using XTMF;

namespace TMG.Emme
{
    public class ImportBinaryMatrixIntoEmme : IEmmeTool
    {
        [SubModelInformation(Description = "Demand Matrix File", Required = true)]
        public FileLocation MatrixFile;

        [RunParameter("Scenario", 0, "The number of the Emme scenario")]
        public int ScenarioNumber;

        [RunParameter("Matrix Number", 0, "The matrix number that this will be assigned to.")]
        public int MatrixNumber;

        [Parameter("Matrix Type", 4, "The type of the matrix to export. 1 for SCALAR, 2 for ORIGIN, 3 for DESTINATION, and 4 for FULL (the default).")]
        public int MatrixType;

        [RunParameter("Description", "From XTMF", "A description of the matrix.")]
        public string Description;

        private const string ToolName = "tmg.input_output.import_binary_matrix";
        private const string OldToolName = "TMG2.IO.ImportBinaryMatrix";

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>( 100, 100, 150 );


        /*
        [1:27:43 PM] Peter Kucirek:    def __call__(self, xtmf_MatrixType, xtmf_MatrixNumber, ImportFile, xtmf_ScenarioNumber,
                 MatrixDescription):
        */
        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if ( mc == null )
                throw new XTMFRuntimeException( "Controller is not a ModellerController!" );

            var args = string.Join( " ", MatrixType.ToString(),
                MatrixNumber.ToString(),
                "\"" + MatrixFile.GetFilePath() + "\"",
                                        ScenarioNumber,
                                        "\"" + Description.Replace( "\"", "\'" ) + "\"" );

            Console.WriteLine( "Importing matrix into scenario " + ScenarioNumber.ToString() + " from file " + MatrixFile.GetFilePath() );

            var result = "";
            if(mc.CheckToolExists(ToolName))
            {
                return mc.Run(ToolName, args, (p => Progress = p), ref result);
            }
            else
            {
                return mc.Run(OldToolName, args, (p => Progress = p), ref result);
            }
        }

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

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

    }
}
