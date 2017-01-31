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
    public abstract class RegionUtilityComponent : IUtilityComponent
    {
        [RunParameter( "Contains Both", false, "Will apply if both of the origin/destination are in the region.  Only one Contains can be set to true." )]
        public bool ContainsBoth;

        [RunParameter( "Contains None", false, "Will apply if neither the origin or destination are in the specified regions.  Only one Contains can be set to true." )]
        public bool ContainsNone;

        [RunParameter( "Contains One", true, "Will apply if either of the origin/destination are in the region.  Only one Contains can be set to true." )]
        public bool ContainsOne;

        [RunParameter( "Exlusive OR", false, "Only one of the origin/destination can be in the region?  Only one Contains can be set to true." )]
        public bool ExclusiveOr;

        [RunParameter( "Invalid Regions", "", typeof( RangeSet ), "A set of ranges for region numbers that will automatically exclude this utility Component." )]
        public RangeSet InvalidRegions;

        [RunParameter( "Must be different", false, "If true, the regions must be different from eachother." )]
        public bool OriginDestinationDifferent;

        [RunParameter( "Must be same", false, "If true, the regions must be the same." )]
        public bool OriginDestinationSame;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Valid Regions", "1", typeof( RangeSet ), "A set of ranges for region numbers that will trigger this utility component to be included." )]
        public RangeSet ValidRegions;

        private ContainsType CurrentContainsType;

        private enum ContainsType
        {
            One,
            ExclusiveOr,
            Both,
            None
        }

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

        [RunParameter( "Component Name", "", "The name of this Utility Component.  This name should be unique for each mode." )]
        public string UtilityComponentName { get; set; }

        public abstract float CalculateV(IZone origin, IZone destination, Time time);

        public bool RuntimeValidation(ref string error)
        {
            var total = 0;
            if ( ContainsBoth )
            {
                CurrentContainsType = ContainsType.Both;
                total++;
            }
            if ( ContainsNone )
            {
                CurrentContainsType = ContainsType.None;
                total++;
            }
            if ( ContainsOne )
            {
                CurrentContainsType = ContainsType.One;
                total++;
            }
            if ( ExclusiveOr )
            {
                CurrentContainsType = ContainsType.ExclusiveOr;
                total++;
            }
            if ( total != 1 )
            {
                error = "In '" + Name + "', exactly one of the Contains parameters must be set to true!";
                return false;
            }
            return SubRuntimeValidation( ref error );
        }

        public override string ToString()
        {
            return UtilityComponentName;
        }

        /// <summary>
        /// Call this to see if the given origin/destination should be accepted.
        /// </summary>
        /// <param name="origin">The zone the trip starts at</param>
        /// <param name="destination">The zone the trip ends at</param>
        /// <returns>If the zone pair should have the utility applied</returns>
        protected virtual bool IsContained(IZone origin, IZone destination)
        {
            if ( InvalidRegions.Count > 0
                && ( InvalidRegions.Contains( origin.RegionNumber ) || InvalidRegions.Contains( destination.RegionNumber ) ) )
            {
                return false;
            }

            var containsOrigin = ValidRegions.Contains( origin.RegionNumber );
            var containsDestination = ValidRegions.Contains( destination.RegionNumber );

            if ( OriginDestinationSame )
            {
                if ( origin.RegionNumber != destination.RegionNumber )
                {
                    return false;
                }
                return containsOrigin;
            }

            if ( OriginDestinationDifferent && origin.RegionNumber == destination.RegionNumber )
            {
                return false;
            }

            switch ( CurrentContainsType )
            {
                case ContainsType.ExclusiveOr:
                    return containsOrigin ^ containsDestination;

                case ContainsType.One:
                    return containsOrigin | containsDestination;

                case ContainsType.Both:
                    return containsOrigin & containsDestination;

                case ContainsType.None:
                    return !( containsOrigin | containsDestination );

                default:
                    throw new XTMFRuntimeException( "Unknown Contains Type!" );
            }
        }

        /// <summary>
        /// Runtime validation of things besides the ContainsX parameters
        /// </summary>
        /// <param name="error">The error message passed back</param>
        /// <returns>False if there was an error.</returns>
        protected abstract bool SubRuntimeValidation(ref string error);
    }
}