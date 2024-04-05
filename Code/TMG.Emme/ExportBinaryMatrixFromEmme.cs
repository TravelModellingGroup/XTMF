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
using TMG.Input;
using XTMF;

namespace TMG.Emme
{
    public class ExportBinaryMatrixFromEmme : IEmmeTool
    {
        [Parameter("Matrix Type", 4, "The type of the matrix to export. 1 for SCALAR, 2 for ORIGIN, 3 for DESTINATION, and 4 for FULL (the default).")]
        public int MatrixType;

        [RunParameter("Matrix Number", 0, "The number of the matrix to extract from Emme.")]
        public int MatrixNumber;

        [RunParameter("Scenario", 0, "The number of the Emme scenario to use, if the project has multiple scenarios with different zone systems. Not used otherwise.")]
        public int ScenarioNumber;

        [SubModelInformation(Description = "Binary File Path", Required = true)]
        public FileLocation Filepath;

        private static Tuple<byte, byte, byte> _ProgressColour = new(100, 100, 150);

        private const string ToolName = "tmg.input_output.export_binary_matrix";
        private const string OldToolName = "TMG2.IO.ExportBinaryMatrix";

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if(mc == null)
                throw new XTMFRuntimeException(this, "Controller is not a ModellerController!");

            var args = string.Join(" ", MatrixType, MatrixNumber, "\"" + Path.GetFullPath(Filepath.GetFilePath()) + "\"", ScenarioNumber);

            /*
            
                def __call__(self, MatrixType, MatrixNumber, FileName, ScenarioNumber):
            */

            var result = "";
            if(mc.CheckToolExists(this, ToolName))
            {
                return mc.Run(this, ToolName, args, (p => Progress = p), ref result);
            }
            else
            {
                return mc.Run(this, OldToolName, args, (p => Progress = p), ref result);
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
            if(MatrixType > 4 & MatrixType < 1)
            {
                error = "Matrix type " + MatrixType.ToString() + " is not a valid matrix type." +
                    " Valid types are 1 for SCALAR, 2 for ORIGIN, 3 for DESTINATION, and 4 for FULL matrices.";
                return false;
            }
            return true;
        }

    }
}
