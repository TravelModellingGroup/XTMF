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
using System.Linq;
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Modes
{
    internal static class Common
    {
        private static Time nine = new Time( "9:00 AM" );

        private static Time six = new Time( "6:00 AM" );
        private static Time sixThirty = new Time( "6:30 PM" );
        private static Time threeThirty = new Time( "3:30 PM" );

        public static float ConvertToHours(float minutes)
        {
            int hours = (int)( minutes / 60 );
            float minutes2 = ( minutes - hours * 60 ) / 100;
            return hours + minutes2;
        }

        public static List<int> ConvertToIntList(string values)
        {
            List<int> toReturn = new List<int>();

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

        public static T GetData<T>(IList<ITravelData> data)
        {
            Type type = data.GetType();
            T toReturn = default( T );
            foreach ( var d in data )
            {
                try
                {
                    toReturn = (T)d;
                }
                catch ( Exception )
                {
                }
            }
            return toReturn;
        }

        public static ITashaMode GetMode(IList<ITashaMode> modes, string name)
        {
            foreach ( var mode in modes )
            {
                if ( mode.Name == name )
                {
                    return mode;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the time period for travel time
        /// </summary>
        /// <param name="time">The time the trip starts at</param>
        /// <returns>The time period</returns>
        public static TravelTimePeriod GetTimePeriod(Time time)
        {
            if ( time >= six & time < nine )
            {
                return TravelTimePeriod.Morning;
            }
            else if ( time >= threeThirty & time < sixThirty )
            {
                return TravelTimePeriod.Afternoon;
            }
            return TravelTimePeriod.Offpeak;
        }

        /// <summary>
        /// Takes a list of V values and 'randomly' selects a value from list
        /// </summary>
        /// <param name="values">a list of values</param>
        /// <returns>an index of the list or -1 if it was not successful</returns>
        public static int RandChoiceCDF(double[] values, int seed, Random random)
        {
            double RandomNum = random.NextDouble();

            double FDenom = 0.0;

            //if the values in the array add up to 0 its no good
            if ( ( FDenom = values.Sum() ) == 0.0 )
            {
                return random.Next( values.Count() );
            }

            double FCDFsum = 0.0;

            FDenom = 1.0 / FDenom;   // Saves doing floating-point divisions

            for ( int i = 0; i < values.Count(); i++ )
            {
                if ( values[i] != 0.0 )
                {
                    FCDFsum += values[i] * FDenom;
                    if ( RandomNum < FCDFsum )
                        return i;
                }
            }

            return -1;
        }
    }
}