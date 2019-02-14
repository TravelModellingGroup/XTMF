/*
    Copyright 2015-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using TMG.Input;
using XTMF;

namespace TMG.Emme.Tools.Analysis.Traffic
{
    public class ExportCountpostResultsMulticlass : IEmmeTool
    {
        private const string ToolName = "tmg.analysis.traffic.export_countpost_results_multiclass";
        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [RunParameter("Scenario Number", "1", typeof(int), "The scenario to interact with")]
        public int ScenarioNumber;

        [RunParameter("CountpostAttributeFlag", "@stn1", typeof(string), "The attribute name to use for identifying countposts.")]
        public string CountpostAttributeFlag;

        [RunParameter("AlternateCountpostAttributeFlag", "@stn2", typeof(string), "The alternate attribute name to use for identifying countposts.")]
        public string AlternateCountpostAttributeFlag;

        [RunParameter("Traffic Class Volume Attribute", "@auto_volume", typeof(string), "For extraction of a specific classes volume only")]
        public string TrafficClassAttributeId;

        [SubModelInformation(Required = false, Description = "A link to a csv file that contains countposts that will need to be summed, rather than max taken. A header is included")]
        public FileLocation SumPostFile;

        [SubModelInformation(Required = true, Description = "The location to save the results to")]
        public FileLocation SaveTo;

        public bool Execute(Controller controller)
        {
            var modeller = controller as ModellerController;
            if (modeller == null)
            {
                throw new XTMFRuntimeException(this, "In '" + Name + "' we require the use of EMME Modeller in order to execute.");
            }
            modeller.Run(this, ToolName, new[]
            {
              new ModellerControllerParameter("xtmf_ScenarioNumber", ScenarioNumber.ToString()),
              new ModellerControllerParameter("CountpostAttributeId", CountpostAttributeFlag),
              new ModellerControllerParameter("AlternateCountpostAttributeId", AlternateCountpostAttributeFlag),
              new ModellerControllerParameter("TrafficClassAttributeId", TrafficClassAttributeId),
              new ModellerControllerParameter("SumPostFile", SumPostFile.GetFilePath()),
              new ModellerControllerParameter("ExportFile", SaveTo.GetFilePath()),
            });
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            if (ErrorIfBlank(CountpostAttributeFlag, "CountpostAttributeFlag", ref error))
            {
                return false;
            }
            return true;
        }

        private bool ErrorIfBlank(string flag, string nameOfAttribute, ref string error)
        {
            if (String.IsNullOrWhiteSpace(flag))
            {
                error = "In '" + Name + "' the attribute '" + nameOfAttribute + "' is not assigned to!";
                return true;
            }
            return false;
        }
    }

}
