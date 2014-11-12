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
using TMG;

namespace DYL.Tasha
{
    /// <summary>
    /// Represents a person in the TASHA simulation
    /// </summary>
    public sealed class Person : Attachable, ITashaPerson
    {
        public static int PersonsMade;
        private static ConcurrentBag<Person> People = new ConcurrentBag<Person>();

        /// <summary>
        /// Creates a new person
        /// </summary>
        /// <param name="household">The household that this person belongs to, may not be null!</param>
        /// <param name="id">The identifyer for this person</param>
        /// <param name="age">The age of this person</param>
        /// <param name="employmentStatus">How this person is employed, if at all</param>
        /// <param name="studentStatus">If this person is a student, and if so what type of student</param>
        /// <param name="female">Is this person female</param>
        /// <param name="licence">Does this person have a driver's licence</param>
        public Person(ITashaHousehold household, int id, int age, Occupation occupation, TTSEmploymentStatus employmentStatus, StudentStatus studentStatus, bool license, bool female)
            : this()
        {
            this.Household = household;
            this.Id = id;
            this.Age = age;
            this.Occupation = occupation;
            this.EmploymentStatus = employmentStatus;
            this.StudentStatus = studentStatus;
            this.Licence = license;
            this.Female = female;
        }

        public Person()
        {
            this.TripChains = new List<ITripChain>( 5 );
            this.AuxTripChains = new List<ITripChain>( 2 );
            this.PersonIterationData = new List<IPersonIterationData>( 5 );
        }

        /// <summary>
        /// Is this person an adult
        /// </summary>
        public bool Adult
        {
            get { return this.Age >= 18; }
        }

        /// <summary>
        /// How old is this person
        /// </summary>
        public int Age { get; set; }

        public List<ITripChain> AuxTripChains
        {
            get;

            set;
        }

        public bool Child
        {
            get { return ( this.Age >= 0 && this.Age <= 10 ); }
        }

        /// <summary>
        /// Is this person employeed, and if so how?
        /// </summary>
        public TTSEmploymentStatus EmploymentStatus
        {
            get;
            set;
        }

        /// <summary>
        /// Where this person is employed at
        /// null otherwise
        /// </summary>
        public IZone EmploymentZone
        {
            get;
            set;
        }

        public float ExpansionFactor { get; set; }

        /// <summary>
        /// Is this person Female?
        /// </summary>
        public bool Female
        {
            get;
            set;
        }

        public bool FreeParking { get; set; }

        /// <summary>
        /// The household this person belongs to
        /// </summary>
        public ITashaHousehold Household
        {
            get;
            set;
        }

        /// <summary>
        /// What is this person's Identifyer
        /// </summary>
        public int Id { get; set; }

        public IPersonIterationData[] iterationData
        {
            get;
            private set;
        }

        /// <summary>
        /// Does this person have a driver's licence?
        /// </summary>
        public bool Licence
        {
            get;
            set;
        }

        /// <summary>
        /// Is this person Male?
        /// </summary>
        public bool Male
        {
            get { return !Female; }
        }

        /// <summary>
        /// What type of job do they have, if any
        /// </summary>
        public Occupation Occupation
        {
            get;
            set;
        }

        public IList<IPersonIterationData> PersonIterationData
        {
            get;
            set;
        }

        /// <summary>
        /// Where this person goes to school.
        /// null otherwise
        /// </summary>
        public IZone SchoolZone
        {
            get;
            set;
        }

        /// <summary>
        /// Is this person a student, and if so what kind
        /// </summary>
        public StudentStatus StudentStatus
        {
            get;
            set;
        }

        public TransitPass TransitPass { get; set; }

        /// <summary>
        /// The trip chains that belong to this person
        /// </summary>
        public List<ITripChain> TripChains
        {
            get;
            set;
        }

        public bool YoungAdult
        {
            get { return ( this.Age >= 16 && this.Age <= 19 ); }
        }

        /// <summary>
        /// Is this person a Youth
        /// </summary>
        public bool Youth
        {
            get { return ( this.Age >= 11 && this.Age <= 15 ); }
        }

        public ITashaPerson Clone()
        {
            return (ITashaPerson)this.MemberwiseClone();
        }

        public void Recycle()
        {
            this.Release();
            foreach ( var chain in this.TripChains )
            {
                chain.Recycle();
            }
            foreach ( var chain in this.AuxTripChains )
            {
                chain.Recycle();
            }
            this.TripChains.Clear();
            this.AuxTripChains.Clear();
            var length = this.PersonIterationData.Count;
            for ( int i = 0; i < length; i++ )
            {
                if ( this.PersonIterationData[i] != null )
                {
                    this.PersonIterationData[i].Recycle();
                }
            }
            this.PersonIterationData.Clear();
            People.Add( this );
        }

        internal static Person GetPerson()
        {
            Person p;
            if ( Person.People.TryTake( out p ) )
            {
                return p;
            }
            PersonsMade++;
            return new Person();
        }

        internal static void ReleasePersonPool()
        {
            Person p;
            try
            {
                while ( Person.People.TryTake( out p ) ) ;
            }
            catch ( ObjectDisposedException )
            {
            }
        }
    }
}