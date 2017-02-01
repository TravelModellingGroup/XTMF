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
using XTMF;

namespace TMG.GTAModel.Modes
{
    [ModuleInformation(Description=
        @"This mode category provides the ability to include densities into the systematic 
utility of at the nested level." )]
    public class DensityCompositeMode : NestedChoice
    {
        [RunParameter( "Destination Employment Density", 0.0f, "The weight to use for the employment density of the destination zone." )]
        public float DestinationEmploymentDensity;

        [RunParameter( "Destination Population Density", 0.0f, "The weight to use for the population density of the destination zone." )]
        public float DestinationPopulationDensity;

        [RunParameter( "Origin Employment Density", 0.0f, "The weight to use for the employment density of the origin zone." )]
        public float OriginEmploymentDensity;

        [RunParameter( "Origin Population Density", 0.0f, "The weight to use for the population density of the origin zone." )]
        public float OriginPopulationDensity;

        public override float CalculateCombinedV(IZone origin, IZone destination, Time time)
        {
            // the factors convert the unit measured to the amount per km^2 of the zone
            // inversed so that we can use multiplication to process faster
            var originFactor = 1f / ( 1000f / origin.InternalArea );
            var destinationFactor = 1f / ( 1000f / destination.InternalArea );
            return (float)(
                ( OriginPopulationDensity != 0 ? Math.Log( origin.Population * originFactor + 1 ) * OriginPopulationDensity : 0f )
                + ( DestinationPopulationDensity != 0 ? Math.Log( destination.Population * destinationFactor + 1 ) * DestinationPopulationDensity : 0 )
                + ( OriginEmploymentDensity != 0 ? Math.Log( origin.Employment * originFactor + 1 ) * OriginEmploymentDensity : 0 )
                + ( DestinationEmploymentDensity != 0 ? Math.Log( destination.Employment * destinationFactor + 1 ) * DestinationEmploymentDensity : 0 )
                );
        }
    }
}