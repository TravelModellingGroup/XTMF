/*
    Copyright 2023 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using XTMF;
using System.IO;

namespace TMG.Emme.XTMF_Internal;

[ModuleInformation(Description =
    @"Copy the background traffic @tvph[per_time_period] between scenarios",
    Name = "Copy Background Traffic Between Scenarios"
    )]

public class CopyBackgroundTraffic : IEmmeTool
{

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    [RunParameter("To Scenario Number", 10, "The scenario number to copy to.")]
    public int ToScenarioNumber;

    [RunParameter("From Scenario Numbers", "20,30,40,49", "The scenario number to copy from.")]
    public string FromScenarioNumbers;

    const string ToolName = "tmg.XTMF_internal.copy_background_traffic";

    [RunParameter("Attribute Index Ranges", "0-5,9-23", "Attribute Index Range.")]
    public string AttributeIndexRange;

    [RunParameter("Background Traffic Link Component Extra Attribute", "@tvph", "Time dependent background traffic link extra attribute")]
    public string LinkComponentAttribute;

    public bool Execute(Controller controller)
    {
        var mc = controller as ModellerController;
        if (mc == null)
        {
            throw new XTMFRuntimeException(this, "TMG.Emme.CopyBackgroundTraffic requires the use of EMME Modeller and will not work through command prompt!");
        }

        string ret = null;
        if (!mc.CheckToolExists(this, ToolName))
        {
            throw new XTMFRuntimeException(this, "There was no tool with the name '" + ToolName + "' available in the EMME databank!");
        }

        return mc.Run(this, ToolName, GetParameters(), (p) => Progress = p, ref ret);
    }

    private ModellerControllerParameter[] GetParameters()
    {
        return new[]
        {
            new ModellerControllerParameter("ToScenarioNumber", ToScenarioNumber.ToString()),
            new ModellerControllerParameter("FromScenarioNumbers", FromScenarioNumbers),
            new ModellerControllerParameter("LinkComponentAttribute", LinkComponentAttribute),
            new ModellerControllerParameter("AttributeIndexRange", AttributeIndexRange),
        };
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}

