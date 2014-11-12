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

namespace Tasha.Scheduler
{
    internal static class TimePeriod
    {
        [ThreadStatic]
        internal static TimePeriodSlice[] TimeSlices;

        internal static TimePeriodSlice GetTimePeriodSlice(Time time)
        {
            if ( TimeSlices == null )
            {
                LoadTimePeriodData();
            }
            int length = TimeSlices.Length;
            for ( int i = 0; i < length; i++ )
            {
                if ( time >= TimeSlices[i].start && time < TimeSlices[i].end )
                {
                    return TimeSlices[i];
                }
            }

            //should not get here...
            return TimeSlices[0];
        }

        internal static void LoadTimePeriodData()
        {
            TimeSlices = new TimePeriodSlice[5];

            //start of day to 5:59
            TimeSlices[0] = new TimePeriodSlice( 0,
                Time.StartOfDay,
                new Time( "6:00:00" ) );

            //6:00 to 8:59
            TimeSlices[1] = new TimePeriodSlice( 1, new Time( "6:00:00" ),
                new Time( "9:00:00" ) );

            //9:00 to 14:59
            TimeSlices[2] = new TimePeriodSlice( 2,
                new Time( "9:00:00" ), new Time( "15:00:00" ) );

            //15:00 to 18:59
            TimeSlices[3] = new TimePeriodSlice( 3,
                new Time( "15:00:00" ), new Time( "19:00:00" ) );

            //19:00 to end of day
            TimeSlices[4] = new TimePeriodSlice( 0,
                new Time( "19:00:00" ), Distribution.DistributionToTimeOfDay( Scheduler.StartTimeQuanta ) );
        }
    }

    internal struct TimePeriodSlice
    {
        internal Time end;
        internal byte id;
        internal Time start;

        public TimePeriodSlice(byte pid, Time pstart,
            Time pend)
        {
            id = pid;
            start = pstart;
            end = pend;
        }

        public override string ToString()
        {
            return string.Format( "{0}->{1}", this.start, this.end );
        }
    }
}