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
using Tasha.Common;
using TMG;

namespace Tasha.Scheduler
{
    internal sealed class TravelEpisode : Episode
    {
        internal TravelEpisode(int id, TimeWindow timeWindow, Episode from, Episode to, ITashaPerson owner)
            : base( timeWindow, owner )
        {
            //TODO: verify this line:
            ActivityType = to.ActivityType;
            //-----
            From = from;
            To = to;
        }

        public override int Adults
        {
            get { throw new NotImplementedException(); }
        }

        public override IZone Zone
        {
            get;
            internal set;
        }

        internal IZone Destination
        {
            get
            {
                return To.Zone;
            }
        }

        internal Episode From
        {
            get;
            set;
        }

        internal IZone Origin
        {
            get
            {
                return From.Zone;
            }
        }

        internal Episode To
        {
            get;
            set;
        }

        public override bool IsPersonIncluded(ITashaPerson person)
        {
            if ( From != null )
            {
                return From.IsOwner( person );
            }
            else if ( To != null )
            {
                return To.IsOwner( person );
            }
            return false;
        }

        internal override void AddPerson(ITashaPerson person)
        {
            throw new NotImplementedException();
        }
    }
}