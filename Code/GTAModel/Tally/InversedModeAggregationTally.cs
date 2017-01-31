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
using System.Threading.Tasks;
using XTMF;
namespace TMG.GTAModel
{
    [ModuleInformation( Description =
        @"The goal of this module is to provide the ability to create a purpose that is in fact 
just an O-D mirror of another purpose.  In order to use this, make sure that this purpose is 
processed after the purpose that it is going to be copying.  This can usually be done by having 
it farther down the list of purposes."
        )]
    public class InversedModeAggregationTally : DirectModeAggregationTally
    {
        public override void IncludeTally(float[][] currentTally)
        {
            var purposes = Root.Purpose;
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var numberOfZones = zones.Length;
            for ( int purp = 0; purp < PurposeIndexes.Length; purp++ )
            {
                var purpose = purposes[purp];
                for ( int m = 0; m < ModeIndexes.Length; m++ )
                {
                    var data = GetResult( purpose.Flows, ModeIndexes[m] );
                    // if there is no data continue on to the next mode
                    if ( data == null ) continue;
                    Parallel.For( 0, numberOfZones, delegate(int o)
                    {
                        if ( data[o] == null ) return;
                        for ( int d = 0; d < numberOfZones; d++ )
                        {
                            currentTally[d][o] += data[o][d];
                        }
                    } );
                }
            }
        }
    }
}