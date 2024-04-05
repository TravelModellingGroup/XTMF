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
using XTMF;

namespace TMG.Emme.XTMF_Internal;

[ModuleInformation(Description =
    @"Calculate the background traffic @tvph[per_time_period] to be used be a space time traffic assignemnt tool.",
    Name = "Background Traffic Calculation"
    )]

public class CalculateBackgroundTraffic : IEmmeTool
{

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    [RunParameter("Scenario Number", 1, "The scenario number to execute against.")]
    public int ScenarioNumber;

    const string ToolName = "tmg.XTMF_internal.calculate_background_traffic";

    [RunParameter("Interval Lengths", "60,60,60", "Defines how the assignment time is split into intervals.")]
    public string IntervalLengths;

    [RunParameter("Background Traffic Link Component Extra Attribute", "@tvph", "Time dependent background traffic link extra attribute")]
    public string LinkComponentAttribute;

    [RunParameter("Time Dependent Start Index for Attributes", 1, "Time Dependent Start Indices used to create the alphanumerical attribute name string for attributes in this class.")]
    public int StartIndex;

    [RunParameter("On Road TTFs", "3-128", typeof(RangeSet), "The Transit Time Functions (TTFs) for transit segments that should be applied to the" +
        " road links to reduce capacity for the buses and streetcars in mixed traffic.")]
    public RangeSet OnRoadTTFs;

    public bool Execute(Controller controller)
    {
        var mc = controller as ModellerController ?? throw new XTMFRuntimeException(this, "TMG.Emme.CalculateBackgroundTraffic requires the use of EMME Modeller and will not work through command prompt!");
        string ret = null;
        if (!mc.CheckToolExists(this, ToolName))
        {
            throw new XTMFRuntimeException(this, "There was no tool with the name '" + ToolName + "' available in the EMME databank!");
        }
        return mc.Run(this, ToolName, GetParameters(), (p) => Progress = p, ref ret);
    }


    private ModellerControllerParameter[] GetParameters()
    {
        return
        [
            new ModellerControllerParameter("ScenarioNumber", ScenarioNumber.ToString()),
            new ModellerControllerParameter("IntervalLengths", IntervalLengths.ToString(CultureInfo.InvariantCulture)),
            new ModellerControllerParameter("LinkComponentAttribute", LinkComponentAttribute),
            new ModellerControllerParameter("StartIndex", StartIndex.ToString(CultureInfo.InvariantCulture)),
            new ModellerControllerParameter("OnRoadTTFRanges", OnRoadTTFs.ToString()),
        ];
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
