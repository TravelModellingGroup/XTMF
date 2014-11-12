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
using Datastructure;
using XTMF;

namespace TMG.GTAModel.Modes.FeasibilityCalculations
{
    [ModuleInformation(
        Description = "This feasibility rule will describes the interaction of going into a radius, where origin has to be outside of it and the destination is inside."
        + "  This is primarly used for the V2 Drive Access Subway mode.  A set of zones is used to compute the minimum distance zone to use as the radius"
        )]
    public class RadialDistanceRequirment : ICalculation<Pair<IZone, IZone>, bool>
    {
        [RunParameter( "PointX", 0f, "The X coordinate of the point in space where we are going to measure from." )]
        public float PointX;

        [RunParameter( "PointY", 0f, "The Y coordinate of the point in space where we are going to measure from." )]
        public float PointY;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Zones", "", typeof( RangeSet ), "The closest zone to point will be used as the minimum distance allowed." )]
        public RangeSet TestAgainstZones;

        // we are going to use a double here since sqrt will be a double which means less conversions aka faster
        private double MinimumDistance = -1f;

        public string Name { get; set; }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public void Load()
        {
            double minDistance = double.PositiveInfinity;
            var zones = this.Root.ZoneSystem.ZoneArray;
            bool any = false;
            for ( int i = 0; i < TestAgainstZones.Count; i++ )
            {
                for ( int j = TestAgainstZones[i].Start; j <= TestAgainstZones[i].Stop; j++ )
                {
                    var zone = zones[j];
                    if ( zone != null )
                    {
                        any = true;
                        var distance = CalcDistance( zone );
                        if ( distance < minDistance )
                        {
                            minDistance = distance;
                        }
                    }
                }
            }
            if ( !any )
            {
                throw new XTMFRuntimeException( "In '" + this.Name + "' we were unable to find any zone number in the range '" + this.TestAgainstZones.ToString() + "' in order to compute the minimum distance!" );
            }
            this.MinimumDistance = minDistance;
        }

        public bool ProduceResult(Pair<IZone, IZone> data)
        {
            if ( this.MinimumDistance < 0 )
            {
                Load();
            }
            return TestOrigin( data.First ) && TestDestination( data.Second );
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( this.TestAgainstZones.Count <= 0 )
            {
                error = "In '" + this.Name + "' you need to select at least one zone to be used to compute the minimum distance for the origin, and the containment for the destination!.";
                return false;
            }
            return true;
        }

        public void Unload()
        {
            this.MinimumDistance = -1.0;
        }

        private double CalcDistance(IZone zone)
        {
            var x = zone.X;
            var y = zone.Y;
            return Math.Sqrt( ( x - this.PointX ) * ( x - this.PointX )
                            + ( y - this.PointY ) * ( y - this.PointY ) );
        }

        private bool TestDestination(IZone destination)
        {
            // the origin needs to be outside of the radius
            return CalcDistance( destination ) <= this.MinimumDistance;
        }

        private bool TestOrigin(IZone origin)
        {
            // the origin needs to be outside of the radius
            return CalcDistance( origin ) >= this.MinimumDistance;
        }
    }
}