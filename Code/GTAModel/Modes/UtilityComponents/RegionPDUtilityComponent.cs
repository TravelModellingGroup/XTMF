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
using Datastructure;
using XTMF;

namespace TMG.GTAModel.Modes.UtilityComponents
{
    public abstract class RegionPDUtilityComponent : RegionUtilityComponent
    {
        [RunParameter( "Contains Both PD", false, "Will apply if both of the origin/destination are in the region.  Only one Contains X PD can be set to true." )]
        public bool ContainsBothPD;

        [RunParameter( "Contains None PD", false, "Will apply if neither the origin or destination are in the specified planning districts.  Only one Contains X PD can be set to true." )]
        public bool ContainsNonePD;

        [RunParameter( "Contains One PD", true, "Will apply if either of the origin/destination are in the region.  Only one Contains X PD can be set to true.." )]
        public bool ContainsOnePD;

        [RunParameter( "Exlusive OR PD", false, "Only one of the origin/destination can be in the region?  Only one Contains X PD can be set to true." )]
        public bool ExclusiveOrPD;

        [RunParameter( "Must be same PD", false, "If true, the PD must be the same." )]
        public bool OriginDestinationPDSame;

        [RunParameter( "Valid Planning Districts", "1", typeof( RangeSet ), "A set of ranges for planning districts that will trigger this utility component to be included."
            + " These are in addition to the region restrictions." )]
        public RangeSet ValidPlanningDistricts;

        protected override bool IsContained(IZone origin, IZone destination)
        {
            return CheckPDContained( origin, destination ) && base.IsContained( origin, destination );
        }

        protected override bool SubRuntimeValidation(ref string error)
        {
            if ( !ContainsBothPD ^ ContainsNonePD ^ ContainsOnePD ^ ExclusiveOrPD )
            {
                error = "In '" + Name + "', exactly one of the Contains X PD parameters must be set to true!";
                return false;
            }
            return true;
        }

        private bool CheckPDContained(IZone origin, IZone destination)
        {
            var containsOrigin = ValidPlanningDistricts.Contains( origin.PlanningDistrict );
            var containsDestination = ValidPlanningDistricts.Contains( destination.PlanningDistrict );

            if ( OriginDestinationPDSame )
            {
                if ( origin.PlanningDistrict != destination.PlanningDistrict )
                {
                    return false;
                }
                return containsOrigin;
            }

            if ( ExclusiveOrPD )
            {
                return containsOrigin ^ containsDestination;
            }
            else if ( ContainsOnePD )
            {
                return containsOrigin | containsDestination;
            }
            else if ( ContainsBothPD )
            {
                return containsOrigin & containsDestination;
            }
            else
            {
                return !( containsOrigin | containsDestination );
            }
        }
    }
}