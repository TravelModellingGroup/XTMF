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
using System.Collections.Generic;
using Datastructure;
using TMG.Emme;
using TMG.Estimation;
using TMG.Input;
using XTMF;

namespace TMG.NetworkEstimation;

// ReSharper disable once InconsistentNaming
public class CalcRMSEofBoardingsAM : IEmmeTool
{

    [RootModule]
    public IEstimationClientModelSystem Root;

    [RunParameter("Scenario", 0, "The number of the Emme scenario")]
    public int ScenarioNumber;

    [SubModelInformation(Description= "Observed Boardings File", Required= true)]
    public FileLocation ObservedBoardingsFile;

    [SubModelInformation(Description = "Line Aggregation File", Required = true)]
    public FileLocation LineAggregationFile;

    [RunParameter("Check Aggregation File", true, "Flag to report differences between the aggregation file and the network.")]
    public bool CheckAggregationFile;

    private const string ToolName = "TMG2.XTMF.returnBoardings";
    private static Tuple<byte, byte, byte> _ProgressColour = new(100, 100, 150);

    public bool Execute(Controller controller)
    {
        var mc = controller as ModellerController;
        if (mc == null)
        {
            throw new XTMFRuntimeException(this, "Controller is not a ModellerController");
        }

        var args = string.Join(" ", ScenarioNumber, LineAggregationFile.GetFilePath(), CheckAggregationFile);
        string result = "";
        mc.Run(this, ToolName, args, (p => Progress = p), ref result);

        var modelResults = ParseResults(result);
        var observations = LoadObservedBoardingsFile();

        CalcFitness(observations, modelResults);

        return true;
    }

    private Dictionary<string, float> ParseResults(string pythonDictionary)
    {
        var result = new Dictionary<string, float>();

        var cleaned = pythonDictionary.Replace("{", "").Replace("}", "");
        var cells = cleaned.Split(',');
        foreach (var cell in cells)
        {
            var pair = cell.Split(':');
            var lineId = pair[0].Replace("'", "").Trim();
            float boardings = float.Parse(pair[1]);
            result[lineId] = boardings;
        }
        return result;
    }

    private Dictionary<string, float> LoadObservedBoardingsFile()
    {
        var result = new Dictionary<string, float>();

        using (CsvReader reader = new(ObservedBoardingsFile.GetFilePath()))
        {
            reader.LoadLine(); //Skip the first line                
            while (reader.LoadLine(out int numCol))
            {
                reader.Get(out string lineId, 0);

                if (string.IsNullOrWhiteSpace(lineId))
                    continue; //Skip over blank lines

                if (numCol < 2)
                    throw new IndexOutOfRangeException("Observed boardings file is expecting two columns (found " + numCol + ")");


                reader.Get(out float amBoardings, 1);

                result[lineId] = amBoardings;
            }
        }

        return result;
    }

    private void CalcFitness(Dictionary<string, float> observedBoardings, Dictionary<string, float> modelledBoardings)
    {
        double squaredErrorSum = 0.0;
        int numberOfLines = 0;

        var badMappings = new List<string>();

        foreach (var key in modelledBoardings.Keys)
        {
            if (!observedBoardings.ContainsKey(key))
            {
                badMappings.Add(key);
                continue;
            }

            float lineObservedBoardings = observedBoardings[key];
            float lineModelledBoardings = modelledBoardings[key];

            float error = lineModelledBoardings - lineObservedBoardings;
            float squaredError = error * error;

            squaredErrorSum += squaredError;
            numberOfLines++;
        }

        if (badMappings.Count > 0)
        {
            Console.WriteLine("Found " + badMappings.Count + " lines in the network that are missing in the observation file");

        }

        Root.RetrieveValue = (() => (float)( Math.Sqrt(squaredErrorSum / numberOfLines)));
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
