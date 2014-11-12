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
    /// Basic storage holder for simple auxiliary trip data. This class is to be used
    /// to store trips that use vehicles that were not created by the scheduler.
    ///
    /// (ie facilitate passenger trips)
    /// </summary>
    public class AuxiliaryTrip : Attachable, ITrip
    {
        /// <summary>
        ///
        /// </summary>
        private static ConcurrentBag<AuxiliaryTrip> Trips = new ConcurrentBag<AuxiliaryTrip>();

        public char cPurpose
        {
            get { return (char)this.Purpose; }
        }

        /// <summary>
        ///
        /// </summary>
        public IZone DestinationZone
        {
            get;
            internal set;
        }

        /// <summary>
        ///
        /// </summary>
        public IZone IntermediateZone
        {
            get;
            set;
        }

        /// <summary>
        ///
        /// </summary>
        public ITashaMode Mode
        {
            get;
            set;
        }

        public ITashaMode[] ModesChosen
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        ///
        /// </summary>
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
        ///
        /// </summary>
        public Activity Purpose
        {
            get;
            set;
        }

        /// <summary>
        ///
        /// </summary>
        public ITashaPerson SharedModeDriver { get; set; }

        /// <summary>
        ///
        /// </summary>
        public ITripChain TripChain
        {
            get;
            set;
        }

        /// <summary>
        /// Create a temporary auxiliary trip with minimum information
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="destination"></param>
        /// <param name="modeChoice"></param>
        /// <param name="startTime"></param>
        public static AuxiliaryTrip MakeAuxiliaryTrip(IZone origin, IZone destination, ITashaMode modeChoice, Time startTime)
        {
            AuxiliaryTrip aux = null;
            //if ( !AuxiliaryTrip.Trips.TryTake( out aux ) )
            //{
            aux = new AuxiliaryTrip();
            aux.Passengers = new List<ITashaPerson>();
            //}
            aux.OriginalZone = origin;
            aux.DestinationZone = destination;
            aux.Mode = modeChoice;
            aux.ActivityStartTime = startTime;
            aux.fActivityStartTime = startTime.ToFloat();
            return aux;
        }

        public ITrip Clone()
        {
            throw new NotImplementedException();
        }

        public void Recycle()
        {
            //this.ActivityStartTime = Time.Zero;
            //this.OriginalZone = null;
            //this.DestinationZone = null;
            //this.Mode = null;
            //this.ActivityStartTime = Time.Zero;
            //this.Passengers.Clear();
            //this.TripChain = null;
            //AuxiliaryTrip.Trips.Add( this );
        }

        #region ITrip Members

        public int TripNumber
        {
            get { return this.TripChain.Trips.IndexOf( this ); }
        }

        #endregion ITrip Members

        #region ITrip Members

        public Time ActivityStartTime
        {
            get;
            internal set;
        }

        public float fActivityStartTime
        {
            get;
            internal set;
        }

        public Time TravelTime
        {
            get { return ActivityStartTime - TripStartTime; }
        }

        public Time TripStartTime
        {
            get
            {
                if ( this.Mode != null )
                {
                    return this.ActivityStartTime - this.Mode.TravelTime( this.OriginalZone, this.DestinationZone, this.ActivityStartTime );
                }
                else
                {
                    return this.ActivityStartTime;
                }
            }
        }

        #endregion ITrip Members

        public void ReleaseTrip()
        {
            this.Release();
            this.Mode = null;
        }
    }
}