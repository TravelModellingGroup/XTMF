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
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.Estimation.Utilities
{
    public class EvaluateEvaluationResults : ISelfContainedModule
    {

        [RootModule]
        public IEstimationClientModelSystem Root;

        [SubModelInformation(Required = true, Description = "The file that contains the results.")]
        public FileLocation ResultFile;

        [RunParameter("Maximize", true, "Should we be trying to maximize (true) or minimize (false) the function?")]
        public bool Maximize;

        [RunParameter("Generation Error", 1.0f, "The additional error given by how many generations it takes to get close to the best value.")]
        public float GenerationError;

        public string Name { get; set; }

        public float Progress
        {
            get
            {
                return 0f;
            }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get
            {
                return new Tuple<byte, byte, byte>( 50, 150, 50 );
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            GetBestUtility(out int generation, out float value);
            Root.RetrieveValue = () => value + generation * GenerationError;
        }

        private void GetBestUtility(out int generation, out float value)
        {
            using CsvReader reader = new CsvReader(ResultFile);
            GetBest(reader, out generation, out value);
        }

        private void GetBest(CsvReader reader, out int generation, out float value)
        {
            // burn the header
            reader.LoadLine();
            if (Maximize)
            {
                GetHighestBest(reader, out generation, out value);
            }
            else
            {
                GetLowestBest(reader, out generation, out value);
            }
        }

        private bool ReadJob(CsvReader reader, out int generation, out float value)
        {
            while (reader.LoadLine(out int columns))
            {
                if (columns >= 2)
                {
                    reader.Get(out generation, 0);
                    reader.Get(out value, 1);
                    return true;
                }
            }
            value = float.NaN;
            generation = -1;
            return false;
        }

        private void GetLowestBest(CsvReader reader, out int bestGeneration, out float value)
        {
            float best = float.MaxValue;
            bestGeneration = 0;
            while (ReadJob(reader, out int generation, out float current))
            {
                //check the last one first since they are in order to see if we need to check each one
                if (current < best)
                {
                    best = current;
                    bestGeneration = generation;
                }
            }
            value = best;
        }

        private void GetHighestBest(CsvReader reader, out int bestGeneration, out float value)
        {
            float best = float.MinValue;
            bestGeneration = 0;
            while (ReadJob(reader, out int generation, out float current))
            {
                //check the last one first since they are in order to see if we need to check each one
                if (current > best)
                {
                    best = current;
                    bestGeneration = generation;
                }
            }
            value = best;
        }
    }
}
