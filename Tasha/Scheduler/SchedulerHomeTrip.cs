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
using System.Collections.Concurrent;
using System.Collections.Generic;
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Scheduler
{
    internal sealed class SchedulerHomeTrip : Attachable, ITrip
    {
        private static ConcurrentBag<SchedulerHomeTrip> Trips = new ConcurrentBag<SchedulerHomeTrip>();

        #region ITrip Members

        internal IMode ObservedMode;

        private SchedulerHomeTrip(int householdIterations)
        {
            Mode = null;
            ModesChosen = new ITashaMode[householdIterations];
        }

        private SchedulerHomeTrip(TravelEpisode episode)
        {
            TripStartTime = episode.EndTime;
            DestinationZone = episode.Destination;
            IntermediateZone = null;
            Mode = null;
            OriginalZone = episode.Origin;
            if ( episode.People == null )
            {
                Passengers = null;
            }
            else
            {
                Passengers = new List<ITashaPerson>( episode.People );
            }
            Purpose = episode.ActivityType;
        }

        /// <summary>
        /// What time does this trip start at?
        /// </summary>
        public Time ActivityStartTime
        {
            get
            {
                if ( Mode == null )
                {
                    return TripStartTime;
                }
                return TripStartTime + Mode.TravelTime( OriginalZone, DestinationZone, TripStartTime );
            }
        }

        public char cPurpose
        {
            get;
            internal set;
        }

        public IZone DestinationZone
        {
            get;
            internal set;
        }

        public float fActivityStartTime
        {
            get;
            internal set;
        }

        public IZone IntermediateZone
        {
            get;
            set;
        }

        public ITashaMode Mode
        {
            get;
            set;
        }

        public ITashaMode[] ModesChosen
        {
            get;
            internal set;
        }

        public IZone OriginalZone
        {
            get;
            internal set;
        }

        public List<ITashaPerson> Passengers
        {
            get;
            set;
        }

        /// <summary>
        /// TODO: Relate this to cPurpose
        /// </summary>
        public Activity Purpose
        {
            get;
            set;
        }

        public ITashaPerson SharedModeDriver { get; set; }

        public Time TravelTime
        {
            get { return ActivityStartTime - TripStartTime; }
        }

        public ITripChain TripChain
        {
            get;
            set;
        }

        public int TripNumber
        {
            get;
            set;
        }

        public Time TripStartTime
        {
            get;
            internal set;
        }

        public ITrip Clone()
        {
            return (ITrip)MemberwiseClone();
        }

        public void Recycle()
        {
            Release();
            Mode = null;
            TripChain = null;
            OriginalZone = null;
            DestinationZone = null;
            fActivityStartTime = 0;
            TripStartTime = Time.Zero;
            TripNumber = -1;
            Array.Clear( ModesChosen, 0, ModesChosen.Length );
            if(Trips.Count < 100)
            {
                Trips.Add(this);
            }
        }

        public void SetActivityStartTime(Time time)
        {
            TripStartTime = time;
        }

        public void SetDestinationZone(IZone zone)
        {
            DestinationZone = zone;
        }

        public void SetOriginZone(IZone zone)
        {
            OriginalZone = zone;
        }

        internal static SchedulerHomeTrip GetTrip(TravelEpisode episode)
        {
            SchedulerHomeTrip ret;
            if ( !Trips.TryTake( out ret ) )
            {
                return new SchedulerHomeTrip( episode );
            }
            return ret;
        }

        internal static SchedulerHomeTrip GetTrip(int householdIterations)
        {
            SchedulerHomeTrip ret;
            if ( !Trips.TryTake( out ret ) )
            {
                return new SchedulerHomeTrip( householdIterations );
            }
            return ret;
        }

        #endregion ITrip Members
    }
}