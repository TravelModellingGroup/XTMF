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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using TMG.Input;
using Datastructure;
using TMG;
using TMG.DataUtility;
using Tasha.Common;
using XTMF;
using Tasha.XTMFModeChoice;

namespace Tasha.Validation.PerformanceMeasures
{
    public class GlobalWeightedAverages : ISelfContainedModule
    {
        [SubModelInformation(Required = true, Description = "Results in .CSV format")]
        public FileLocation GlobalAverageResults;

        [SubModelInformation(Required = false, Description = "The different links to consider")]
        public AverageOfInterest[] AveragesToConsider;

        public sealed class AverageOfInterest : XTMF.IModule
        {
            [RunParameter("Label", "WeightedAverageTotalTransitTime", "The appropriate label for global average")]
            public string Label;  
            
            [SubModelInformation(Required = true, Description = "Resource that returns the summation of the respective global average")]
            public IResource RespectiveGlobalAverage;

            public float ReturnData()
            {
                return RespectiveGlobalAverage.AquireResource<IDataSource<float>>().GiveData();
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
                if(!RespectiveGlobalAverage.CheckResourceType<float>())
                {
                    error = "In '" + Name + "' the RespectiveGlobalAverage was not of type float!";
                    return false;
                }

                return true;
            }
        }

        public void Start()
        {
            using (StreamWriter writer = new StreamWriter(GlobalAverageResults))
            {
                writer.WriteLine("Global Average,Value");
                foreach(var average in AveragesToConsider)
                {
                    writer.WriteLine("{0},{1}", average.Label, average.ReturnData());
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
            get { return new Tuple<byte, byte, byte>(120, 25, 100); }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
