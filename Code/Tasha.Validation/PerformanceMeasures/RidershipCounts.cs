/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.IO;
using TMG.Input;
using TMG.Emme;
using TMG.DataUtility;
using XTMF;

namespace Tasha.Validation.PerformanceMeasures;

public class RidershipCounts : IEmmeTool
{
    private const string ToolName = "tmg.analysis.transit.strategy_analysis.volume_per_operator";

    [SubModelInformation(Required = true, Description = "Ridership results .CSV file")]
    public FileLocation RidershipResults;

    [RunParameter("Scenario Numbers", "1", typeof(NumberList), "A comma separated list of scenario numbers to execute this against.")]
    public NumberList ScenarioNumbers;

    [SubModelInformation(Required = false, Description = "Operators to Consider")]
    public Operator[] OperatorsToConsider;

    public sealed class Operator : IModule
    {
        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [RunParameter("Label", "", "What label do you want to apply to this operator")]
        public string Label;

        [RunParameter("Line Filter", "", "Appropriate line filter for lines with this operator")]
        public string LineFilter;

        [RunParameter("Mode Filter", "", "Appropriate mode filter for this operator")]
        public string ModeFilter;

        [RunParameter("Custom Filter", "", "A custom made filter that you would like to apply to the network")]
        public string CustomFilter;

        internal string ReturnFilter(ModellerController controller)
        {   
            string filterExpression = Label.Replace('"', '\'') + ":";

            if(!String.IsNullOrWhiteSpace(CustomFilter))
            {
                filterExpression += CustomFilter.Replace('"', '\'');                        
            }

            else if(!String.IsNullOrWhiteSpace(LineFilter) && !String.IsNullOrWhiteSpace(ModeFilter))
            {
                filterExpression += 
                    (LineFilter.Contains("=") ? LineFilter.Replace('"', '\'') : "line=" + LineFilter.Replace('"', '\'')) + " and " 
                    + (ModeFilter.Contains("=") ? ModeFilter.Replace('"', '\'') : "mode=" + ModeFilter.Replace('"', '\''));
            }

            else if(!String.IsNullOrWhiteSpace(LineFilter))
            {
                filterExpression += 
                    (LineFilter.Contains("=") ? LineFilter.Replace('"', '\'') : "line=" + LineFilter.Replace('"', '\''));
            }

            else
            {
                filterExpression += 
                    (ModeFilter.Contains("=") ? ModeFilter.Replace('"', '\'') : "mode=" + ModeFilter.Replace('"', '\''));
            }

            return filterExpression;
        }

        public bool RuntimeValidation(ref string error)
        {
            if(String.IsNullOrWhiteSpace(Label))
            {
                error = "In " + Name + " the label parameter was left blank.";
                return false;
            }

            if(((!String.IsNullOrWhiteSpace(LineFilter)) || (!String.IsNullOrWhiteSpace(ModeFilter))) && (!String.IsNullOrWhiteSpace(CustomFilter)))
            {
                error = "In " + Name + " the line/mode and custom filters are all filled in. Please fill in either the line/mode or custom, but not both sets of filters.";
                return false;                    
            }
            return true;
        }
    }

    private string GenerageArgumentString(ModellerController controller)
    {
        var scenarioString = string.Join(",", ScenarioNumbers.Select(v => v.ToString()));
        var filterString = "\"" + string.Join(",", OperatorsToConsider.Select(b => b.ReturnFilter(controller))) + "\"";
        return "\"" + scenarioString + "\" " + filterString + "\"" + Path.GetFullPath(RidershipResults) + "\" ";
    }

    public bool Execute(Controller controller)
    {
        var modeller = controller as ModellerController;
        if (modeller == null)
        {
            throw new XTMFRuntimeException(this, "In '" + Name + "' we were not given a modeller controller!");
        }

        return modeller.Run(this, ToolName, GenerageArgumentString(modeller));
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
        get { return new Tuple<byte, byte, byte>(120, 25, 100); }   
    }            

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
