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
using System.IO;
using System.Linq;
using Datastructure;
using TMG.Emme;
using TMG.Estimation;
using TMG.Input;
using XTMF;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace TMG.NetworkEstimation
{
    [ModuleInformation(Description = "Calculates Root Mean Square Error (RMSE) of transit line boardings for AM and PM time periods, including an entry for walk-all-way numbers")]
    public class CalcRmsEofBoardingsAmpmwaw : IEmmeTool
    {

        [RootModule]
        public IEstimationClientModelSystem Root;

        [RunParameter("AM Scenario", 0, "The number of the AM Emme scenario")]
        public int AMScenarioNumber;

        [RunParameter("PM Scenario", 0, "The number of the PM Emme scenario")]
        public int PMScenarioNumber;

        [Parameter("WaW Error Factor", 0.5f, "A factor applied to the error term of walk-all-way numbers (which are always compared against a truth value of 0). Therefore " +
                    "the error term (which gets included in the overall mean) for WAW is given by (ErrorFactor * ModelWalkAllWayTrips)^2. A weight of 0 will disable including " +
                    "walk-all-way numbers.")]
        public float WawErrorFactor;

        [Parameter("AM Error Factor", 1.0f, "A factor applied to the non-squared error of AM boardings.")]
        public float AMErrorFactor;

        [Parameter("PM Error Factor", 1.0f, "A factor applied to the non-squared error of PM boardings.")]
        public float PMErrorFactor;

        [SubModelInformation(Description = "AM Observed Boardings File", Required = true)]
        public FileLocation ObservedBoardingsFileAM;

        [SubModelInformation(Description = "PM Observed Boardings File", Required = true)]
        public FileLocation ObservedBoardingsFilePM;

        [SubModelInformation(Description = "Line Aggregation File", Required = true)]
        public FileLocation LineAggregationFile;

        [SubModelInformation(Required = false, Description = "Optionally where to save the aggregated boardings to file.")]
        public FileLocation SaveAMBoardingsByAggregatedLine;

        [SubModelInformation(Required = false, Description = "Optionally where to save the deltas of the aggregated boardings to file.")]
        public FileLocation SaveAMBoardingDifferencesByAggregatedLine;

        [SubModelInformation(Required = false, Description = "Optionally where to save the aggregated boardings to file.")]
        public FileLocation SavePMBoardingsByAggregatedLine;

        [SubModelInformation(Required = false, Description = "Optionally where to save the deltas of the aggregated boardings to file.")]
        public FileLocation SavePMBoardingDifferencesByAggregatedLine;

        private const string ToolName = "tmg.XTMF_internal.return_boardings_and_WAW";
        private const string WawKey = "Walk-all-way";
        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException("Controller is not a ModellerController");
            }

            //Load the observed boardings
            var observationsAM = LoadObservedBoardingsFile(ObservedBoardingsFileAM.GetFilePath());
            var observationsPM = LoadObservedBoardingsFile(ObservedBoardingsFilePM.GetFilePath());

            //Load the AM Modelled Boardings
            var args = string.Join(" ", AMScenarioNumber,
                                        LineAggregationFile.GetFilePath(),
                                        (WawErrorFactor != 0.0f));
            string result = "";
            mc.Run(ToolName, args, (p => Progress = p), ref result);
            var amModelResults = ParseResults(result);

            //Load the PM Modelled Boardings
            args = string.Join(" ", PMScenarioNumber,
                                    LineAggregationFile.GetFilePath(),
                                    (WawErrorFactor != 0.0f));
            result = "";
            mc.Run(ToolName, args, ref result);
            var pmModelResults = ParseResults(result);

            //Calculate the fitness
            CalcFitness(observationsAM, observationsPM, amModelResults, pmModelResults);

            return true;
        }

        private Dictionary<string, float> ParseResults(string pythonDictionary)
        {
            var result = new Dictionary<string, float>();

            var cleaned = pythonDictionary.Replace("{", "").Replace("}", "");
            var cells = cleaned.Split(',');
            int cellNumber = 0;
            foreach (var cell in cells)
            {
                var pair = cell.Split(':');
                if (pair.Length < 2)
                {
                    throw new XTMFRuntimeException("In '" + Name + "' the results were not in the correct format in cell #" 
                        + cellNumber + ".\r\nThe results were '" + pythonDictionary + "'.");
                }
                var lineId = pair[0].Replace("'", "").Trim();
                float boardings = float.Parse(pair[1]);
                result[lineId] = boardings;
                cellNumber++;
            }
            return result;
        }

        private Dictionary<string, float> LoadObservedBoardingsFile(string filepath)
        {
            var result = new Dictionary<string, float>();

            using (CsvReader reader = new CsvReader(filepath))
            {
                reader.LoadLine(); //Skip the first line                
                int numCol;
                while (reader.LoadLine(out numCol))
                {
                    string lineId;
                    reader.Get(out lineId, 0);

                    if (string.IsNullOrWhiteSpace(lineId))
                        continue; //Skip over blank lines

                    if (numCol < 2)
                        throw new IndexOutOfRangeException("Observed boardings file is expecting two columns (found " + numCol + ")");

                    float boardings;
                    reader.Get(out boardings, 1);

                    result[lineId] = boardings;
                }
            }

            return result;
        }

        private void CalcFitness(Dictionary<string, float> observedBoardingsAM, Dictionary<string, float> observedBoardingsPM, Dictionary<string, float> modelledBoardingsAm, Dictionary<string, float> modelledBoardingsPm)
        {
            double squaredErrorSum = 0.0;
            int numberOfLines = 0;
            if (SaveAMBoardingsByAggregatedLine != null)
            {
                SaveBoardings(SaveAMBoardingsByAggregatedLine, modelledBoardingsAm);
            }
            if (SavePMBoardingsByAggregatedLine != null)
            {
                SaveBoardings(SavePMBoardingsByAggregatedLine, modelledBoardingsPm);
            }
            //Calc error for AM boardings
            foreach (var entry in observedBoardingsAM)
            {
                if (!modelledBoardingsAm.ContainsKey(entry.Key)) continue; //Skip over lines not in network
                var modelledBoardings = modelledBoardingsAm[entry.Key];
                squaredErrorSum += Math.Pow((modelledBoardings - entry.Value) * AMErrorFactor, 2);
                numberOfLines++;
            }

            //Calc error for PM boardings
            foreach (var entry in observedBoardingsAM)
            {
                if (!modelledBoardingsPm.ContainsKey(entry.Key)) continue; //Skip over lines not in network
                var modelledBoardings = observedBoardingsPM[entry.Key];
                squaredErrorSum += Math.Pow((modelledBoardings - entry.Value) * PMErrorFactor, 2);
                numberOfLines++;
            }

            if (SaveAMBoardingDifferencesByAggregatedLine != null)
            {
                SaveBoardings(SaveAMBoardingDifferencesByAggregatedLine, ComputeDeltas(observedBoardingsAM, modelledBoardingsAm));
            }

            if (SavePMBoardingDifferencesByAggregatedLine != null)
            {
                SaveBoardings(SavePMBoardingDifferencesByAggregatedLine, ComputeDeltas(observedBoardingsPM, modelledBoardingsPm));
            }

            //Add in the values for walk-all-ways
            if (WawErrorFactor != 0.0f)
            {
                squaredErrorSum += Math.Pow(modelledBoardingsAm[WawKey] * WawErrorFactor, 2) + Math.Pow(modelledBoardingsPm[WawKey] + WawErrorFactor, 2);
                numberOfLines += 2;
            }

            Root.RetrieveValue = (() => (float)(Math.Sqrt(squaredErrorSum / numberOfLines)));
        }

        private Dictionary<string, float> ComputeDeltas(Dictionary<string, float> observedBoardingsAM, Dictionary<string, float> modelledBoardingsAm)
        {
            return (from modelled in modelledBoardingsAm
                    select new KeyValuePair<string, float>(modelled.Key, modelled.Value - observedBoardingsAM[modelled.Key])).ToDictionary(e => e.Key, e => e.Value);
        }

        private void SaveBoardings(FileLocation location, Dictionary<string, float> periodData)
        {
            using (StreamWriter writer = new StreamWriter(location))
            {
                writer.WriteLine("Line,Boardings");
                foreach (var set in periodData)
                {
                    writer.Write(set.Key);
                    writer.Write(',');
                    writer.WriteLine(set.Value);
                }
            }
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
}
