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
using System.IO;
using System.Linq;
using System.Threading;
using Datastructure;
using Tasha.Common;
using TMG;
using TMG.Input;
using XTMF;

namespace Tasha.Validation.PoRPoW
{
    public class LinkageDistanceHistogram : IPostHousehold
    {

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter("Bins", "0-5,5-10,10-15,15-20,20-30,30-40", typeof(RangeSet), "")]
        public RangeSet HistogramBins;

        [SubModelInformation(Description = "Save file location", Required = true)]
        public FileLocation SaveFile;

        [RunParameter("Coordinate Factor", 0.001f, "Convert from coordinate units to length units. For example, enter 0.001 to convert from coordinate meters to km.")]
        public float CoordinateFactor;

        private SparseTwinIndex<float> _ZoneDistances;
        SpinLock WriteLock = new(false);
        private float[][] _BinData;

        public string Name
        {
            get; set;
        }

        public float Progress { get; } = 0.0f;

        public Tuple<byte, byte, byte> ProgressColour
        {
            get
            {
                return null;
            }
        }



        public void Execute(ITashaHousehold household, int iteration)
        {
            //Determine the worker category
            int nVehicles = household.Vehicles.Length;
            int nDrivers = household.Persons.Count(p => p.Licence);

            int wcat;
            if (nVehicles == 0)
            {
                wcat = 0;
            }
            else
            {
                wcat = (nVehicles > nDrivers) ? 2 : 1;
            }

            foreach (var person in household.Persons)
            {
                var empStat = person.EmploymentStatus;
                if (empStat == TTSEmploymentStatus.FullTime | empStat == TTSEmploymentStatus.PartTime) continue; //Skip unemployed persons
                IZone employmentZone = person.EmploymentZone;
                if ( employmentZone == null ) continue;
                var distance = (int) (_ZoneDistances[household.HomeZone.ZoneNumber, employmentZone.ZoneNumber] * CoordinateFactor);
                int index = HistogramBins.IndexOf(distance);
                if (index < 0)
                {
                    index = HistogramBins.Count;
                }
                bool taken = false;
                WriteLock.Enter(ref taken);
                _BinData[index][wcat] += person.ExpansionFactor;
                if (taken) WriteLock.Exit(true);
            }
        }

        public void IterationFinished(int iteration)
        {
            using (var writer = new StreamWriter(SaveFile.GetFilePath()))
            {
                writer.WriteLine("Distance,WCAT 0,WCAT 1,WCAT 2");

                for (int i = 0; i < HistogramBins.Count; i++)
                {
                    var range = HistogramBins[i];
                    var distances = _BinData[i];
                    var line = string.Join(",", range.Start + " - " + range.Stop,
                                            distances[0], distances[1], distances[2]);
                    writer.WriteLine(line);
                }

                var lastdistances = _BinData[HistogramBins.Count];
                var lastline = string.Join(",", HistogramBins[HistogramBins.Count - 1] + "+",
                                            lastdistances[0], lastdistances[1], lastdistances[2]);
                writer.WriteLine(lastline);
            }

            Console.WriteLine("Exported PoRPoW linkage distance histogram to " + SaveFile.GetFilePath());
        }

        public void Load(int maxIterations)
        {
            _ZoneDistances = Root.ZoneSystem.Distances;

            _BinData = new float[1 + HistogramBins.Count][]; //Extra bin for outside of the array
            for (int i = 0; i < _BinData.Length; i++)
            {
                _BinData[i] = new float[3]; 
            }
            
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void IterationStarting(int iteration)
        {
        }
    }
}
