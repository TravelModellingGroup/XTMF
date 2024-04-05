/*
    Copyright 2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;

namespace TMG.Emme.Tools.Analysis.Transit;

[ModuleInformation(Description = "This tool invokes TMGToolbox's tmg.analysis.transit.extract_transit_matrix_results")]
public class ExtractTransitMatrixResults : IEmmeTool
{
    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50,150,50);

    [RunParameter("Scenario Number", 0, "The scenario numbers to run against.")]
    public int ScenarioNumber;

    public enum AnalysisType
    {
        Distance,
        ActualTime,
        ActualCost,
        PerceivedTime,
        PerceivedCost
    }

    [ModuleInformation(Description = "This module specifies a particular matrix extraction to perform on a transit assignment.")]
    public class Extraction : IModule
    {
        [RunParameter("Modes", "*", "The modes to do the extraction for. * is all modes.")]
        public string Modes;

        [RunParameter("Matrix Number", 0, "The matrix to save the results into.")]
        public int MatrixNumber;

        [RunParameter("Analysis Type", AnalysisType.Distance, "The type of analysis to perform.")]
        public AnalysisType Type;

        [RunParameter("Class Name", "", "The name of the class to extract.  Use a blank for a single class assignment.")]
        public string ClassName;

        internal string TypeString
        {
            get
            {
                switch(Type)
                {
                    case AnalysisType.Distance:
                        return "Distance";
                    case AnalysisType.ActualCost:
                        return "ActualCost";
                    case AnalysisType.ActualTime:
                        return "ActualTime";
                    case AnalysisType.PerceivedCost:
                        return "PerceivedCost";
                    case AnalysisType.PerceivedTime:
                        return "PerceivedTime";
                }
                throw new XTMFRuntimeException(this, "The Analysis Type is not recognized!");
            }
        }

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new(50,150,50);

        public bool RuntimeValidation(ref string error)
        {
            if(String.IsNullOrWhiteSpace(Modes))
            {
                error = $"In {Name} there were no modes selected for the analysis!";
                return false;
            }
            if(Modes.Contains(','))
            {
                error = $"In {Name} there was an invalid mode ','!";
                return false;
            }
            if(MatrixNumber <= 0)
            {
                error = $"In {Name} the matrix number needs to be greater than zero!";
                return false;
            }
            if(ClassName.Contains(','))
            {
                error = $"In {Name} the class name may not include a comma!";
                return false;
            }
            return true;
        }
    }

    [SubModelInformation(Required = true, Description = "The extractions to perform.")]
    public Extraction[] Extractions;

    public bool Execute(Controller controller)
    {
        if (controller is ModellerController mc)
        {
            return mc.Run(this, "tmg.analysis.transit.extract_transit_matrix_results", GetParameters());
        }
        throw new XTMFRuntimeException(this, "In '" + Name + "' we were unable to run since the controller is not connected through modeller.");
    }

    private ModellerControllerParameter[] GetParameters()
    {
        //def __call__(self, xtmf_ScenarioNumber, xtmf_ModeList, xtmf_MatrixNumbers, xtmf_AnalysisTypes, xtmf_ClassNames):
        var scenarioNumber = new ModellerControllerParameter("xtmf_ScenarioNumber", ScenarioNumber.ToString());
        var modeList = new ModellerControllerParameter("xtmf_ModeList", string.Join(",", from ex in Extractions
                                                                                         select ex.Modes));
        var matrixNumber = new ModellerControllerParameter("xtmf_MatrixNumbers", string.Join(",", from ex in Extractions
                                                                                                 select ("mf" + ex.MatrixNumber.ToString())));
        var analysisType = new ModellerControllerParameter("xtmf_AnalysisTypes", string.Join(",", from ex in Extractions
                                                                                                 select ex.TypeString));

        var classNames = new ModellerControllerParameter("xtmf_ClassNames", string.Join(",", from ex in Extractions
                                                                                             select ex.ClassName));

        return [scenarioNumber, modeList, matrixNumber, analysisType, classNames];
    }

    public bool RuntimeValidation(ref string error)
    {
        if(ScenarioNumber <= 0)
        {
            error = $"In {Name} the scenario number must be greater than zero!";
            return false;
        }
        return true;
    }
}
