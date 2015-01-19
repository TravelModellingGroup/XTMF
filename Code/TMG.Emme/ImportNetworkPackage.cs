/*
    Copyright 2014-2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.Text;
using XTMF;
using TMG.Input;
using System.IO;

namespace TMG.Emme
{
    public enum FunctionConflictOption
    {
        RAISE,
        PRESERVE,
        OVERWRITE
    }

    public class ImportNetworkPackage : IEmmeTool
    {
        [RunParameter("Scenario Id", 0, "The number of the new Emme scenario to create.")]
        public int ScenarioId;

        [RunParameter("Function Conflict Option", FunctionConflictOption.RAISE, "Option to deal with function definition conflicts. For example, if "
            + "FT1 is defined as 'length / speed * 60' in the current Emmebank, but defined as 'length / us1 * 60' in the NWP's functions file."
            + "One of RAISE, PRESERVE, or OVERWRITE. RAISE (default) raises an error if "
            + "any conflict is detected. PRESERVE keeps the definitions that already exist in the Emmebank (no modification). OVERWRITE modifies "
            + "the definitions to match what is given in the NWP file.")]
        public FunctionConflictOption ConflictOption;

        [SubModelInformation(Required = true, Description = "Network Package File")]
        public FileLocation NetworkPackage;

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);
        private const string _ToolName = "tmg.input_output.import_network_package";

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if(mc == null)
                throw new XTMFRuntimeException("Controller is not a ModellerController!");

            var args = string.Join(" ", "\""+Path.GetFullPath(NetworkPackage.GetFilePath())+"\"",
                                    ScenarioId, this.ConflictOption.ToString());

            Console.WriteLine("Importing network into scenario " + ScenarioId.ToString() + " from file " + Path.GetFullPath(NetworkPackage.GetFilePath()));

            var result = "";
            return mc.Run(_ToolName, args, (p => Progress = p), ref result);
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
