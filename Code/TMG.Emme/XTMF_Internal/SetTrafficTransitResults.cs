/*
    Copyright 2015-2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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


namespace TMG.Emme.XTMF_Internal;

[ModuleInformation(Description = "This tool is designed to set, clear or leave transit or traffic results on an EMME Network.")]
public class SetTrafficTransitResults : IEmmeTool
{

    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

    public enum ActionTypes
    {
        DoNothing = 0,
        Assign = 1,
        UnAssign = 2,
    }

    private const string ToolName = "tmg.XTMF_internal.has_transit_traffic_results";

    [RunParameter("Scenario Number", 1, "What scenario would you like to run this for?")]
    public int ScenarioNumber;

    [RunParameter("Has Traffic", ActionTypes.DoNothing, "Set if you want to add TRAFFIC result attribute or not. Options: DoNothing, Assign, UnAssign")]
    public ActionTypes HasTraffic;

    [RunParameter("Has Transit", ActionTypes.DoNothing, "Set if you want to add TRANSIT result attribute or not. Options: DoNothing, Assign, UnAssign.")]
    public ActionTypes HasTransit;

     public bool Execute(Controller controller)
    {
        var mc = controller as ModellerController;
        if (mc == null)
        {
            throw new XTMFRuntimeException(this, "TMG.Emme.XTMF_INTERNAL.SetTrafficTransitResults requires the use of EMME Modeller and will not work through command prompt!");
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
        return
        [
            new ModellerControllerParameter("ScenarioNumber", ScenarioNumber.ToString()),
            new ModellerControllerParameter("HasTraffic", HasTraffic.ToString()),
            new ModellerControllerParameter("HasTransit", HasTransit.ToString()),
            ];
    }
    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
