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

[ModuleInformation( Name = "Extract Transit Travel Time Matrices",
                    Description = "Extracts average in-vehicle, walking, waiting, and boarding time" +
                                "matrices from a strategy-based assignment." )]
public class ExtractTransitTravelTimes : IEmmeTool
{
    [RunParameter( "Boarding Matrix Number", 4, "The number of the FULL matrix to store average total boarding times" )]
    public int BoardingMatrixNumber;

    [RunParameter( "IVTT Matrix Number", 1, "The number of the FULL matrix to store average total in-vehicle travel times." )]
    public int IVTTMatrixNumber;

    [RunParameter( "Modes", "blmstuvwy", "A list of single-character modes used in the assignment." )]
    public string ModeString;

    [RunParameter( "Scenario Number", 0, "The scenario number with transit results." )]
    public int ScenarioNumber;

    [RunParameter( "Wait Matrix Number", 3, "The number of the FULL matrix to store average total waiting times." )]
    public int WaitMatrixNumber;

    [RunParameter( "Walk Matrix Number", 2, "The number of the FULL matrix to store average total walk (auxilliary transit) times." )]
    public int WalkMatrixNumber;

    private static Tuple<byte, byte, byte> _ProgressColour = new( 100, 100, 150 );
    private const string ToolName = "tmg.analysis.transit.strategy_analysis.extract_LOS_matrices";
    private const string AlternateToolName = "TMG2.Analysis.Transit.Strategies.ExtractTravelTimeMatrices";

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
        var mc = controller as ModellerController ?? throw new XTMFRuntimeException(this, "Controller is not a modeller controller!" );
        var args = new StringBuilder();
        args.AppendFormat( "{0} {1} {2} {3} {4} {5}",
            ScenarioNumber, ModeString, IVTTMatrixNumber, WalkMatrixNumber, WaitMatrixNumber, BoardingMatrixNumber );

        var toolName = ToolName;
        if (!mc.CheckToolExists(this, toolName))
        {
            toolName = AlternateToolName;
        }

        string result = null;
        return mc.Run(this, toolName, args.ToString(), (p => Progress = p), ref result);
    }

    public bool RuntimeValidation(ref string error)
    {
        //No checking required
        return true;
    }
}