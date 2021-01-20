/*
    Copyright 2020 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Threading.Tasks;
using TMG.Input;
using XTMF;

namespace TMG.Emme.XTMF_Internal
{
    [ModuleInformation(Description = "This module allows the model system to load in a VDF batch file into an EMME project.  This" +
        " is useful after loading in a NWP to modify it without editing the NWP directly.")]
    public sealed class ImportFunctionBatchFile : IEmmeTool
    {
        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

        [SubModelInformation(Required = true, Description = "The location of the transaction file to load.")]
        public FileLocation TransactionFile;

        [RunParameter("Scenario Number", 0, "The scenario number to use.")]
        public int ScenarioNumber;

        public bool Execute(Controller controller)
        {
            if (controller is ModellerController mc)
            {
                return mc.Run(this, "tmg.XTMF_internal.import_function_batch_file", new ModellerControllerParameter[]
                {
                    new ModellerControllerParameter("batch_file", TransactionFile),
                    new ModellerControllerParameter("scenario_number", ScenarioNumber.ToString()),
                });
            }
            else
            {
                throw new XTMFRuntimeException(this, "Controller is not a ModellerController!");
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            if(ScenarioNumber <= 0)
            {
                error = $"The scenario number {ScenarioNumber} is invalid!";
                return false;
            }
            return true;
        }
    }
}
