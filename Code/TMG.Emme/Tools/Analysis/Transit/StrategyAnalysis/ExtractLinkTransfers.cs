/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMG.Input;
using XTMF;

namespace TMG.Emme.Tools.Analysis.Transit.StrategyAnalysis
{
    [ModuleInformation(Description = "")]
    public class ExtractLinkTransfers : IEmmeTool
    {
        [RunParameter("Scenario Number", 1, "The scenario to read the information from.")]
        public int ScenarioNumber;

        [RunParameter("Demand Matrix", 0, "The matrix to use for analysis. A value of 0 will cause the tool to search for the demand matrix used in the most recent assignment.")]
        public int DemandMatrix;

        [RunParameter("Link Set", "label:link1:link2", "A description of what links to apply this to. Needs to be in the form label:link1:link2. Links should be in the form 10000,10001. Separate link pair sets with a semicolon.")]
        public string LinkSet;

        [RunParameter("Peak Period Factor", 1.0f, "The peak period factor to use. Note that the tool uses division here, so a value akin to the assignment period is expected.")]
        public float PeakPeriodFactor;

        [RunParameter("Hypernetwork", false, "Set this to true if you wish to include links in the hypernetwork with the same shape.")]
        public bool Hypernetwork;

        [SubModelInformation(Required = true, Description = "The location to save the results to.")]
        public FileLocation SaveTo;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private const string ToolName = "tmg.analysis.transit.strategy_analysis.extract_link_transfers";

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if(mc == null)
            {
                throw new XTMFRuntimeException("Controller is not a ModellerController!");
            }
            return mc.Run(ToolName, GetParameters());
        }

        private string GetParameters()
        {
            /*
                def __call__(self, xtmf_ScenarioNumber, xtmf_DemandMatrixNumber, LinkSetString, 
                 ExportFile, PeakHourFactor, HypernetworkFlag):
                 */
            return string.Join(" ", ScenarioNumber, DemandMatrix, AddQuotes(LinkSet), GetFullPath(SaveTo), PeakPeriodFactor, Hypernetwork);
        }

        private string GetFullPath(FileLocation saveTo)
        {
            return AddQuotes(System.IO.Path.GetFullPath(saveTo));
        }

        private string AddQuotes(string str)
        {
            return "\"" + str + "\"";
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

}
