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
using XTMF;

namespace TMG.GTAModel.Modes
{
    [ModuleInformation(Description=
        @"Nested choice provides the ability to create a nested-logit model.  
It can take a list of children and will take the max utility of its children as its own." )]
    public class NestedChoice : IModeCategory
    {
        [SubModelInformation( Description = "The modes or subcategories" )]
        public List<IModeChoiceNode> Children
        {
            get;
            set;
        }

        [RunParameter( "Correlation", 1f, "The correlation between the alternatives.  1 means no correlation, 0 means perfect correlation." )]
        public float Correlation
        {
            get;
            set;
        }

        [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
        public float CurrentlyFeasible { get; set; }

        [RunParameter( "Mode Name", "", "The name of the nested choice node." )]
        public string ModeName
        {
            get;
            set;
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
            get;
            set;
        }

        public virtual float CalculateCombinedV(IZone origin, IZone destination, Time time)
        {
            return 0;
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            if ( Children != null )
            {
                var length = Children.Count;
                var total = 0f;
                int alternatives = 0;
                for ( int i = 0; i < length; i++ )
                {
                    if ( Children[i].Feasible( origin, destination, time ) )
                    {
                        var u = Children[i].CalculateV( origin, destination, time );
                        if ( !float.IsNaN( u ) )
                        {
                            alternatives++;
                            total += (float)Math.Exp( u );
                        }
                    }
                }
                if ( alternatives == 0 )
                {
                    return float.NaN;
                }
                else
                {
                    var thisLevel = CalculateCombinedV( origin, destination, time );
                    return float.IsNaN( thisLevel ) ? float.NaN : ( (float)Math.Log( total ) * Correlation + thisLevel );
                }
            }
            return float.MinValue;
        }

        public virtual bool Feasible(IZone origin, IZone destination, Time time)
        {
            // make sure that we are allowed at this point
            return CurrentlyFeasible > 0;
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( String.IsNullOrWhiteSpace( ModeName ) )
            {
                error = "Please add in a 'Mode Name' for your nested choice!";
                return false;
            }
            if ( Correlation > 1 || Correlation < 0 )
            {
                error = "Correlation must be between 0 and 1 for " + ModeName + "!";
                return false;
            }
            return true;
        }
    }
}