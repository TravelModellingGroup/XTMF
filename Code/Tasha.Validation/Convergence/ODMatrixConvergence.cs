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
using XTMF;
using TMG.Functions;
using System.IO;
using Tasha.Common;
using TMG.Input;
using Datastructure;

namespace Tasha.Validation.Convergence
{

    public sealed class ODMatrixConvergence : IPostIteration, IDisposable
    {

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private StreamWriter Writer;

        [SubModelInformation(Required = true, Description = "The first matrix to compare.")]
        public IResource FirstMatrix;

        [SubModelInformation(Required = true, Description = "The second matrix to compare.")]
        public IResource SecondMatrix;

        [RunParameter("Sum First", true, "Should we also provide a sum of the first matrix?")]
        public bool SumFirst;

        [SubModelInformation(Required = true, Description = "The location to save the report to.")]
        public FileLocation ReportFile;

        public enum AnalysisType
        {
            Average = 0,
            Max = 1
        }

        [RunParameter("Analysis", "Average", typeof(AnalysisType), "The type of analysis to execute.  Options are 'Average', and 'Max'.")]
        public AnalysisType AnalysisToRun;

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool managed)
        {
            if (managed)
            {
                GC.SuppressFinalize(this);
            }
            Writer?.Dispose();
            Writer = null;
        }

        ~ODMatrixConvergence()
        {
            Dispose(false);
        }

        public void Execute(int iterationNumber, int totalIterations)
        {
            if (Writer == null)
            {
                Writer = new StreamWriter(ReportFile);
                switch (AnalysisToRun)
                {
                    case AnalysisType.Average:
                        Writer.Write("Iteration,Average");
                        break;
                    case AnalysisType.Max:
                        Writer.Write("Iteration,Max");
                        break;
                }
                Writer.WriteLine(SumFirst ? ",SumOfFirst" : "");
            }
            var first = FirstMatrix.AcquireResource<SparseTwinIndex<float>>().GetFlatData();
            var second = SecondMatrix.AcquireResource<SparseTwinIndex<float>>().GetFlatData();
            float value = 0.0f;
            switch (AnalysisToRun)
            {
                case AnalysisType.Average:
                    value = GetAverage(first, second);
                    break;
                case AnalysisType.Max:
                    value = GetMax(first, second);
                    break;
            }
            Writer.Write(iterationNumber + 1);
            Writer.Write(',');
            Writer.Write(value);
            if (SumFirst)
            {
                var sum = 0.0f;
                for (int i = 0; i < first.Length; i++)
                {
                    sum += VectorHelper.Sum(first[i], 0, first[i].Length);
                }
                Writer.Write(',');
                Writer.Write(sum);
            }
            Writer.WriteLine();
            // if this is the last iteration dispose
            if (iterationNumber >= totalIterations - 1)
            {
                Writer.Dispose();
                Writer = null;
            }
        }

        private float GetAverage(float[][] first, float[][] second)
        {
            var diff = 0.0f;
            for (int i = 0; i < first.Length; i++)
            {
                diff += VectorHelper.AbsDiffAverage(first[i], 0, second[i], 0, first[i].Length);
            }

            diff = diff / first.Length;
            FirstMatrix.ReleaseResource();
            SecondMatrix.ReleaseResource();
            return diff;
        }

        private float GetMax(float[][] first, float[][] second)
        {
            var diff = 0.0f;
            for (int i = 0; i < first.Length; i++)
            {
                diff = Math.Max(VectorHelper.AbsDiffMax(first[i], 0, second[i], 0, first[i].Length), diff);
            }
            FirstMatrix.ReleaseResource();
            SecondMatrix.ReleaseResource();
            return diff;
        }

        public void Load(IConfiguration config, int totalIterations)
        {

        }

        public bool RuntimeValidation(ref string error)
        {
            if (!FirstMatrix.CheckResourceType<SparseTwinIndex<float>>())
            {
                error = "In '" + Name + "' the FirstMatrix resource is not of type 'SparseTwinIndex<float>'!";
                return false;
            }
            if (!SecondMatrix.CheckResourceType<SparseTwinIndex<float>>())
            {
                error = "In '" + Name + "' the SecondMatrix resource is not of type 'SparseTwinIndex<float>'!";
                return false;
            }
            return true;
        }
    }
}
