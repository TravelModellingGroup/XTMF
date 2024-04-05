using System;
using System.Linq;
using System.IO;
using TMG.Input;
using TMG.Emme;
using TMG.DataUtility;
using XTMF;

namespace Tasha.Validation.PerformanceMeasures;

public class RevenueCalculations : IEmmeTool
{
    private const string ToolName = "tmg.analysis.transit.strategy_analysis.revenue_calculation";

    [RunParameter("Scenario Numbers", "1", typeof(NumberList), "A comma separated list of scenario numbers to execute this against.")]
    public NumberList ScenarioNumbers;

    [SubModelInformation(Required = true, Description = "Revenue results .CSV file")]
    public FileLocation RevenueResults;

    [SubModelInformation(Required = false, Description = "The different links to consider")]
    public LinesToConsider[] TransitLinesConsidered;

    public sealed class LinesToConsider : IModule
    {
        [RunParameter("Label", "GoNetwork", "The appropriate label for this Transit Lines")]
        public string Label;

        [RunParameter("Custom Transit Line Filter", "", "The line filter for this specific group. Use standard EMME Network selector expression")]
        public string TransitLineFilter;

        internal string ReturnFilter(ModellerController controller)
        {
            string filterExpression = Label.Replace('"', '\'') + ":";

            filterExpression +=
                    (TransitLineFilter.Contains("=") ? TransitLineFilter.Replace('"', '\'') : "line=" + TransitLineFilter.Replace('"', '\''));

            return filterExpression;
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
            if (String.IsNullOrWhiteSpace(Label))
            {
                error = "In " + Name + " the label parameter was left blank.";
                return false;
            }
            if (String.IsNullOrWhiteSpace(TransitLineFilter))
            {
                error = "In " + Name + " the line filter parameter was left blank.";
                return false;
            }
            return true;
        }
    }

    private string GenerageArgumentString(ModellerController controller)
    {
        var scenarioString = string.Join(",", ScenarioNumbers.Select(v => v.ToString()));
        var linkString = "\"" + string.Join(",", TransitLinesConsidered.Select(b => b.ReturnFilter(controller))) + "\"";
        return "\"" + scenarioString + "\" " + linkString + "\"" + Path.GetFullPath(RevenueResults) + "\" ";
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
