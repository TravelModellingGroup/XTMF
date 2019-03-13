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
using TMG;

namespace Tasha.Internal
{
    internal class TashaHousehold : Attachable, ITashaHousehold
    {
        public DwellingType DwellingType
        {
            get;
            internal set;
        }

        public float ExpansionFactor
        {
            get;
            set;
        }

        public HouseholdType HhType
        {
            get;
            internal set;
        }

        public IZone HomeZone
        {
            get;
            set;
        }

        public int HouseholdId
        {
            get;
            set;
        }

        public Dictionary<int, List<ITripChain>> JointTours
        {
            get;
            internal set;
        }

        public int NumberOfAdults
        {
            get
            {
                int total = 0;
                var people = Persons;
                for ( int i = 0; i < people.Length; i++)
                {
                    if(people[i].Adult) total++;
                }
                return total;
            }
        }

        public int NumberOfChildren
        {
            get
            {
                return Persons.Length - NumberOfAdults;
            }
        }

        public ITashaPerson[] Persons
        {
            get;
            set;
        }

        public IVehicle[] Vehicles
        {
            get;
            set;
        }

        public int IncomeClass { get; set; }

        public ITashaHousehold Clone()
        {
            TashaHousehold newH = (TashaHousehold)MemberwiseClone();
            newH.Variables = new SortedList<string, object>();
            newH.Attach("Maintainer", this["Maintainer"] );
            newH.Persons = new ITashaPerson[Persons.Length];
            for ( int i = 0; i < Persons.Length; i++)
            {
                TashaPerson newPerson = (TashaPerson)Persons[i].Clone();
                newPerson.Household = newH;
                newH.Persons[i] = newPerson;
            }
            return newH;
        }

        public List<ITashaPerson> GetJointTourMembers(int tourID)
        {
            List<ITashaPerson> persons = new List<ITashaPerson>(Persons.Length);
            foreach(var person in Persons)
            {
                foreach(var tripchain in person.TripChains)
                {
                    if(tripchain.JointTripID == tourID)
                    {
                        persons.Add(person);
                    }
                }
            }
            return persons;
        }

        public ITripChain GetJointTourTripChain(int tourID, ITashaPerson person)
        {
            foreach(var tripchain in person.TripChains)
            {
                if(tripchain.JointTripID == tourID)
                {
                    return tripchain;
                }
            }
            return null;
        }

        public void Recycle()
        {
            Release();
            foreach(var p in Persons)
            {
                p.Recycle();
            }
            foreach(var v in Vehicles)
            {
                v.Recycle();
            }
            Persons = null;
            Vehicles = null;
        }
    }
}