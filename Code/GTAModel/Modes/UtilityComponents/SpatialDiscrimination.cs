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
using TMG.Modes;
using XTMF;

namespace TMG.GTAModel.Modes.UtilityComponents
{
    public abstract class SpatialDiscrimination : IUtilityComponent
    {
        [RunParameter( "Destination Contained", true, "This will only be applied if the destination is contained.  If false, it will only be applied if the destination is not contained." )]
        public bool DestinationContained;

        [RunParameter( "Destination Planning District", false, "The valid destinations refer to planning district numbers." )]
        public bool DestinationPlanningDistrict;

        [RunParameter( "Destination Region", true, "The valid destinations refer to region numbers." )]
        public bool DestinationRegion;

        [RunParameter( "Destination Zone", false, "The valid destinations refer to zone numbers." )]
        public bool DestinationZone;

        [RunParameter( "Origin Contained", true, "This will only be applied if the origin is contained.  If false, it will only be applied if the origin is not contained." )]
        public bool OriginContained;

        [RunParameter( "Origin Destination Same", false, "This will only be applied if the origin and destination are the same." )]
        public bool OriginDestinationSame;

        [RunParameter( "Origin Planning District", false, "The valid origins refer to planning district numbers." )]
        public bool OriginPlanningDistrict;

        [RunParameter( "Origin Region", true, "The valid origins refer to region numbers." )]
        public bool OriginRegion;

        [RunParameter( "Origin Zone", false, "The valid origins refer to zone numbers." )]
        public bool OriginZone;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Valid Destinations", "1", typeof( RangeSet ), "A set of ranges for region/Planning District numbers that will trigger this utility component to be included for the destination." )]
        public RangeSet ValidDestinations;

        [RunParameter( "Valid Origins", "1", typeof( RangeSet ), "A set of ranges for region/Planning District numbers that will trigger this utility component to be included for the origin." )]
        public RangeSet ValidOrigins;

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        [RunParameter( "Component Name", "", "The name of this Utility Component.  This name should be unique for each mode." )]
        public string UtilityComponentName { get; set; }

        public abstract float CalculateV(IZone origin, IZone destination, Time time);

        public bool RuntimeValidation(ref string error)
        {
            int origin = 0;
            int destination = 0;
            if ( OriginRegion ) origin++;
            if ( OriginPlanningDistrict ) origin++;
            if ( OriginZone ) origin++;

            if ( DestinationRegion ) destination++;
            if ( DestinationPlanningDistrict ) destination++;
            if ( DestinationZone ) destination++;
            if ( origin != 1 )
            {
                error = "In '" + Name + "' exactly one origin zonal type must be selected!";
                return false;
            }
            if ( destination != 1 )
            {
                error = "In '" + Name + "' exactly one destination zonal type must be selected!";
                return false;
            }
            if ( OriginDestinationSame && (
                ( OriginRegion != DestinationRegion )
                || ( OriginPlanningDistrict != DestinationPlanningDistrict )
                || ( OriginZone != DestinationZone )
                ) )
            {
                error = "In '" + Name + "' the spatial size of the origin and destination are not the same when also specifying that the origin and destination must be the same location!";
                return false;
            }
            return SubRuntimeValidation( ref error );
        }

        protected bool IsContained(IZone origin, IZone destination)
        {
            var originContained = IsContained( origin, ValidOrigins, OriginContained, OriginRegion, OriginPlanningDistrict );
            var destinationContained = IsContained( destination, ValidDestinations, DestinationContained, DestinationRegion, DestinationPlanningDistrict );
            if ( OriginDestinationSame )
            {
                return ( originContained & destinationContained ) && CheckOriginDestinationSame( origin, destination );
            }
            return ( originContained & destinationContained );
        }

        protected abstract bool SubRuntimeValidation(ref string error);

        private static bool IsContained(IZone zone, RangeSet validRanges, bool contained, bool useZoneRegion, bool useZonePlanningDistrict)
        {
            if ( useZoneRegion )
            {
                return ( !contained ) ^ validRanges.Contains( zone.RegionNumber );
            }
            if ( useZonePlanningDistrict )
            {
                return ( !contained ) ^ validRanges.Contains( zone.PlanningDistrict );
            }
            return ( !contained ) ^ validRanges.Contains( zone.ZoneNumber );
        }

        private bool CheckOriginDestinationSame(IZone origin, IZone destination)
        {
            if ( OriginRegion )
            {
                return origin.RegionNumber == destination.RegionNumber;
            }
            if ( OriginPlanningDistrict )
            {
                return origin.PlanningDistrict == destination.PlanningDistrict;
            }
            return origin.ZoneNumber == destination.ZoneNumber;
        }
    }
}