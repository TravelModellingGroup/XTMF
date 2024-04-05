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
    internal sealed class ActivityEpisode : Episode
    {
        /// <summary>
        /// The information of how we are going to travel from this episode
        /// </summary>
        internal TravelEpisode TravelToNext;

        internal ActivityEpisode(TimeWindow window, Activity type, ITashaPerson owner)
            : base( window )
        {
            People = null;
            ActivityType = type;
            OriginalDuration = window.Duration;
            Owner = owner;
        }

        public override int Adults
        {
            get
            {
                int total = 0;
                var numberOfPeople = People.Count;
                for ( int i = 0; i < numberOfPeople; i++ )
                {
                    if ( People[i].Adult )
                    {
                        total++;
                    }
                }
                return total;
            }
        }

        public override IZone Zone
        {
            get;
            internal set;
        }

        public override bool IsPersonIncluded(ITashaPerson person)
        {
            if ( People == null ) return false;
            return People.Contains( person );
        }

        public override string ToString()
        {
            return String.Format( "{0}->{1}", StartTime, EndTime );
        }

        internal override void AddPerson(ITashaPerson person)
        {
            // only allow people who are not included added to the episode
            if ( People == null )
            {
                People = [];
            }

            if ( !IsPersonIncluded( person ) )
            {
                People.Add( person );
            }
            else
            {
                throw new XTMFRuntimeException(null, "This person has already been included!" );
            }
        }
    }
}