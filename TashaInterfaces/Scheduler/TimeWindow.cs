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
using XTMF;

namespace Tasha.Scheduler
{
    /// <summary>
    /// Represents a window in time
    /// </summary>
    public struct TimeWindow
    {
        /// <summary>
        /// Creates a new time window
        /// </summary>
        /// <param name="start">The start of this window</param>
        /// <param name="end">The end of this window</param>
        public TimeWindow(Time start, Time end)
            : this()
        {
            this.StartTime = start;
            this.EndTime = end;
        }

        public Time Duration
        {
            get
            {
                return EndTime - StartTime;
            }
        }

        /// <summary>
        /// When the time window ends
        /// </summary>
        public Time EndTime
        {
            get;
            set;
        }

        /// <summary>
        /// When the time window starts
        /// </summary>
        public Time StartTime
        {
            get;
            set;
        }
    }
}