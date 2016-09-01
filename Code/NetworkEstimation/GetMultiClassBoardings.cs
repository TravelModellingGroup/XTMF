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
using TMG.Input;
using XTMF;
using System.IO;
using System.Text.RegularExpressions;

namespace TMG.Emme
{
    [ModuleInformation(Description = "Gets multiclass boardings from previous transit assignment. Uses EMME's network results tool.")]
    public class GetMultiClassBoardings : IEmmeTool
    {
        [RunParameter("Scenario", 0, "The number of the Emme scenario which boardings will be extracted from.")]
        public int ScenarioNumber;

        [SubModelInformation(Description = "Line Aggregation File", Required = true)]
        public FileLocation LineAggregationFile;
         
        private const string _ToolName = "tmg.XTMF_internal.return_boardings_multiclass";

        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(100, 100, 150);

        public bool Execute(Controller controller)
        {
            var mc = controller as ModellerController;
            if (mc == null)
            {
                throw new XTMFRuntimeException("Controller is not a ModellerController");
            }

            //Boardings
            var args = string.Join(" ", this.ScenarioNumber,
                                        this.LineAggregationFile.GetFilePath());
            string result = "";
            mc.Run(_ToolName, args, (p => this.Progress = p), ref result);
            var Boardings = this.ParseResults(result);

             

            return true;
        }

        private Dictionary<string, Dictionary<string, float>> ParseResults(string pythonDictionary)
        {
            var result = new Dictionary<string, float>();
            var fullresults = new Dictionary<string, Dictionary<string, float>> { };
            var cleaned = pythonDictionary;
            string classPattern = @"{([^}]+)}"; //filter expression to separate out each class
            string LineName = "";
            float Boardings = 0.0f;
            string ClassName = "";
            MatchCollection PersonClasses = Regex.Matches(cleaned, classPattern);
          
            foreach (Match PersonClass in PersonClasses)
            {
                ClassName = "";
                result.Clear();
                String[] Lines = Regex.Split(PersonClass.Groups[1].Value, ",");

                foreach (string Line in Lines)
                {
                    String[] LineBoardings = Regex.Split(Line, ":");

                    LineName = LineBoardings[0].Trim(new char[]{' ', '\''});
                    if (LineName == "name")
                    {
                        ClassName = LineBoardings[1];
                    }
                    else
                    {
                        float.TryParse(LineBoardings[1], out Boardings);
                        result[LineName] = Boardings;
                    };

                };
                fullresults[ClassName] = result;
            };
            return fullresults;
        }

        private void SaveBoardings(FileLocation location, Dictionary<string, float> periodData)
        {
            using (StreamWriter writer = new StreamWriter(location))
            {
                writer.WriteLine("Line,Class,Boardings");
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
