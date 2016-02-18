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

        [RunParameter("Demand Matrix", 1, "The matrix to use for assigning demand.")]
        public int DemandMatrix;

        [RunParameter("Link Set", "", "A description of what links to apply this to.")]
        public string LinkSet;

        [RunParameter("Peak Period Factor", 0.43f, "The peak period factor to use for the assignment.")]
        public float PeakPeriodFactor;

        [RunParameter("Hypernetwork", false, "Set this to true if you are running this against a hyper-network")]
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
