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
using XTMF;
using TMG.Input;
using System.IO;

namespace TMG.Emme;

// ReSharper disable InconsistentNaming
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

    [RunParameter("Scenario Name", "", "The name of the Emme scenario to create.")]
    public string ScenarioName;

    [RunParameter("Function Conflict Option", FunctionConflictOption.RAISE, "Option to deal with function definition conflicts. For example, if "
        + "FT1 is defined as 'length / speed * 60' in the current Emmebank, but defined as 'length / us1 * 60' in the NWP's functions file."
        + "One of RAISE, PRESERVE or OVERWRITE. RAISE (default) raises an error if "
        + "any conflict is detected. PRESERVE keeps the definitions that already exist in the Emmebank (no modification). OVERWRITE modifies "
        + "the definitions to match what is given in the NWP file.")]
    public FunctionConflictOption ConflictOption;

    [SubModelInformation(Required = true, Description = "Network Package File")]
    public FileLocation NetworkPackage;

    private static Tuple<byte, byte, byte> _ProgressColour = new(100, 100, 150);
    private const string _ToolName = "tmg.input_output.import_network_package";


    [RunParameter("Add Functions", true, "Flag to specify whether non-conflicting functions should be added on import.")]
    public bool AddFunctions;

    public bool Execute(Controller controller)
    {
        var mc = controller as ModellerController ?? throw new XTMFRuntimeException(this, "Controller is not a ModellerController!");
        Console.WriteLine("Importing network into scenario " + ScenarioId.ToString() + " from file " + Path.GetFullPath(NetworkPackage.GetFilePath()));


        return mc.Run(this, _ToolName,
            [
                new ModellerControllerParameter("NetworkPackageFile", Path.GetFullPath(NetworkPackage.GetFilePath())),
                new ModellerControllerParameter("ScenarioId", ScenarioId.ToString()),
                new ModellerControllerParameter("ConflictOption", ConflictOption.ToString()),
                new ModellerControllerParameter("AddFunction", AddFunctions.ToString()),
                new ModellerControllerParameter("ScenarioName", ScenarioName.ToString())
            ]);
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
        if (ScenarioName == "")
            ScenarioName = " ";
        return true;
    }
}
