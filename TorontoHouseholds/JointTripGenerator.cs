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
using Tasha.Common;

namespace TMG.Tasha
{
    internal static class JointTripGenerator
    {
        internal static IMode Auto;
        internal static string ObsMode;
        internal static IMode Passenger;
        internal static IMode RideShare;

        internal static void Convert(ITashaHousehold house)
        {
            int JointTourNumber = 1;
            // we don't need to look at the last person
            for ( int person = 0; person < house.Persons.Length - 1; person++ )
            {
                foreach ( var chain in house.Persons[person].TripChains )
                {
                    if ( chain.JointTrip )
                    {
                        continue;
                    }
                    for ( int otherPerson = person + 1; otherPerson < house.Persons.Length; otherPerson++ )
                    {
                        foreach ( var otherChain in house.Persons[otherPerson].TripChains )
                        {
                            if ( otherChain.JointTrip )
                            {
                                continue;
                            }
                            if ( AreTogether( chain, otherChain ) )
                            {
                                int tourNum = JointTourNumber;
                                if ( !chain.JointTrip )
                                {
                                    ReAssignPurpose( chain );
                                    ( (TripChain)chain ).JointTripID = ( (TripChain)otherChain ).JointTripID = tourNum;
                                    ( (TripChain)chain ).JointTripRep = true;
                                    ReassignObservedModes( chain );
                                    JointTourNumber++;
                                }
                                ( (TripChain)otherChain ).JointTripID = chain.JointTripID;
                                ( (TripChain)otherChain ).GetRepTripChain = chain;
                                ReAssignPurpose( otherChain );
                                ReassignObservedModes( otherChain );
                            }
                        }
                    }
                }
            }
        }

        private static bool AreTogether(ITripChain f, ITripChain s)
        {
            if (f.Trips.Count != s.Trips.Count) return false;
            var fTrips = f.Trips;
            var sTrips = s.Trips;
            for (int i = 0; i < fTrips.Count; i++)
            {
                if (!AreTogether(fTrips[i], sTrips[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool AreTogether(ITrip f, ITrip s)
        {
            return ( f.TripStartTime == s.TripStartTime )
                 & ( f.Purpose == s.Purpose )
                 & ( f.Purpose == Activity.IndividualOther | f.Purpose == Activity.Market | f.Purpose == Activity.Home )
                 & ( f.OriginalZone.ZoneNumber == s.OriginalZone.ZoneNumber )
                 & ( f.DestinationZone.ZoneNumber == s.DestinationZone.ZoneNumber );
        }

        private static void ReassignObservedModes(ITripChain chain)
        {
            if ( RideShare == null )
            {
                return;
            }
            var trips = chain.Trips;
            var numberOfTrips = trips.Count;
            for ( int i = 0; i < numberOfTrips; i++ )
            {
                var obsMode = trips[i][ObsMode] as IMode;
                if ( obsMode != null )
                {
                    if ( obsMode.ModeName == Auto.ModeName || obsMode.ModeName == Passenger.ModeName )
                    {
                        trips[i].Attach( ObsMode, RideShare );
                    }
                }
            }
        }

        private static void ReAssignPurpose(ITripChain chain)
        {
            foreach ( var t in chain.Trips )
            {
                switch ( t.Purpose )
                {
                    case Activity.IndividualOther:
                        t.Purpose = Activity.JointOther;
                        break;

                    case Activity.Market:
                        t.Purpose = Activity.JointMarket;
                        break;
                }
            }
        }
    }
}