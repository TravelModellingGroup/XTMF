using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tasha.Common;
using XTMF;

namespace TMG.Tasha.MicrosimLoader
{

    internal sealed class HouseholdPurposeTrip : Attachable, ITrip
    {
        private static ConcurrentBag<HouseholdPurposeTrip> Trips = new ConcurrentBag<HouseholdPurposeTrip>();

        #region ITrip Members

        private HouseholdPurposeTrip(int householdIterations)
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

        internal static HouseholdPurposeTrip GetTrip(int householdIterations)
        {
            if (!Trips.TryTake(out var ret))
            {
                return new HouseholdPurposeTrip(householdIterations);
            }
            return ret;
        }

        #endregion ITrip Members
    }
}
