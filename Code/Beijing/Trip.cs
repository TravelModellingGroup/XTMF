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
using System.Linq;
using System.Text;
using XTMF;
using Datastructure;
using TMG;
using Tasha;
using Tasha.Common;
namespace Beijing
{
    public sealed class Trip : Attachable, ITrip
    {
        public Time ActivityStartTime { get; set; }


        public Time TripStartTime { get; set; }
        

        public IZone DestinationZone{ get; set; }
        

        public ITashaMode Mode { get; set; }


        public ITashaMode[] ModesChosen { get; set; }

        public ITashaPerson SharedModeDriver { get; set; }

        public IZone OriginalZone { get; set; }

        public Activity Purpose
        {
            get;
            set;
        }


        public int TripNumber { get; set; }


        public ITripChain TripChain { get; set; }


        public IZone IntermediateZone { get; set; }


        public Time TravelTime { get; set; }


        public List<ITashaPerson> Passengers { get; set; }

        public Trip(int householdIterations)
        {
            ModesChosen = new ITashaMode[householdIterations];
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
