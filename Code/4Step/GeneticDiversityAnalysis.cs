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
using System.IO;
using System.Xml;
using XTMF;
using Datastructure;

namespace James.UTDM
{
    public struct EstimationParameter
    {
        public string Name;
        public float Start;
        public float End;
    }

    public class GeneticDiversityAnalysis : IModelSystemTemplate
    {
        [RunParameter("Input Directory", "../../BeijingInput", "The directory to read the input from.")]
        public string InputBaseDirectory { get; set; }

        [RunParameter("Evaluation File", "Evaluation.csv", "The name of the file that has the genetic output.")]
        public string EvaluationFileName;

        [RunParameter("Parameter File", "", "(Optional) The location of the parameter file used.  Will use Percent Distance.")]
        public string ParameterFile;

        protected bool PercentDistance;

        protected EstimationParameter[] Parameters;
        protected int[] ParameterIndex;

        public string OutputBaseDirectory { get; set; }

        private string[] Headers;

        public bool ExitRequest()
        {
            return false;
        }

        protected class GenerationInformation
        {
            internal List<float[]> Entries;
        }

        public void Start()
        {
            Dictionary<int, GenerationInformation> generationDictionary = new Dictionary<int, GenerationInformation>();
            LoadParameters();
            // Load all of the information including the headers into the dictionary
            LoadInformation(generationDictionary);
            // Now that we have all of the information we can start to process the information
            using (StreamWriter writer = new StreamWriter("Output.csv"))
            {
                int numberOfParameters = this.Headers.Length;
                writer.Write("Generation,MinValue,AvgValue,AvgDistance,STDDistance");
                for(int i = 0; i < numberOfParameters; i++)
                {
                    writer.Write(',');
                    writer.Write("Avg");
                    writer.Write(this.Headers[i]);
                    writer.Write(',');
                    writer.Write("STD");
                    writer.Write(this.Headers[i]);
                    writer.Write(',');
                    writer.Write("Best");
                    writer.Write(this.Headers[i]);
                }
                writer.WriteLine();
                foreach(var entry in generationDictionary)
                {
                    var generation = entry.Key;
                    var entries = entry.Value;
                    float average = CalculateOverallAverage(entries);
                    float std = CalculateOverallSTD(entries, average);
                    float minValue;
                    float averageValue;
                    float[] best;
                    CalculateValues(entries, out minValue, out averageValue, out best);
                    writer.Write(generation);
                    writer.Write(',');
                    writer.Write(minValue);
                    writer.Write(',');
                    writer.Write(averageValue);
                    writer.Write(',');
                    writer.Write(average);
                    writer.Write(',');
                    writer.Write(std);
                    for(int i = 0; i < numberOfParameters; i++)
                    {
                        float avgDistance;
                        float stdDistance;
                        CalculateParameterValues(entries, out avgDistance, out stdDistance, i);
                        writer.Write(',');
                        writer.Write(avgDistance);
                        writer.Write(',');
                        writer.Write(stdDistance);
                        writer.Write(',');
                        writer.Write(best[i]);
                    }
                    writer.WriteLine();
                }
            }
        }

        private void LoadParameters()
        {
            // if we do not need to load the parameters just exit
            if(!this.PercentDistance)
            {
                return;
            }
            XmlDocument doc = new XmlDocument();
            try
            {
                doc.Load(this.GetFileName(this.ParameterFile));
            }
            catch
            {
                throw new XTMFRuntimeException("We were unable to load the parameter's file \"" + this.ParameterFile + "\"!");
            }
            var pList = new List<EstimationParameter>(10);
            var root = doc["Root"];
            this.ThrowIfNotExist(root);
            foreach(XmlNode child in root.ChildNodes)
            {
                if(child.NodeType == XmlNodeType.Comment) continue;
                var stop = child.Attributes["Maximum"];
                var start = child.Attributes["Minimum"];
                if(stop == null || start == null) continue;
                if(child.HasChildNodes)
                {
                    foreach(var subChild in child.ChildNodes)
                    {
                        var path = child.Attributes["ParameterPath"];
                        if(path == null) continue;
                        this.AddToParameterList(path, start, stop, pList);
                    }
                }
                else
                {
                    var path = child.Attributes["ParameterPath"];
                    if(path == null) continue;
                    this.AddToParameterList(path, start, stop, pList);
                }
            }
            this.Parameters = pList.ToArray();
            this.ParameterIndex = new int[this.Parameters.Length];
        }

        private void AddToParameterList(XmlAttribute path, XmlAttribute start, XmlAttribute stop, List<EstimationParameter> list)
        {
            this.ThrowIfNotExist(path);
            this.ThrowIfNotExist(start);
            this.ThrowIfNotExist(stop);
            list.Add(new EstimationParameter() { Name = path.InnerText, Start = float.Parse(start.InnerText), End = float.Parse(stop.InnerText) });
        }

        private void ThrowIfNotExist(object e)
        {
            if(e == null)
            {
                throw new XTMFRuntimeException("The parameter file is in an invalid format.");
            }
        }

        private void CalculateParameterValues(GenerationInformation entries, out float avgDistance, out float stdDistance, int pos)
        {
            var list = entries.Entries;
            CalculateDirectDistance(pos, list, out avgDistance, out stdDistance);
        }

        private void CalculateDirectDistance(int pos, List<float[]> list, out float avgDistance, out float stdDistance)
        {
            var numberOfEntries = list.Count;
            avgDistance = 0;
            stdDistance = 0;
            for(int i = 0; i < numberOfEntries; i++)
            {
                for(int j = i + 1; j < numberOfEntries; j++)
                {
                    avgDistance += this.Distance(list[i][pos + 1], list[j][pos + 1], pos);
                }
            }
            avgDistance /= (numberOfEntries * numberOfEntries - numberOfEntries) / 2.0f;
            for(int i = 0; i < numberOfEntries; i++)
            {
                for(int j = i + 1; j < numberOfEntries; j++)
                {
                    stdDistance += Math.Abs(this.Distance(list[i][pos + 1], list[j][pos + 1], pos) - avgDistance);
                }
            }
            stdDistance /= (numberOfEntries * numberOfEntries - numberOfEntries) / 2.0f;
        }

        private void CalculateValues(GenerationInformation entries, out float minValue, out float averageValue, out float[] best)
        {
            minValue = float.MaxValue;
            averageValue = 0;
            var list = entries.Entries;
            var numberOfEntries = list.Count;
            int minIndex = -1;
            for(int i = 0; i < numberOfEntries; i++)
            {
                var value = list[i][0];
                if(value < minValue)
                {
                    minValue = value;
                    minIndex = i;
                }
                averageValue += value;
            }
            if(minIndex >= 0)
            {
                best = new float[list[minIndex].Length - 1];
                Array.Copy(list[minIndex], 1, best, 0, best.Length);
            }
            else
            {
                best = new float[0];
            }
            averageValue /= (numberOfEntries * numberOfEntries - numberOfEntries) / 2.0f;
        }

        private float CalculateOverallAverage(GenerationInformation entries)
        {
            var list = entries.Entries;
            var length = list.Count;
            double average = 0;
            for(int i = 0; i < length - 1; i++)
            {
                for(int j = i + 1; j < length; j++)
                {
                    average += this.Distance(list[i], list[j]);
                }
            }
            return (float)(average / ((length * length - length) / 2.0));
        }

        private float CalculateOverallSTD(GenerationInformation entries, float average)
        {
            var list = entries.Entries;
            var length = list.Count;
            double std = 0;
            for(int i = 0; i < length - 1; i++)
            {
                for(int j = i + 1; j < length; j++)
                {
                    std += (float)Math.Abs(this.Distance(list[i], list[j]) - average);
                }
            }
            return (float)(std / ((length * length - length) / 2.0));
        }

        private double Distance(float[] first, float[] second)
        {
            double distance = 0;
            var numberOfParameters = first.Length;
            if(PercentDistance)
            {
                // the first entry is the value, so we can skip that
                for(int i = 1; i < numberOfParameters; i++)
                {
                    var parameter = this.Parameters[this.ParameterIndex[i - 1]];
                    var unit = (first[i] - second[i]) / (parameter.End - parameter.Start);
                    distance += unit * unit;
                }
            }
            else
            {
                // the first entry is the value, so we can skip that
                for(int i = 1; i < numberOfParameters; i++)
                {
                    var unit = first[i] - second[i];
                    distance = distance + (unit * unit);
                }
            }
            return (float)Math.Sqrt(distance);
        }

        private float Distance(float first, float second, int parameter)
        {
            float distance = 0;
            if(this.PercentDistance)
            {
                var unit = (first - second) / (this.Parameters[this.ParameterIndex[parameter]].End - this.Parameters[this.ParameterIndex[parameter]].Start);
                distance = unit * unit;
            }
            else
            {
                distance = (second - first) * (second - first);
            }
            return (float)Math.Sqrt(distance);
        }

        private void LoadInformation(Dictionary<int, GenerationInformation> generationDictionary)
        {
            using (CsvReader reader = new CsvReader(this.EvaluationFileName))
            {
                LoadHeader(reader);

                int numberOfParameters = this.Headers.Length;
                while(reader.LoadLine() != 0)
                {
                    int generation;
                    reader.Get(out generation, 0);
                    var parameters = new float[numberOfParameters + 1];
                    for(int i = 0; i < numberOfParameters + 1; i++)
                    {
                        // offset 1 for the generation i == 0 is the value
                        reader.Get(out parameters[i], i + 1);
                    }
                    GenerationInformation info;
                    if(!generationDictionary.TryGetValue(generation, out info))
                    {
                        info = new GenerationInformation();
                        info.Entries = new List<float[]>(250);
                        generationDictionary.Add(generation, info);
                    }
                    info.Entries.Add(parameters);
                }
            }
        }

        private void LoadHeader(CsvReader reader)
        {
            int columns;
            if((columns = reader.LoadLine()) == 0)
            {
                return;
            }
            this.Headers = new string[columns - 2];

            for(int i = 2; i < columns; i++)
            {
                reader.Get(out this.Headers[i - 2], i);
            }

            // If we have percent distance enabled go through and try to link the headers
            if(this.PercentDistance)
            {
                // if we are doing percent distance try to match the parameters to the header indexes
                if(this.Headers.Length != this.Parameters.Length)
                {
                    throw new XTMFRuntimeException("The number of headers did not match the number of parameters!\r\n"
                        + "The Parameter Range file has " + this.Parameters.Length + " and the Estimation Output has " + this.Headers.Length);
                }
                for(int i = 0; i < this.Parameters.Length; i++)
                {
                    bool found = false;
                    for(int j = 0; j < this.Headers.Length; j++)
                    {
                        if(this.Parameters[i].Name == this.Headers[j])
                        {
                            this.ParameterIndex[j] = i;
                            found = true;
                            break;
                        }
                    }
                    if(!found)
                    {
                        throw new XTMFRuntimeException("We were unable to find a header to match parameter " + Parameters[i].Name + "!");
                    }
                }
            }
        }

        public string Name { get; set; }

        public float Progress
        {
            get;
            set;
        }


        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(50, 100, 50);
        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return _ProgressColour; }
        }

        public bool RuntimeValidation(ref string error)
        {
            this.EvaluationFileName = this.GetFileName(this.EvaluationFileName);
            this.PercentDistance = !String.IsNullOrWhiteSpace(this.ParameterFile);
            return true;
        }

        private string GetFileName(string baseFileName)
        {
            if(Path.IsPathRooted(baseFileName)) return baseFileName;
            return Path.Combine(this.InputBaseDirectory, baseFileName);
        }

        public override string ToString()
        {
            return "Producing Genetic Health Metrics";
        }
    }
}
