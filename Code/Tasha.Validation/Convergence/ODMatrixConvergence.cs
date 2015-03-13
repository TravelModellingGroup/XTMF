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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XTMF;
using TMG.Functions.VectorHelper;
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
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool managed)
        {
            if(Writer != null)
            {
                Writer.Dispose();
                Writer = null;
            }
        }

        ~ODMatrixConvergence()
        {
            Dispose(false);
        }

        public void Execute(int iterationNumber, int totalIterations)
        {
            if(Writer == null)
            {
                Writer = new StreamWriter(ReportFile);
                switch(AnalysisToRun)
                {
                    case AnalysisType.Average:
                        Writer.WriteLine("Iteration,Average");
                        break;
                    case AnalysisType.Max:
                        Writer.WriteLine("Iteration,Max");
                        break;
                }
            }
            float value = 0.0f;
            switch(AnalysisToRun)
            {
                case AnalysisType.Average:
                    value = GetAverage();
                    break;
                case AnalysisType.Max:
                    value = GetMax();
                    break;
            }
            Writer.Write(iterationNumber + 1);
            Writer.Write(',');
            Writer.WriteLine(value);
            // if this is the last iteration dispose
            if(iterationNumber >= totalIterations - 1)
            {
                Writer.Dispose();
            }
        }

        private float GetAverage()
        {
            var first = FirstMatrix.AquireResource<SparseTwinIndex<float>>().GetFlatData();
            var second = SecondMatrix.AquireResource<SparseTwinIndex<float>>().GetFlatData();
            var diff = 0.0f;
            if(IsHardwareAccelerated)
            {
                for(int i = 0; i < first.Length; i++)
                {
                    diff += VectorAbsDiffAverage(first[i], 0, second[i], 0, first[i].Length);
                }
            }
            else
            {
                for(int i = 0; i < first.Length; i++)
                {
                    var firstRow = first[i];
                    var secondRow = second[i];
                    var local = 0.0f;
                    for(int j = 0; j < firstRow.Length; j++)
                    {
                        local += Math.Abs(firstRow[j] - secondRow[j]);
                    }
                    diff += local / firstRow.Length;
                }
            }
            diff = diff / first.Length;
            FirstMatrix.ReleaseResource();
            SecondMatrix.ReleaseResource();
            return diff;
        }

        private float GetMax()
        {
            var first = FirstMatrix.AquireResource<SparseTwinIndex<float>>().GetFlatData();
            var second = SecondMatrix.AquireResource<SparseTwinIndex<float>>().GetFlatData();
            var diff = 0.0f;
            if(IsHardwareAccelerated)
            {
                for(int i = 0; i < first.Length; i++)
                {
                    diff = Math.Max(VectorAbsDiffMax(first[i], 0, second[i], 0, first[i].Length), diff);
                }
            }
            else
            {
                for(int i = 0; i < first.Length; i++)
                {
                    var firstRow = first[i];
                    var secondRow = second[i];
                    for(int j = 0; j < firstRow.Length; j++)
                    {
                        diff = Math.Max(Math.Abs(firstRow[j] - secondRow[j]), diff);
                    }
                }
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
            if(!FirstMatrix.CheckResourceType<SparseTwinIndex<float>>())
            {
                error = "In '" + Name + "' the FirstMatrix resource is not of type 'SparseTwinIndex<float>'!";
                return false;
            }
            if(!SecondMatrix.CheckResourceType<SparseTwinIndex<float>>())
            {
                error = "In '" + Name + "' the SecondMatrix resource is not of type 'SparseTwinIndex<float>'!";
                return false;
            }
            return true;
        }
    }
}
