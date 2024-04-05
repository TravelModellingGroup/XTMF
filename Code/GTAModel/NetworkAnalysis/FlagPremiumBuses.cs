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
using System.Text;
using TMG.Emme;
using XTMF;

namespace TMG.GTAModel.NetworkAnalysis;

[ModuleInformation(Name = "Flag Premium Buses", Description = "Flags certain premium bus lines by assigning '1' to line extra " +
                    "attribute '@lflag'. Initializes  @lflag to 0 first.")]
public class FlagPremiumBuses : IEmmeTool
{
    private const string ToolName = "tmg.assignment.preprocessing.flag_premium_buses";
    private const string OldToolName = "TMG2.Assignment.PreProcessing.FlagPremiumBusLines";
    [RunParameter("GO Bus Flag", true, "Flag GO buses true\false.")]
    public bool FlagGo;

    [RunParameter("Premium TTC Bus Flag", true, "Flag premium TTC buses (mode='p') true\false.")]
    public bool FlagPremTTC;

    [RunParameter("VIVA Bus Flag", false, "Flag VIVA buses true\false.")]
    public bool FlagVIVA;

    [Parameter("ZUM Buse Flag", false, "IS NOT CURRENTLY SUPPORTED. DO NOT USE.")]
    public bool FlagZUM;

    [RunParameter("Scenario Number", 0, "The scenario number in which to flag the selected lines.")]
    public int ScenarioNumber;

    private static Tuple<byte, byte, byte> _ProgressColour = new(100, 100, 150);

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

    public bool Execute(Controller controller)
    {
        var mc = controller as ModellerController;
        if (mc == null)
            throw new XTMFRuntimeException(this, "Controller is not a modeller controller!");

        var sb = new StringBuilder();
        sb.AppendFormat("{0} {1} {2} {3} {4}",
            ScenarioNumber, FlagGo, FlagPremTTC, FlagVIVA, FlagZUM);
        string result = null;

        /*
        ScenarioNumber, FlagGO, FlagPremTTC, FlagVIVA, \
             FlagZum
        */
        if (mc.CheckToolExists(this, ToolName))
        {
            return mc.Run(this, ToolName, sb.ToString(), (p => Progress = p), ref result);
        }
        return mc.Run(this, OldToolName, sb.ToString(), (p => Progress = p), ref result);
    }

    public bool RuntimeValidation(ref string error)
    {
        if (FlagZUM)
        {
            error = "Flagging of ZUM bus lines is not currently supported!. Set this variable to 'false'!";
            return false;
        }

        return true;
    }
}