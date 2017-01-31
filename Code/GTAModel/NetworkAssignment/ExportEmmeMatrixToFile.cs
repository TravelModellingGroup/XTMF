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
using TMG.Emme;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.NetworkAssignment
{
    [ModuleInformation(Name = "Export EMME Matrix To File", Description = "Basic tool for extracting matrix results from Emme. This module is considered deprecated after Emme 4.04")]
    public class ExportEmmeMatrixToFile : IEmmeTool
    {
        private const string _ToolName = "tmg.XTMF_internal.export_matrix_batch_file";
        private const string _OldToolName = "TMG2.XTMF.ExportMatrix";
        [SubModelInformation(Required = true, Description = "The location to save the matrix (.311) to.")]
        public FileLocation FileName;

        [RunParameter("Matrix Number", 1, "The number of the FULL matrix to export.")]
        public int MatrixNumber;

        [RootModule]
        public IModelSystemTemplate Root;

        [RunParameter("Scenario Number", 1, "The number of the scenario to read matrix data from. This is required by Emme for databanks in which multiple scenarios are defined with" +
                " differing zone systems.")]
        public int ScenarioNumber;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>( 50, 51, 50 ); }
        }

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if ( mc == null )
                throw new XTMFRuntimeException( "Controller is not a modeller controller!" );

            string filepath = Path.GetFullPath( FileName );
            if(mc.CheckToolExists(_ToolName))
            {
                return mc.Run(_ToolName, MatrixNumber + " \"" + filepath + "\"" + ScenarioNumber);
            }
            else
            {
                return mc.Run(_OldToolName, MatrixNumber + " \"" + filepath + "\"" + ScenarioNumber);
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
