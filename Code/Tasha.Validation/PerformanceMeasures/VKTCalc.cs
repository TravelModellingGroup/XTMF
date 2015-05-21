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
using Tasha.Common;
using XTMF;
using Tasha.XTMFModeChoice;
using TMG.Functions;

namespace Tasha.Validation.PerformanceMeasures
{
    public class VKTCalc : ISelfContainedModule
    {
        [RootModule]
        public ITashaRuntime Root;

        Dictionary<int, float> TotalVKT;

        [RunParameter("Cost per Km", 0.153f, "What is the cost per km used in this model system?")]
        public float CostPerKm;

        [SubModelInformation(Required = false, Description = "The different time periods you wish to calculate VKTs for")]
        public VKTPerTimePeriod[] TimePeriods;


        public sealed class VKTPerTimePeriod : XTMF.IModule
        {
            [RunParameter("Time Period", "AM", "Which time period do you want to analyze?")]
            public string Label;

            [SubModelInformation(Required = true, Description = ".CSV File containing the ODTrips for this Time Period.")]
            public FileLocation ODTripsData;

            [SubModelInformation(Required = true, Description = "Results file in .CSV format for this Time period")]
            public FileLocation VKTbyHomeZone;

            [SubModelInformation(Required = true, Description = "Resource that will subtract the two Cost Matrices and return a Flat Cost.")]
            public IResource ODFlatCostMatrix;

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
                if(!ODFlatCostMatrix.CheckResourceType<SparseTwinIndex<float>>())
                {
                    error = "In '" + Name + "' the ODDistanceMatrix was not of type SparseTwinIndex<float>!";
                    return false;
                }
                return true;
            }
        }

        public void Start()
        {
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            char[] separators = { ',' };
            var invCostPerKM = 1.0f / CostPerKm;
            foreach(var timePeriod in TimePeriods)
            {
                TotalVKT = new Dictionary<int, float>();
                var odCostMatrix = timePeriod.ODFlatCostMatrix.AquireResource<SparseTwinIndex<float>>();
                using (CsvReader reader = new CsvReader(timePeriod.ODTripsData))
                {
                    int columns;
                    while(reader.LoadLine(out columns))
                    {
                        if(columns >= 4)
                        {
                            float vkt = 0.0f;
                            int homeZone, origin, destination;
                            float numberOfTrips;
                            reader.Get(out homeZone, 0);
                            reader.Get(out origin, 1);
                            reader.Get(out destination, 2);
                            reader.Get(out numberOfTrips, 3);
                            var distance = odCostMatrix[origin, destination] * invCostPerKM;
                            TotalVKT.TryGetValue(homeZone, out vkt);
                            TotalVKT[homeZone] = vkt + numberOfTrips * distance;
                        }
                    }
                }
                using (StreamWriter writer = new StreamWriter(timePeriod.VKTbyHomeZone))
                {
                    writer.WriteLine("Home Zone, Total VKTs");
                    foreach(var pair in TotalVKT)
                    {
                        writer.WriteLine("{0}, {1}", pair.Key, pair.Value);
                    }
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
