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
using System.Threading;
using TMG;

namespace Tasha.Common
{
    /// <summary>
    /// This represents one household in the simulation
    /// </summary>
    public sealed class Household : Attachable, ITashaHousehold
    {
        public static int HouseholdsMade;
        private static ConcurrentBag<Household> Households = new ConcurrentBag<Household>();

        private int _NumberOfAdults = -1;

        /// <summary>
        /// Households can only be created by loading them from file
        /// </summary>
        public Household()
        {
        }

        public Household(int id, ITashaPerson[] persons, IVehicle[] vehicles, float expansion, IZone zone)
        {
            //this.auxiliaryTripChains = new List<ITripChain>(7);
            HouseholdId = id;
            Persons = persons;
            Vehicles = vehicles;
            ExpansionFactor = expansion;
            HomeZone = zone;
        }

        public DwellingType DwellingType { get; set; }

        /// <summary>
        /// How many households this represents
        /// </summary>
        public float ExpansionFactor
        {
            get;
            set;
        }

        public IZone HomeZone { get; set; }

        /// <summary>
        /// Used for stuff like ordering of the households
        /// </summary>
        public int HouseholdId
        {
            get;
            set;
        }

        /// <summary>
        /// Number of Adults in the household
        /// </summary>
        public int NumberOfAdults
        {
            get
            {
                if (_NumberOfAdults == -1 )
                {
                    _NumberOfAdults = 0;
                    foreach (ITashaPerson p in Persons)
                    {
                        if (p.Adult) _NumberOfAdults++;
                    }
                }
                return _NumberOfAdults;
            }
        }

        /// <summary>
        /// Number of Children in the household (Non adults)
        /// </summary>
        public int NumberOfChildren
        {
            get
            {
                return Persons.Length - NumberOfAdults;
            }
        }

        /// <summary>
        /// The people in this household
        /// </summary>
        public ITashaPerson[] Persons
        {
            get;
            set;
        }

        /// <summary>
        /// The vehicles that belong to this household
        /// </summary>
        public IVehicle[] Vehicles
        {
            get;
            set;
        }

        public static Household MakeHousehold()
        {
            for (int i = 0; i < 10; i++)
            {
                if (Households.TryTake(out Household h))
                {
                    return h;
                }
                Thread.Sleep(0);
            }
            HouseholdsMade++;
            return new Household();
        }

        public List<ITashaPerson> GetJointTourMembers(int tourID)
        {
            List<ITashaPerson> persons = new List<ITashaPerson>(Persons.Length);
            foreach (var person in Persons)
            {
                foreach (var tripchain in person.TripChains)
                {
                    if (tripchain.JointTripID == tourID)
                        persons.Add(person);
                }
            }

            return persons;
        }

        public ITripChain GetJointTourTripChain(int tourID, ITashaPerson person)
        {
            foreach (var tripchain in person.TripChains)
            {
                if (tripchain.JointTripID == tourID)
                    return tripchain;
            }

            return null;
        }

        public void Recycle()
        {
            Release();
            var persons = Persons;
            for (int i = 0; i < persons.Length; i++)
            {
                persons[i].Recycle();
            }
            var vehicles = Vehicles;
            for (int i = 0; i < vehicles.Length; i++)
            {
                vehicles[i].Recycle();
            }
            _NumberOfAdults = -1;
            if(Households.Count < 100)
            {
                Households.Add(this);
            }
        }

        internal static void ReleaseHouseholdPool()
        {
            try
            {
                while (Households.TryTake(out Household notUsed))
                {
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        #region IHousehold Members

        public Dictionary<int, List<ITripChain>> JointTours
        {
            get
            {
                Dictionary<int, List<ITripChain>> jointTours = new Dictionary<int, List<ITripChain>>();

                foreach (var person in Persons)
                {
                    foreach (var tc in person.TripChains)
                    {
                        if (tc.JointTripChains == null )
                            continue;

                        foreach (var jtc in tc.JointTripChains)
                        {
                            if (jointTours.TryGetValue(jtc.JointTripID, out List<ITripChain> jointTour))
                            {
                                if (!jointTour.Contains(jtc))
                                {
                                    jointTour.Add(jtc);
                                }
                            }
                            else
                            {
                                jointTour = new List<ITripChain>(4);
                                jointTour.Add(jtc);
                                jointTours.Add(jtc.JointTripID, jointTour);
                            }
                        }
                    }
                }
                return jointTours;
            }
        }

        #endregion IHousehold Members

        #region IHousehold Members

        public HouseholdType HhType
        {
            get;
            set;
        }
        public int IncomeClass { get; internal set; }

        #endregion IHousehold Members

        #region IHousehold Members

        public ITashaHousehold Clone()
        {
            Household newH = (Household)MemberwiseClone();
            newH.Variables = new SortedList<string, object>();
            newH.Attach("Maintainer", this["Maintainer"] );
            newH.Persons = new ITashaPerson[Persons.Length];
            for ( int i = 0; i < Persons.Length; i++)
            {
                Person newPerson = (Person)Persons[i].Clone();
                newPerson.Household = newH;
                newH.Persons[i] = newPerson;
            }
            return newH;
        }

        #endregion IHousehold Members

        #region IHousehold Members

        #endregion IHousehold Members
    }
}