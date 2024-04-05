﻿/*
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
using TMG.Emme;
using XTMF;

namespace TMG.GTAModel.NetworkAnalysis;

public class ExtractGoIvttMatrix : IEmmeTool
{
    [RunParameter("Scenario", 0, "The number of the Emme scenario from which to extract results.")]
    public int ScenarioNumber;

    [RunParameter("Result Matrix", 5, "The number of the full matrix in which to store extracted results. It will be created if it does not already exist.")]
    public int ResultMatrixNumber;

    private const string ToolName = "tmg.analysis.transit.strategy_analysis.extract_rail_IVTT_matrix";

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
            throw new XTMFRuntimeException(this, "Controller is not a ModellerController!");


        string args = string.Join(" ", ScenarioNumber, "mf" + ResultMatrixNumber);
        string result = null;
        return mc.Run(this, ToolName, args, (p => Progress = p), ref result);
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

}
