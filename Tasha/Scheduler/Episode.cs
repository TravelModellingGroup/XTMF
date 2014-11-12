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

namespace Tasha.Scheduler
{
    /// <summary>
    /// Base class for a scheduled event.
    /// </summary>
    public abstract class Episode : IEpisode
    {
        internal static long GeneratedEpisodes = 0;

        /// <summary>
        /// Creates a new Episode
        /// </summary>
        /// <param name="id">The id for the event</param>
        /// <param name="window">The window in time this episode occurs in</param>
        internal Episode(TimeWindow window, ITashaPerson owner)
        {
            this.Window = window;
            // originally we have no travel time
            this.TravelTime = Time.Zero;
        }

        /// <summary>
        /// The type of activity this episode is about
        /// </summary>
        public Activity ActivityType { get; internal set; }

        /// <summary>
        /// The number of Adults that are in this Episode
        /// </summary>
        public abstract int Adults { get; }

        public ISchedule ContainingSchedule
        {
            get;set;
        }


        /// <summary>
        /// How long the episode takes.  Changes to this will affect the end time.
        /// </summary>
        public Time Duration
        {
            get
            {
                return EndTime - StartTime;
            }

            internal set
            {
                this.EndTime = this.StartTime + value;
            }
        }

        /// <summary>
        /// The time the episode ends at
        /// </summary>
        public Time EndTime { get; internal set; }

        /// <summary>
        /// The original duration set for this episode
        /// This is used for calculating the bounds of how far we can try to shift this episode
        /// </summary>
        public Time OriginalDuration { get; internal set; }

        /// <summary>
        /// The person who is the owner of the episode
        /// </summary>
        public ITashaPerson Owner { get; internal set; }

        /// <summary>
        /// The person who this episode is for
        /// </summary>
        public List<ITashaPerson> People
        {
            get;
            internal set;
        }

        /// <summary>
        /// The time the episode starts at
        /// </summary>
        public Time StartTime { get; internal set; }

        /// <summary>
        /// The amount of time to get to the next activity
        /// </summary>
        public Time TravelTime { get; internal set; }

        /// <summary>
        /// The zone that the episode is at
        /// </summary>
        public abstract IZone Zone { get; internal set; }

        /// <summary>
        /// When this episode occurs
        /// </summary>
        internal TimeWindow Window
        {
            get
            {
                return new TimeWindow( this.StartTime, this.EndTime );
            }

            set
            {
                this.StartTime = value.StartTime;
                this.EndTime = value.EndTime;
            }
        }

        /// <summary>
        /// Checks to see if a person is the owner of an episode
        /// </summary>
        public bool IsOwner(ITashaPerson person)
        {
            return person == this.Owner;
        }

        /// <summary>
        /// Checks to see if this person is on the trip
        /// </summary>
        /// <param name="person">The person we are testing for</param>
        /// <returns>True, if the person is found</returns>
        public abstract bool IsPersonIncluded(ITashaPerson person);

        /// <summary>
        /// Includes a person in the episode
        /// </summary>
        /// <param name="person">The person to include</param>
        internal abstract void AddPerson(ITashaPerson person);

        internal bool CheckDuration(Time testDuration)
        {
            if ( ( this.ActivityType == Activity.PrimaryWork ) | ( this.ActivityType == Activity.WorkBasedBusiness ) )
            {
                // Check to see if the test duration is the same as the one we are in
                return this.OriginalDuration == testDuration;
            }
            else
            {
                return Scheduler.PercentOverlapAllowed * Distribution.TashaTimeToDistribution( this.OriginalDuration )
                    <= Distribution.TashaTimeToDistribution( testDuration );
            }
        }
    }
}