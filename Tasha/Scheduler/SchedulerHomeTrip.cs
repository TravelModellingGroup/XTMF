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
        private static ConcurrentBag<SchedulerHomeTrip> Trips = [];

        #region ITrip Members

        private SchedulerHomeTrip(int householdIterations)
        {
            Mode = null;
            ModesChosen = new ITashaMode[householdIterations];
        }

        /// <summary>
        /// This is used to help us cache when an activity start time should happen.
        /// </summary>
        private bool RecalculateActivityStartTime = true;

        private Time _ActivityStartTime;
        /// <summary>
        /// What time does this trip start at?
        /// </summary>
        public Time ActivityStartTime
        {
            get
            {
                if (Mode == null)
                {
                    return TripStartTime;
                }
                if (RecalculateActivityStartTime)
                {
                    _ActivityStartTime = TripStartTime + Mode.TravelTime(OriginalZone, DestinationZone, TripStartTime);
                    RecalculateActivityStartTime = false;
                }
                return _ActivityStartTime;
            }
        }

        public IZone DestinationZone
        {
            get;
            internal set;
        }

        public IZone IntermediateZone
        {
            get;
            set;
        }

        private ITashaMode _Mode;
        public ITashaMode Mode
        {
            get
            {
                return _Mode;
            }
            set
            {
                _Mode = value;
                RecalculateActivityStartTime = true;
            }
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

        private Time _TripStartTime;
        public Time TripStartTime
        {
            get
            {
                return _TripStartTime;
            }
            set
            {
                _TripStartTime = value;
                RecalculateActivityStartTime = true;
            }
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
            TripStartTime = Time.Zero;
            TripNumber = -1;
            Array.Clear(ModesChosen, 0, ModesChosen.Length);
            RecalculateActivityStartTime = true;
            if (Trips.Count < 100)
            {
                Trips.Add(this);
            }
        }

        internal static SchedulerHomeTrip GetTrip(int householdIterations)
        {
            if (!Trips.TryTake(out SchedulerHomeTrip ret))
            {
                return new SchedulerHomeTrip(householdIterations);
            }
            return ret;
        }

        #endregion ITrip Members
    }
}