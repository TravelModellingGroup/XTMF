﻿/*
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
using System.IO;
using TMG.Input;
using XTMF;

namespace Tasha.Validation.PerformanceMeasures
{
    public class GlobalWeightedAverages : ISelfContainedModule
    {
        [SubModelInformation(Required = true, Description = "Results in .CSV format")]
        public FileLocation MatrixOutputResults;

        [SubModelInformation(Required = false, Description = "The different Matrix results to write out")]
        public MatrixResultOutputs[] MatrixCalcOutputs;

        public sealed class MatrixResultOutputs : IModule
        {
            [RunParameter("Label", "WeightedAverageTotalTransitTime", "The appropriate label for global average")]
            public string Label;  
            
            [SubModelInformation(Required = true, Description = "Resource that returns the summation of the respective result matrix")]
            public IResource SummationOfResult;

            public float ReturnData()
            {
                return SummationOfResult.AcquireResource<float>();
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
                get { return new Tuple<byte, byte, byte>(120, 25, 100); }
            }

            public bool RuntimeValidation(ref string error)
            {
                if(!SummationOfResult.CheckResourceType<float>())
                {
                    error = "In '" + Name + "' the SummationfOfResult was not of type float!";
                    return false;
                }

                return true;
            }
        }

        public void Start()
        {
            using StreamWriter writer = new StreamWriter(MatrixOutputResults);
            writer.WriteLine("Global Average,Value");
            foreach (var average in MatrixCalcOutputs)
            {
                writer.WriteLine("{0},{1}", average.Label, average.ReturnData());
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
            get { return new Tuple<byte, byte, byte>(120, 25, 100); }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
