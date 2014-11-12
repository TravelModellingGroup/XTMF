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
using XTMF;

namespace Tasha.Common
{
    /// <summary>
    ///
    /// </summary>
    public class AuxiliaryTripChain : Attachable, ITripChain
    {
        #region ITripChain Members

        /// <summary>
        ///
        /// </summary>
        private List<ITrip> trips;

        /// <summary>
        ///
        /// </summary>
        public AuxiliaryTripChain()
        {
            trips = new List<ITrip>( 2 );
        }

        /// <summary>
        ///
        /// </summary>
        public Time EndTime
        {
            get
            {
                return this.Trips[this.Trips.Count - 1].ActivityStartTime;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public ITripChain GetRepTripChain
        {
            get { return null; }
        }

        /// <summary>
        ///
        /// </summary>
        public bool JointTrip
        {
            get { return false; }
        }

        /// <summary>
        ///
        /// </summary>
        public List<ITripChain> JointTripChains
        {
            get { return null; }
        }

        /// <summary>
        ///
        /// </summary>
        public int JointTripID
        {
            get { return -1; }
        }

        /// <summary>
        ///
        /// </summary>
        public bool JointTripRep
        {
            get { return false; }
        }

        /// <summary>
        ///
        /// </summary>
        public ITashaPerson Person
        {
            get;
            set;
        }

        /// <summary>
        ///
        /// </summary>
        public Time StartTime
        {
            get
            {
                return this.Trips[0].TripStartTime;
            }
        }

        /// <summary>
        ///
        /// </summary>
        public bool TripChainRequiresPV
        {
            get { return false; }
        }

        /// <summary>
        ///
        /// </summary>
        public List<ITrip> Trips
        {
            get
            {
                return trips;
            }

            set
            {
                trips = value;
            }
        }

        #endregion ITripChain Members

        #region ITripChain Members

        public List<ITashaPerson> passengers
        {
            get
            {
                List<ITashaPerson> pass = new List<ITashaPerson>();
                foreach ( var trip in trips )
                {
                    pass.AddRange( trip.Passengers );
                }
                return pass;
            }
        }

        #endregion ITripChain Members

        public void Recycle()
        {
            this.Release();
            foreach ( var t in this.Trips )
            {
                t.Recycle();
            }
            this.Trips.Clear();
        }

        #region ITripChain Members

        public List<IVehicleType> requiresVehicle
        {
            get
            {
                List<IVehicleType> v = new List<IVehicleType>();

                foreach ( var trip in Trips )
                {
                    if ( trip.Mode.RequiresVehicle != null )
                    {
                        if ( !v.Contains( trip.Mode.RequiresVehicle ) )
                        {
                            v.Add( trip.Mode.RequiresVehicle );
                        }
                    }
                }

                return v;
            }
        }

        #endregion ITripChain Members

        #region ITripChain Members

        public ITripChain Clone()
        {
            throw new NotImplementedException();
        }

        #endregion ITripChain Members

        #region ITripChain Members

        public ITripChain DeepClone()
        {
            throw new NotImplementedException();
        }

        #endregion ITripChain Members
    }
}