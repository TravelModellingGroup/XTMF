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

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using XTMF;

namespace Tasha.Common
{
    public class TripChain : Attachable, ITripChain
    {
        public static ITashaRuntime TashaRuntime;
        public static int TripChainNumber;
        public static int TripDestinationPlanningDistrct;
        public static int TripDestinationZone;
        public static bool TripHeader;
        public static int TripHouseholdID;
        public static int TripJointTourID;
        public static int TripJointTourRep;
        public static int TripNumber;
        public static int TripObservedMode;
        public static int TripOriginZone;
        public static int TripPersonID;
        public static int TripPlanningDistrictOrigin;
        public static int TripPurposeDestination;
        public static int TripPurposeOrigin;
        public static int TripStartTime;
        private static ConcurrentQueue<TripChain> TripChains = new ConcurrentQueue<TripChain>();

        /// <summary>
        /// The person that this trip chain belongs to
        /// </summary>

        /// <summary>
        ///
        /// </summary>
        /// <param name="person"></param>
        public TripChain(ITashaPerson person)
        {
            Person = person;
            Trips = new List<ITrip>(3 );
        }

        /// <summary>
        /// The End Time of this Trip Chain (The time returned home)
        /// </summary>
        public Time EndTime
        {
            get
            {
                return Trips[Trips.Count - 1].ActivityStartTime;
            }
        }

        public ITripChain GetRepTripChain
        {
            get;
            internal set;
        }

        /// <summary>
        /// Is this a joint trip?
        /// </summary>
        public bool JointTrip
        {
            get
            {
                return JointTripID != 0;
            }
        }

        public List<ITripChain> JointTripChains
        {
            get
            {
                if (!JointTrip) return null;

                List<ITripChain> linkedTripChains = [];
                foreach (var p in Person.Household.Persons)
                {
                    foreach (var tripChain in p.TripChains)
                    {
                        if (tripChain.JointTripID == JointTripID)
                            linkedTripChains.Add(tripChain);
                    }
                }
                return linkedTripChains;
            }
        }

        /// <summary>
        /// What is the ID of this joint trip?
        /// </summary>
        public int JointTripID
        {
            get;
            set;
        }

        /// <summary>
        /// Is the owned the Representative for the joint trip?
        /// </summary>
        public bool JointTripRep
        {
            get;
            set;
        }

        public ITashaPerson Person
        {
            get;
            set;
        }

        /// <summary>
        /// The Start Time of this TripChain
        /// </summary>
        public Time StartTime
        {
            get
            {
                return Trips[0].TripStartTime;
            }
        }

        public bool TripChainRequiresPV
        {
            get
            {
                foreach (var t in Trips)
                {
                    if (!t.Mode.NonPersonalVehicle)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// The trips in this trip chain
        /// </summary>
        public List<ITrip> Trips
        {
            get;
            set;
        }

        public static IEnumerable<ITrip> GetTrips(ITashaHousehold household)
        {
            foreach (var person in household.Persons)
            {
                foreach (var chain in person.TripChains)
                {
                    foreach (var trip in chain.Trips)
                    {
                        yield return trip;
                    }
                }
            }
        }

        public static TripChain MakeChain(ITashaPerson iPerson)
        {
            if (!TripChains.TryDequeue(out TripChain c))
            {
                return new TripChain(iPerson);
            }
            c.Person = iPerson;
            return c;
        }

        public static void Save(string fileName, IEnumerable<ITashaHousehold> households)
        {
            using StreamWriter writer = new StreamWriter(fileName);
            //write header
            if (TripHeader)
                writer.WriteLine("hhld_num,pers_num,trip_num,start_time,mode_prime,purp_orig,pd_orig,gta96_orig,gta01_orig,purp_dest,pd_dest,gta96_dest,gta01_dest,trip_km,jointTourID,jointTourRep");

            foreach (var household in households)
            {
                foreach (var trip in GetTrips(household))
                {
                    int jointTourLeader = 0;
                    int personNum = 1;
                    foreach (var p in household.Persons)
                    {
                        if ((p.TripChains.FindLast((chain) => (chain.JointTripID == trip.TripChain.JointTripID && chain.JointTripRep))) != null)
                        {
                            jointTourLeader = personNum;
                            break;
                        }
                        personNum++;
                    }

                    ActivityConverter.Converter.GetTripActivities(trip, trip.TripChain, out char purposeOrigin, out char purposeDestination);

                    string[] attributes = new string[22];

                    for (int i = 0; i < attributes.Length; i++)
                    {
                        attributes[i] = "";
                    }

                    attributes[TripHouseholdID] = household.HouseholdId.ToString();
                    attributes[TripPersonID] = trip.TripChain.Person.Id.ToString();
                    attributes[TripNumber] = trip.TripNumber.ToString();
                    attributes[TripStartTime] = trip.ActivityStartTime.ToString();
                    attributes[TripObservedMode] = ((char)trip["ObservedMode"]).ToString();
                    attributes[TripPurposeOrigin] = purposeOrigin.ToString();
                    attributes[TripOriginZone] = trip.OriginalZone.ZoneNumber.ToString();
                    attributes[TripPurposeDestination] = purposeDestination.ToString();
                    attributes[TripDestinationZone] = trip.DestinationZone.ZoneNumber.ToString();
                    attributes[TripJointTourID] = trip.TripChain.JointTripID.ToString();
                    attributes[TripJointTourRep] = jointTourLeader.ToString();
                    string line = string.Empty;
                    for (int i = 0; i < attributes.Length; i++)
                    {
                        line += attributes[i] + ",";
                    }
                    writer.WriteLine(line);
                }
            }
        }

        public void Recycle()
        {
            Release();
            var trips = Trips;
            for (int i = 0; i < trips.Count; i++)
            {
                trips[i].Recycle();
            }
            Trips.Clear();
            if (Passengers != null )
            {
                Passengers.Clear();
            }
            GetRepTripChain = null;
            JointTripRep = false;
            JointTripID = 0;
            if (JointTripChains != null)
            {
                JointTripChains.Clear();
            }
            GetRepTripChain = null;
            TripChains.Enqueue(this);
        }

        internal static void ReleaseChainPool()
        {
            TripChains = new ConcurrentQueue<TripChain>();
        }

        #region ITripChain Members

        public List<ITashaPerson> Passengers
        {
            get { return null; }
        }

        #endregion ITripChain Members

        #region ITripChain Members

        public List<IVehicleType> RequiresVehicle
        {
            get
            {
                List<IVehicleType> v = [];

                foreach (var trip in Trips)
                {
                    if (trip.Mode.RequiresVehicle != null )
                    {
                        if (!v.Contains(trip.Mode.RequiresVehicle))
                        {
                            v.Add(trip.Mode.RequiresVehicle);
                        }
                    }
                }

                return v;
            }
        }

        #endregion ITripChain Members

        #region ITripChain Members

        /// <summary>
        /// Shallow clone of this trip chain (does not clone trips)
        /// </summary>
        /// <returns></returns>
        public ITripChain Clone()
        {
            ITripChain chain = (ITripChain)MemberwiseClone();
            chain.Trips = [.. Trips];
            return chain;
        }

        /// <summary>
        /// Clones this trip chain and its trips
        /// </summary>
        /// <returns></returns>
        public ITripChain DeepClone()
        {
            //shallow clone of tripchain
            ITripChain chain = (ITripChain)MemberwiseClone();
            chain.Trips = [];
            List<ITrip> trips = [];

            //cloning trips as well and setting their trip chain to cloned chained
            foreach (var trip in Trips)
            {
                ITrip t = trip.Clone();
                t.TripChain = chain;
                trips.Add(t);
            }

            chain.Trips.AddRange(trips);

            return chain;
        }

        #endregion ITripChain Members
    }// end class
}// end namespace