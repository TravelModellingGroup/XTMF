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
using System.IO;
using TMG.Input;
using XTMF;

namespace TMG.Emme.XTMF_Internal;

[ModuleInformation(Description =
@"This module will go through a Multiclass Transit assignment and store the boardings by class aggregated 
by the Line Aggregation File.  It will be saved into the Save To Directory/[className].csv"
    )]
public class ReturnBoardingsMulticlass : IEmmeTool
{
    [RunParameter("Scenario Number", 0, "The scenario number to get the boardings from.")]
    public int ScenarioNumber;

    [SubModelInformation(Required = true, Description = "The line aggregation file to apply")]
    public FileLocation LineAggregationFile;

    [SubModelInformation(Required = true, Description = "The directory to save the results into")]
    public FileLocation SaveToDirectory;

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    public bool Execute(Controller controller)
    {
        var mc = controller as ModellerController ?? throw new XTMFRuntimeException(this, "Controller is not a ModellerController!");
        return mc.Run(this, "tmg.XTMF_internal.return_boardings_multiclass",
            [
                new ModellerControllerParameter("xtmf_ScenarioNumber", ScenarioNumber.ToString()),
                new ModellerControllerParameter("xtmf_LineAggregationFile", Path.GetFullPath(LineAggregationFile.GetFilePath())),
                new ModellerControllerParameter("xtmf_OutputDirectory", Path.GetFullPath(SaveToDirectory.GetFilePath()))
            ]);
    }

    public bool RuntimeValidation(ref string error)
    {
        if (ScenarioNumber <= 0)
        {
            error = "The scenario number '" + ScenarioNumber
                + "' is an invalid scenario number!";
            return false;
        }
        return true;
    }
}
