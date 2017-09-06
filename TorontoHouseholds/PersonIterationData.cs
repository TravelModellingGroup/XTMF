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

namespace Tasha.Common
{
    public class PersonIterationData : IPersonIterationData
    {
        private static ConcurrentBag<PersonIterationData> Items = new ConcurrentBag<PersonIterationData>();

        /// <summary>
        /// Creates a person's iteration data for the specified household iteration
        /// </summary>
        /// <param name="person">the person</param>
        private PersonIterationData(ITashaPerson person)
            : this()
        {
            TripChains = new List<ITripChain>();
            PopulateData( person );
        }

        public bool IterationSuccessful
        {
            get;
            private set;
        }

        public Dictionary<ITrip, ITashaMode> TripModes
        {
            get;
            set;
        }

        public static IPersonIterationData MakePersonIterationData(ITashaPerson person)
        {
            if (Items.TryTake(out PersonIterationData p))
            {
                p.PopulateData(person);
                return p;
            }
            return new PersonIterationData( person );
        }

        public ITashaMode ModeChosen(ITrip trip)
        {
            if (TripModes.TryGetValue(trip, out ITashaMode mode))
            {
                return mode;
            }
            return null;
        }

        public void PopulateData(ITashaPerson person)
        {
            IterationSuccessful = true;
            TripChains.AddRange( person.TripChains );
            TripChains.AddRange( person.AuxTripChains );
            //setting this persons trip chains for this iteration

            foreach ( var tc in TripChains )
            {
                foreach ( var t in tc.Trips )
                {
                    TripModes.Add( t, t.Mode );
                }
            }
        }

        #region IPersonIterationData Members

        public PersonIterationData()
        {
            TripModes = new Dictionary<ITrip, ITashaMode>();
            TripChains = new List<ITripChain>();
            IterationSuccessful = false;
        }

        public List<ITripChain> TripChains
        {
            get;
            set;
        }

        #endregion IPersonIterationData Members

        public void Recycle()
        {
            TripChains.Clear();
            TripModes.Clear();
            IterationSuccessful = false;
            Items.Add( this );
        }
    }
}