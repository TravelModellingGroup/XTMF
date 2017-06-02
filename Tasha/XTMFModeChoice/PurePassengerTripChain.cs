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

using System.Collections.Generic;
using Tasha.Common;
using XTMF;

namespace Tasha.XTMFModeChoice
{
    internal class PurePassengerTripChain : Attachable, ITripChain
    {
        public PurePassengerTripChain()
        {
            Trips = new List<ITrip>( 1 );
        }

        public Time EndTime
        {
            get
            {
                return Trips[0].ActivityStartTime;
            }
        }

        public ITripChain GetRepTripChain
        {
            get { return null; }
        }

        public bool JointTrip
        {
            get { return false; }
        }

        public List<ITripChain> JointTripChains
        {
            get { return null; }
        }

        public int JointTripID
        {
            get { return -1; }
        }

        public bool JointTripRep
        {
            get { return false; }
        }

        public List<ITashaPerson> Passengers
        {
            get { return null; }
        }

        public ITashaPerson Person
        {
            get;
            set;
        }

        public List<IVehicleType> RequiresVehicle
        {
            get { return null; }
        }

        public Time StartTime
        {
            get
            {
                return Trips[0].TripStartTime;
            }
        }

        public bool TripChainRequiresPV
        {
            get { return false; }
        }

        public List<ITrip> Trips { get; set; }

        public ITripChain Clone()
        {
            return null;
        }

        public ITripChain DeepClone()
        {
            return null;
        }

        public void Recycle()
        {
            
        }
    }
}