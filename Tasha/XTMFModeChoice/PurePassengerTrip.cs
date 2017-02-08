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

namespace Tasha.XTMFModeChoice
{
    public class PurePassengerTrip : Attachable, ITrip
    {
        public Time ActivityStartTime
        {
            get;
            set;
        }

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

        public ITashaMode[] ModesChosen
        {
            get
            {
                return null;
            }
        }

        public IZone OriginalZone
        {
            get;
            set;
        }

        public List<ITashaPerson> Passengers
        {
            get;
            set;
        }

        public Activity Purpose
        {
            get;
            set;
        }

        public ITashaPerson SharedModeDriver
        {
            get;
            set;
        }

        public Time TravelTime
        {
            get;
            set;
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
            set;
        }

        public static PurePassengerTrip MakeDriverTrip(IZone HomeZone, ITashaMode mode, Time StartTime, Time EndTime)
        {
            PurePassengerTrip DriverTrip = new PurePassengerTrip();
            DriverTrip.Purpose = Activity.FacilitatePassenger;
            DriverTrip.OriginalZone = DriverTrip.DestinationZone = HomeZone;
            DriverTrip.Mode = mode;
            DriverTrip.ActivityStartTime = EndTime;
            DriverTrip.TripStartTime = StartTime;
            return DriverTrip;
        }

        public ITrip Clone()
        {
            throw new NotImplementedException();
        }

        public void Recycle()
        {
            
        }
    }
}