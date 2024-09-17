/*
    Copyright 2015-2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG.Emme;
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

namespace TMG.EMME.XTMF_Internal;

[ModuleInformation(Description = "This tool is designed to call the EMME Network calculator.")]
[RedirectModule("Tasha.Validation.PerformanceMeasures.NetworkCalculator, Tasha.Validation, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")]
public sealed class NetworkCalculator : IEmmeTool
{

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

    private const string ToolName = "tmg.XTMF_internal.xtmf_network_calculator";

    [RunParameter("Scenario Number", "1", "What scenario would you like to run this for?")]
    public int ScenarioNumber;

    [RunParameter("Expression", "", "What is the expression you want to compute?")]
    public string Expression;

    [RunParameter("Node Selection", "all", "What specific nodes would you like to include in the calculation? Default=all")]
    public string Node_Selection;

    [RunParameter("Link Selection", "all", "What specific links would you like to include in the calculation? Default=all")]
    public string Link_Selection;

    [RunParameter("Transit Line Selection", "all", "What specific transit lines would you like to include in the calculation? Default=all")]
    public string Transit_Line_Selection;

    [RunParameter("Domain", DomainTypes.Link, "What Emme domain type is the result? Options: Link, Node, Transit_Line, Transit_Segment")]
    public DomainTypes Domain;

    [RunParameter("Result Attribute", "", "The attribute to save the result into, leave blank to not save.")]
    public string Result;

    [RunParameter("Aggregation", AggregationType.None, "The aggregation type to apply if required. Set to none if there is no aggregation.")]
    public AggregationType Aggregation;

    [SubModelInformation(Required = false, Description = "Resource that will store the sum of the network calculation")]
    public IResource SumOfReport;

    public bool Execute(Controller controller)
    {
        var modeller = controller as ModellerController ?? throw new XTMFRuntimeException(this, "In '" + Name + "' we were not given a modeller controller!");
        string result = null;
        if (modeller.Run(this, ToolName, GetParameters(), ref result))
        {
            if (SumOfReport != null)
            {
                if (float.TryParse(result, out float value))
                {
                    ISetableDataSource<float> dataSource = ((ISetableDataSource<float>)SumOfReport.GetDataSource());
                    if (!dataSource.Loaded)
                    {
                        dataSource.LoadData();
                    }
                    dataSource.SetData(value);
                }
            }
            return true;
        }
        return false;
    }

    private string GetParameters()
    {
        return string.Join(" ", ScenarioNumber, (int)Domain, AddQuotes(Expression), AddQuotes(Node_Selection), AddQuotes(Link_Selection), AddQuotes(Transit_Line_Selection),
            AddQuotes(Result), (int)Aggregation);
    }

    private static string AddQuotes(string toQuote)
    {
        return String.Concat("\"", toQuote, "\"");
    }

    public string Name { get; set; } = string.Empty;
    
    public float Progress { get; set; }
    

    public Tuple<byte, byte, byte> ProgressColour => new(120, 25, 100);

    public bool RuntimeValidation(ref string error)
    {
        if (SumOfReport != null)
        {
            if (!SumOfReport.CheckResourceType<float>())
            {
                error = $"In {Name} the SumOfReport is not of type float!";
                return false;
            }
            if (SumOfReport.GetDataSource() is not ISetableDataSource<float>)
            {
                error = $"In {Name} the SumOfReport is not a settable data source!";
                return false;
            }
        }
        return true;
    }
}
