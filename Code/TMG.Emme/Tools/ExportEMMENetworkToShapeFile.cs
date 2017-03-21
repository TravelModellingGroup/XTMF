/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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

namespace TMG.Emme.Tools
{
    public enum TransitShape
    {
        LINES = 0,
        SEGMENTS = 1,
        LINES_AND_SEGMENTS = 2
    }


    [ModuleInformation(
        Description =
            "This module calls export_network_shapfile tool of TMG Toolbox. " +
        "The tool exports network data from an EMME scenario to a specified shape file."
    )]
    public class ExportEMMENetworkToShapeFile : IEmmeTool
    {
        private const string ToolName = "tmg.input_output.export_network_shapefile";

        [RunParameter("Transit Shape", "SEGMENTS", typeof(Tools.TransitShape), "Type of geometry / transhit shape to export.")]
        public TransitShape TransitShape;

        [RunParameter("Scenario", 0,
            "The number of the Emme scenario to use, if the project has multiple scenarios with different zone systems. Not used otherwise."
        )] public int ScenarioNumber;

        [SubModelInformation(Description = "Output File Path", Required = true)] public FileLocation Filepath;

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException("Controller is not a ModellerController!");
            }

            Console.WriteLine("Running Export EMME network shape file, export path = " + Filepath.GetFilePath());


            return mc.Run(ToolName,
                new[]
                {
                    new ModellerControllerParameter("xtmf_exportPath", Filepath.GetFilePath()),
                    new ModellerControllerParameter("xtmf_transitShapes", TransitShape.ToString()),
                    new ModellerControllerParameter("xtmf_scenario", ScenarioNumber.ToString())
                });
        }


        public bool RuntimeValidation(ref string error)
        {
            if (string.IsNullOrEmpty(Filepath.GetFilePath()))
            {
                error = "Export path cannot be null or empty.";
                return true;
            }

            return false;
        }

        public string Name { get; set; }
        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get; }
    }
}
