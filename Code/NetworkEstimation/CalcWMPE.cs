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
using System.Linq;
using System.Text;
using TMG.Emme;
using TMG.Estimation;
using TMG.Input;
using XTMF;
using Datastructure;

namespace TMG.NetworkEstimation
{
    [ModuleInformation(Description="Calculates Weighted Mean Percent Error (WMPE) for transit line boardings.")]
    public class CalcWMPE : IEmmeTool
    {
        /*
        [RootModule]
        public IEstimationClientModelSystem Root;
        */
         
        [RunParameter("Scenario", 0, "The number of the Emme scenario")]
        public int ScenarioNumber;

        [SubModelInformation(Description = "Observed Boardings File", Required = true)]
        public FileLocation ObservedBoardingsFile;

        [SubModelInformation(Description = "Line Aggregation File", Required = true)]
        public FileLocation LineAggregationFile;

        private const string _ToolName = "TMG2.XTMF.returnBoardings";
        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException("Controller is not a ModellerController");
            }

            var args = string.Join(" ", this.ScenarioNumber, this.LineAggregationFile.GetFilePath());
            string result = "";
            mc.Run(_ToolName, args, (p => this.Progress = p), ref result);

            var modelResults = this.ParseResults(result);
            var observations = this.LoadObservedBoardingsFile();

            this.CalcFitness(observations, modelResults);

            return true;
        }

        private Dictionary<string, float> ParseResults(string pythonDictionary)
        {
            var result = new Dictionary<string, float>();

            var cleaned = pythonDictionary.Replace("{", "").Replace("}", "");
            var cells = cleaned.Split(',');
            foreach (var cell in cells)
            {
                var pair = cell.Split(':');
                var lineId = pair[0].Replace("'", "").Trim();
                float boardings = float.Parse(pair[1]);
                result[lineId] = boardings;
            }
            return result;
        }

        private Dictionary<string, Tuple<float, float>> LoadObservedBoardingsFile()
        {
            var result = new Dictionary<string, Tuple<float, float>>();

            using (CsvReader reader = new CsvReader(this.ObservedBoardingsFile.GetFilePath()))
            {
                reader.LoadLine(); //Skip the first line                
                int numCol = 3;
                while (reader.LoadLine(out numCol))
                {
                    if (numCol < 3)
                        throw new IndexOutOfRangeException("Observed boardings file is expecting two columns (found " + numCol.ToString() + ")");

                    string lineId = "";
                    float weight = 0.0f;
                    float amBoardings = 0.0f;
                    reader.Get(out lineId, 0);
                    reader.Get(out weight, 1);
                    reader.Get(out amBoardings, 2);

                    if (amBoardings <= 0.0f)
                        throw new Exception("Found 0 boardings for line/group " + lineId + ". All values must be greater than 0 in order to calculate WMPE");

                    result[lineId] = new Tuple<float, float>(weight, amBoardings);
                }
            }

            return result;
        }

        private void CalcFitness(Dictionary<string, Tuple<float, float>> observedBoardings, Dictionary<string, float> modelledBoardings)
        {
            float percentErrorSum = 0.0f;
            float totalWeight = 0.0f;

            var badMappings = new List<string>();

            foreach (var key in modelledBoardings.Keys)
            {
                if (!observedBoardings.ContainsKey(key))
                {
                    badMappings.Add(key);
                    continue;
                }

                var tup = observedBoardings[key];
                float lineObservedBoardings = tup.Item2;
                float lineWeight = tup.Item1;
                float lineModelledBoardings = modelledBoardings[key];

                float absError = Math.Abs(lineModelledBoardings - lineObservedBoardings);
                float percentError = absError / lineObservedBoardings * 100.0f;

                percentErrorSum += percentError * lineWeight;
                totalWeight += lineWeight;
            }

            if (badMappings.Count > 0)
            {
                Console.WriteLine("Found " + badMappings.Count + " lines in the network that are missing in the observation file");
            }

            //this.Root.RetrieveValue = (() => percentErrorSum / totalWeight);
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
