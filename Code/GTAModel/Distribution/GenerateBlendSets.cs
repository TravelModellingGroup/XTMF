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
using System.Collections.Generic;
using Datastructure;
using XTMF;

namespace TMG.GTAModel.Distribution
{
    public sealed class GenerateBlendSets : BlendSet
    {
        [ParentModel]
        public MultiBlendSet Parent;

        public override bool RuntimeValidation(ref string error)
        {
            Parent.Subsets.Remove( this );
            foreach ( var set in this.Set )
            {
                for ( int i = set.Start; i <= set.Stop; i++ )
                {
                    var item = new List<Range>( 1 );
                    item.Add( new Range() { Start = i, Stop = i } );
                    Parent.Subsets.Add( new BlendSet() { Set = new Datastructure.RangeSet( item ) } );
                }
            }
            return true;
        }
    }
}