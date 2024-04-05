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
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Modes
{
    internal static class Common
    {
        private static Time Nine = new Time( "9:00 AM" );

        private static Time Six = new Time( "6:00 AM" );
        private static Time SixThirty = new Time( "6:30 PM" );
        private static Time ThreeThirty = new Time( "3:30 PM" );

        public static float ConvertToHours(float minutes)
        {
            int hours = (int)( minutes / 60 );
            float minutes2 = ( minutes - hours * 60 ) / 100;
            return hours + minutes2;
        }

        public static List<int> ConvertToIntList(string values)
        {
            List<int> toReturn = [];

            string[] items = values.Split( ',' );

            foreach ( var item in items )
            {
                int dashIndex;
                if ( ( dashIndex = item.IndexOf( '-' ) ) != -1 )
                {
                    int startZone = Convert.ToInt32( item.Substring( 0, dashIndex ) );
                    int endZone = Convert.ToInt32( item.Substring( dashIndex + 1, item.Length - ( dashIndex + 1 ) ) );

                    for ( int i = startZone; i < endZone; i++ )
                        toReturn.Add( i );
                }
                else
                    toReturn.Add( Convert.ToInt32( item ) );
            }

            return toReturn;
        }

        /// <summary>
        /// Calculate the distance to another zone
        /// </summary>
        /// <param name="origin">Where we are</param>
        /// <param name="destination">Where you want to go</param>
        /// <returns>The distance between the two</returns>
        public static double Distance(this IZone origin, IZone destination)
        {
            // Since most paths are square [non triangular, we use this formula instead of taking the square root]
            return Math.Abs( origin.X - destination.X ) + Math.Abs( origin.Y - destination.Y );
        }

        /// <summary>
        /// Gets the time period for travel time
        /// </summary>
        /// <param name="time">The time the trip starts at</param>
        /// <returns>The time period</returns>
        public static TravelTimePeriod GetTimePeriod(Time time)
        {
            if ( time >= Six & time < Nine )
            {
                return TravelTimePeriod.Morning;
            }
            if ( time >= ThreeThirty & time < SixThirty )
            {
                return TravelTimePeriod.Afternoon;
            }
            return TravelTimePeriod.Offpeak;
        }
    }
}