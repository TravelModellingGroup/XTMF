/*
    Copyright 2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections.Generic;
using System.IO;
using TMG.Input;
using XTMF;

namespace TMG.Emme.XTMF_Internal;

[ModuleInformation(Description = "Provides the ability to execute multi network calculations using a CSV file." +
    " The format of the CSV file is as follows.  A header with the following columns: Result, Expression, NodeSelection, LinkSelection, TransitLineSelection, Domain, Aggregation." +
    " The order of the columns is required. The result attribute must already be initialized before running.  Each row is a separate network calculation, and are run" +
    " sequentially in order.")]
public sealed class NetworkCalculatorFromCSV : IEmmeTool
{
    private const string _ToolName = "tmg.XTMF_internal.xtmf_network_calculator";

    [RunParameter("Scenario Number", "1", "What scenario would you like to run this for?")]
    public int ScenarioNumber;

    [SubModelInformation(Required = true, Description = "The location to load the network calculations to execute.")]
    public FileLocation CalculationFile;

    public enum DomainTypes
    {
        Link = 0,
        Node = 1,
        Transit_Line = 2,
        Transit_Segment = 3
    }

    public enum AggregationType
    {
        None = 0,
        Sum = 1,
        Average = 2,
        Min = 3,
        Max = 4,
        BitwiseAnd = 5,
        BitwiseOr = 6,
    }

    private record struct NetworkCalculationParameters(string Expression, string NodeSelection, string LinkSelection, string TransitLineSelection,
        DomainTypes Domain, AggregationType Aggregation, string Result);

    public bool Execute(Controller controller)
    {
        if (controller is not ModellerController modeller)
        {
            throw new XTMFRuntimeException(this, "In '" + Name + "' we were not given a modeller controller!");
        }
        // TODO: In the future replace this with a tool that runs all of these at the same time.
        foreach (NetworkCalculationParameters parameters in ReadNetworkCalculations())
        {
            if(!modeller.Run(this, _ToolName, CreateParmeters(parameters)))
            {
                return false;
            }
        }
        return true;
    }

    private string CreateParmeters(NetworkCalculationParameters parameters)
    {
        return string.Join(" ", ScenarioNumber, (int)parameters.Domain, 
            AddQuotes(parameters.Expression),
            AddQuotes(parameters.NodeSelection),
            AddQuotes(parameters.LinkSelection),
            AddQuotes(parameters.TransitLineSelection),
            AddQuotes(parameters.Result),
            (int)parameters.Aggregation);
    }

    private static string AddQuotes(string toQuote)
    {
        return string.Concat("\"", toQuote, "\"");
    }

    /// <summary>
    /// Reads the network calculations from a CSV file.
    /// </summary>
    /// <returns>An enumerable collection of <see cref="NetworkCalculationParameters"/>.</returns>
    private IEnumerable<NetworkCalculationParameters> ReadNetworkCalculations()
    {
        const int expectedColumns = 7;
        if (!File.Exists(CalculationFile))
        {
            throw new XTMFRuntimeException(this, $"In {Name} the file '{CalculationFile.GetFilePath()}' does not exist!");
        }
        using CsvReader reader = new(CalculationFile);
        reader.LoadLine(); // Skip the header
        while (reader.LoadLine(out int columns))
        {
            // Skip lines that are not long enough.
            if (columns < expectedColumns)
            {
                continue;
            }
            // Result, Expression, NodeSelection, LinkSelection, TransitLineSelection, Domain, Aggregation
            reader.Get(out string Result, 0);
            reader.Get(out string Expression, 1);
            reader.Get(out string NodeSelection, 2);
            reader.Get(out string LinkSelection, 3);
            reader.Get(out string TransitLineSelection, 4);
            reader.Get(out string Domain, 5);
            reader.Get(out string Aggregation, 6);
            yield return new NetworkCalculationParameters()
            {
                Expression = Expression,
                NodeSelection = NodeSelection,
                LinkSelection = LinkSelection,
                TransitLineSelection = TransitLineSelection,
                Domain = Enum.Parse<DomainTypes>(Domain),
                Aggregation = Enum.Parse<AggregationType>(Aggregation),
                Result = Result
            };

        }
    }

    public string Name { get; set; } = string.Empty;

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
