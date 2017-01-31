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
using TMG;
using XTMF;
using Datastructure;
namespace TMG.GTAModel.DataResources
{
    [ModuleInformation(Description=
        @"Provides a way of storing work related data in for the format [zone][EmploymentCategoryRange,MobilityRange,AgeRange]."
        )]
    public class WorkGenerationData : IDataSource<SparseArray<SparseTriIndex<float>>>
    {
        [RootModule]
        public ITravelDemandModel Root;
        [Parameter( "Employment Categories", "1-2", typeof( RangeSet ), "The ranges of employment categories." )]
        public RangeSet EmploymentCategoryRange;
        [Parameter( "Mobility Categories", "0-5", typeof( RangeSet ), "The ranges of mobility categories." )]
        public RangeSet MobilityRange;
        [Parameter( "Age Categories", "1-6", typeof( RangeSet ), "The ranges of age categories." )]
        public RangeSet AgeRange;


        private SparseArray<SparseTriIndex<float>> Data;
        public SparseArray<SparseTriIndex<float>> GiveData()
        {
            return Data;
        }

        public bool Loaded
        {
            get { return Data != null; }
        }

        public void LoadData()
        {
            // get a replica of the zone system
            SparseArray<SparseTriIndex<float>> temp = Root.ZoneSystem.ZoneArray.CreateSimilarArray<SparseTriIndex<float>>();
            var total = ( EmploymentCategoryRange[0].Stop - EmploymentCategoryRange[0].Start + 1 )
                * ( MobilityRange[0].Stop - MobilityRange[0].Start + 1 )
                * ( AgeRange[0].Stop - AgeRange[0].Start + 1 );
            int[] first = new int[total];
            int[] second = new int[total];
            int[] third = new int[total];
            float[] data = new float[total];
            int pos = 0;
            for ( int i = EmploymentCategoryRange[0].Start; i <= EmploymentCategoryRange[0].Stop; i++ )
            {
                for ( int j = MobilityRange[0].Start; j <= MobilityRange[0].Stop; j++ )
                {
                    for ( int k = AgeRange[0].Start; k <= AgeRange[0].Stop; k++ )
                    {
                        first[pos] = i;
                        second[pos] = j;
                        third[pos] = k;
                        data[pos] = 0f;
                        pos++;
                    }
                }
            }
            var flatTemp = temp.GetFlatData();
            // initialize the first one, and use it for the first zone
            flatTemp[0] = SparseTriIndex<float>.CreateSparseTriIndex( first, second, third, data );
            //release memory resources before we allocate a bunch more
            first = second = third = null;
            // now create a clone of this for each zone [i starts at 1 since 0 has already been set]
            for ( int i = 1; i < flatTemp.Length; i++ )
            {
                flatTemp[i] = flatTemp[0].CreateSimilarArray<float>();
            }
            Data = temp;
        }

        public void UnloadData()
        {
            Data = null;
        }

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
