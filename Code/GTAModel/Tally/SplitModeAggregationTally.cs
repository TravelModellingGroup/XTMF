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
using System.Threading.Tasks;
using XTMF;

namespace TMG.GTAModel
{
    public class SplitModeAggregationTally : DirectModeAggregationTally
    {
        [RunParameter( "Count From Origin", true, "Should we be tallying from the origin to the intermediate zone" +
            "\r\nor should we be counting from the intermediate zone to the destination?" )]
        public bool CountFromOrigin;

        [RunParameter( "Intermediate Zone", 7000, "Which zone should we use as the intermediate?" )]
        public int IntermediateZone;

        public override void IncludeTally(float[][] currentTally)
        {
            var purposes = Root.Purpose;
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var numberOfZones = zones.Length;
            int modeFlatZone = Root.ZoneSystem.ZoneArray.GetFlatIndex( IntermediateZone );
            if ( modeFlatZone == -1 )
            {
                throw new XTMFRuntimeException( "The intermediate zone '" + IntermediateZone + " does not exist in the zone system!" );
            }
            for ( int purp = 0; purp < PurposeIndexes.Length; purp++ )
            {
                var purpose = purposes[purp];
                for ( int m = 0; m < ModeIndexes.Length; m++ )
                {
                    var data = GetResult( purpose.Flows, ModeIndexes[m] );
                    // if there is no data continue on to the next mode
                    if ( data == null ) continue;
                    if ( CountFromOrigin )
                    {
                        Parallel.For( 0, numberOfZones, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                            delegate(int o)
                            {
                                if ( data[o] == null ) return;
                                for ( int d = 0; d < numberOfZones; d++ )
                                {
                                    currentTally[o][modeFlatZone] += data[o][d];
                                }
                            } );
                    }
                    else
                    {
                        // we have to go parallel on the destination or we will have overlap in parallel
                        Parallel.For( 0, numberOfZones, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                            delegate(int d)
                            {
                                for ( int o = 0; o < numberOfZones; o++ )
                                {
                                    if ( data[o] == null ) continue;
                                    currentTally[modeFlatZone][d] += data[o][d];
                                }
                            } );
                    }
                }
            }
        }
    }
}