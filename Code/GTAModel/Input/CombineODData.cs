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
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Input
{
    [ModuleInformation( Description = "This module allows you to combine several IReadODData<float> sources and add their data together into the travel demand model's zone system." )]
    public class CombineODData : IReadODData<float>
    {
        [SubModelInformation( Description = "The ODData to read from", Required = false )]
        public List<IReadODData<float>> DataSources;

        [RootModule]
        public ITravelDemandModel Root;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public IEnumerable<ODData<float>> Read()
        {
            var zones = Root.ZoneSystem.ZoneArray;
            var matrix = zones.CreateSquareTwinArray<float>().GetFlatData();
            CombineData( zones, matrix );
            var zoneIndexes = zones.ValidIndexArray();
            ODData<float> currentData = new ODData<float>();
            for ( int i = 0; i < matrix.Length; i++ )
            {
                currentData.O = zoneIndexes[i];
                for ( int j = 0; j < matrix[i].Length; j++ )
                {
                    currentData.D = zoneIndexes[j];
                    currentData.Data = matrix[i][j];
                    yield return currentData;
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private void CombineData(Datastructure.SparseArray<IZone> zones, float[][] matrix)
        {
            foreach ( var source in DataSources )
            {
                foreach ( var dataPoint in source.Read() )
                {
                    var o = zones.GetFlatIndex( dataPoint.O );
                    var d = zones.GetFlatIndex( dataPoint.D );
                    matrix[o][d] += dataPoint.Data;
                }
            }
        }
    }
}