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
using TMG;
using XTMF;

namespace Tasha.Common
{
    /// <summary>
    /// This class is designed to represent abstract single trip
    /// </summary>

    public class Trip : Attachable, ITrip
    {
        #region ITrip Members

        public IZone DestinationZone
        {
            get;
            set;
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

        public IZone OriginalZone
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

        public ITripChain TripChain
        {
            get;
            set;
        }

        #endregion ITrip Members

        private static ConcurrentQueue<Trip> Trips = new();

        public Trip(ITripChain chain, IZone origin, IZone destination, Activity purpose, Time startTime, int householdIterations)
        {
            TripChain = chain;
            TripStartTime = startTime;
            OriginalZone = origin;
            DestinationZone = destination;
            Purpose = purpose;
            IntermediateZone = null;
            Passengers = [];
            ModesChosen = new ITashaMode[householdIterations];
        }

        private Trip(int householdIterations)
        {
            ModesChosen = new ITashaMode[householdIterations];
        }

        /// <summary>
        /// What time does this trip start at?
        /// </summary>
        public Time ActivityStartTime
        {
            get
            {
                return Mode == null ? TripStartTime : TripStartTime + Mode.TravelTime( OriginalZone, DestinationZone, TripStartTime );
            }
        }

        public ITashaMode[] ModesChosen
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets a trip off of the stack
        /// </summary>
        /// <returns>A Trip to use</returns>
        public static Trip GetTrip(int householdIterations)
        {
            if (!Trips.TryDequeue(out Trip t))
            {
                return new Trip(householdIterations);
            }
            return t;
        }

        public ITrip Clone()
        {
            return (ITrip)MemberwiseClone();
        }

        public void Recycle()
        {
            Release();
            Array.Clear( ModesChosen, 0, ModesChosen.Length );
            Mode = null;
            TripChain = null;
            Trips.Enqueue( this );
        }

        internal static void ReleaseTripPool()
        {
            Trips = new ConcurrentQueue<Trip>();
        }

        #region ITrip Members

        public int TripNumber
        {
            get;
            set;
        }

        #endregion ITrip Members

        #region ITrip Members

        public Time TripStartTime
        {
            get;
            set;
        }

        #endregion ITrip Members

        #region ITrip Members

        public Time TravelTime
        {
            get;
            set;
        }

        #endregion ITrip Members

        #region ITrip Members

        public List<ITashaPerson> Passengers
        {
            get;
            set;
        }

        #endregion ITrip Members
    }
}