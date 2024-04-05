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
using XTMF;

namespace TMG.Tasha.MicrosimLoader;

internal sealed class ActivityPurposeTrip : Attachable, ITrip
{
    private static ConcurrentQueue<ActivityPurposeTrip> Trips = new();

    #region ITrip Members

    private ActivityPurposeTrip(int householdIterations)
    {
        Mode = null;
        ModesChosen = new ITashaMode[householdIterations];
    }

    private Time _ActivityStartTime;
    /// <summary>
    /// What time does this trip start at?
    /// </summary>
    public Time ActivityStartTime
    {
        get
        {
            return _ActivityStartTime;
        }
        internal set
        {
            _ActivityStartTime = value;
            RecalculateTripStartTime = true;
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
            RecalculateTripStartTime = true;
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

    private bool RecalculateTripStartTime = true;
    private Time _TripStartTime;
    public Time TripStartTime
    {
        get
        {
            if (RecalculateTripStartTime)
            {
                if (Mode != null)
                {
                    _TripStartTime = ActivityStartTime - Mode.TravelTime(OriginalZone, DestinationZone, ActivityStartTime);
                }
                else
                {
                    _TripStartTime = ActivityStartTime;
                }
                RecalculateTripStartTime = false;
            }
            return _TripStartTime;
        }
    }

    public ITrip Clone()
    {
        return (ITrip)MemberwiseClone();
    }

    public void Recycle()
    {
        if (Trips.Count < 100)
        {
            Release();
            Mode = null;
            TripChain = null;
            OriginalZone = null;
            DestinationZone = null;
            ActivityStartTime = Time.Zero;
            TripNumber = -1;
            Array.Clear(ModesChosen, 0, ModesChosen.Length);
            RecalculateTripStartTime = true;
            Trips.Enqueue(this);
        }
    }

    internal static ActivityPurposeTrip GetTrip(int householdIterations)
    {
        if (!Trips.TryDequeue(out ActivityPurposeTrip ret))
        {
            return new ActivityPurposeTrip(householdIterations);
        }
        return ret;
    }

    #endregion ITrip Members
}