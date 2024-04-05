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

public class LinkVolumes : IEmmeTool
{
    private const string ToolName = "tmg.analysis.link_specific_volumes";

    [SubModelInformation(Required = true, Description = "Volume results .CSV file")]
    public FileLocation LinkVolumeResults;

    [RunParameter("Scenario Numbers", "1", typeof(NumberList), "A comma separated list of scenario numbers to execute this against.")]
    public NumberList ScenarioNumbers;

    [RunParameter("Transit Flag", false, "Report the transit volumes on this link in addition to the Auto Volumes.")]
    public bool TransitFlag;

    [SubModelInformation(Required = false, Description = "The different links to consider")]
    public LinksToConsider[] LinksConsidered;

    public sealed class LinksToConsider : IModule
    {
        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [RunParameter("Label", "HWY407westOf404W", "The appropriate label for this link")]
        public string Label;                

        [RunParameter("i,j of link", "123,123", "The i,j tuple of the line separated by a comma")]
        public string LinkID;

        internal string ReturnFilter()
        {   
            string filter = Label.Replace('"', '\'') + ":" + "link=" + LinkID.Replace('"', '\'');
            return filter;
        }

        public bool RuntimeValidation(ref string error)
        {
            if(String.IsNullOrWhiteSpace(Label))
            {
                error = "In " + Name + " the label parameter was left blank.";
                return false;
            }
            else if (String.IsNullOrWhiteSpace(LinkID))
            {
                error = "in " + Name + " the ij link parameter was left blank";
                return false;
            }

            return true;
        }
    }

    private string GenerageArgumentString()
    {
        var scenarioString = string.Join(",", ScenarioNumbers.Select(v => v.ToString()));
        var linkString = "\"" + string.Join(";", LinksConsidered.Select(b => b.ReturnFilter())) + "\"";
        return "\"" + scenarioString + "\" " + linkString + "\"" + Path.GetFullPath(LinkVolumeResults) + "\" " + TransitFlag;
    }

    public bool Execute(Controller controller)
    {
        var modeller = controller as ModellerController;
        if (modeller == null)
        {
            throw new XTMFRuntimeException(this, "In '" + Name + "' we were not given a modeller controller!");
        }

        return modeller.Run(this, ToolName, GenerageArgumentString());
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
