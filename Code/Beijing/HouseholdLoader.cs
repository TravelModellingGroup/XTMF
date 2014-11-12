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
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using Tasha.Common;
using Datastructure;
using XTMF;
using TMG;

namespace Beijing
{
    public class HouseholdLoader : IDataLoader<ITashaHousehold>, IDisposable
    {
        const int Threshhold = 400;

        bool AllDataLoaded = true;

        object Lock = new object();

        [RootModule]
        public ITashaRuntime TashaRuntime;

        [SubModelInformation( Description = "The next model that loads in person information for the household.", Required = true )]
        public IDatachainLoader<ITashaHousehold, ITashaPerson> PersonLoader;

        CsvReader Reader;

        [RunParameter( "FileName", "Households/Households.csv", "The csv file containing all of the household information." )]
        public string FileName;

        [RunParameter( "HouseholdID", 0, "The 0 indexed column that the household's id is located at." )]
        public int HouseholdIDCol;
        [RunParameter( "ZoneCol", 1, "The 0 indexed column that the household's id is located at." )]
        public int ZoneCol;
        [RunParameter( "ExpansionFactorCol", 2, "The 0 indexed column that the household's id is located at." )]
        public int ExpansionFactorCol;
        [RunParameter( "DwellingTypeCol", 3, "The 0 indexed column that the household's id is located at." )]
        public int DwellingTypeCol;
        [RunParameter( "PeopleCol", 4, "The 0 indexed column that the household's id is located at." )]
        public int PeopleCol;
        [RunParameter( "CarsCol", 5, "The 0 indexed column that the household's id is located at." )]
        public int CarsCol;
        [RunParameter( "IncomeCol", 6, "Th 0 indexed column that the household's income is located at." )]
        public int IncomeCol;
        [RunParameter( "Income Name", "Income", "The name of the income class attribute." )]
        public string IncomeName;
        [RunParameter( "Header", false, "True if the csv file contains a header." )]
        public bool ContainsHeader;

        [RunParameter( "Filter Facilitate Passenger", false, "Remove the trips that facilitate passenger" )]
        public bool FilterPassenger;

        [RunParameter( "Passenger Mode Name", "Passenger", "The name of the mode that does passenger." )]
        public string PassengerName;

        [DoNotAutomate]
        private ITashaMode Passenger;

        [RunParameter( "Max Trip Chain Size", 0, "The maximum size of a trip chain, 0 if any number." )]
        public int MaxTripchainSize;

        ITashaHousehold[] Households;

        public void CopyTo(ITashaHousehold[] array, int index)
        {
            throw new NotImplementedException();
        }

        public ITashaHousehold[] ToArray()
        {
            return this.Households;
        }

        public bool TryAdd(ITashaHousehold item)
        {
            throw new NotImplementedException();
        }

        public bool TryTake(out ITashaHousehold item)
        {
            throw new XTMFRuntimeException( this.Name + " is unable to load households one by one, please load all of the data at one time." );
        }

        public void LoadData()
        {
            List<ITashaHousehold> ourList = new List<ITashaHousehold>( 100000 );
            this.LoadAll( ourList );
            Households = ourList.ToArray();
        }

        private void LoadAll(List<ITashaHousehold> list)
        {
            lock ( this.Lock )
            {
                if ( this.Reader == null )
                {
                    this.Reader = new CsvReader( System.IO.Path.Combine( this.TashaRuntime.InputBaseDirectory, this.FileName ) );
                    if ( this.ContainsHeader )
                    {
                        this.Reader.LoadLine( );
                    }
                    this.AllDataLoaded = this.Reader.EndOfFile;
                }

                while ( LoadNextHousehold( list ) && !this.AllDataLoaded ) ;
            }
            this.Reset();
        }
        private IVehicleType AutoType;

        private bool LoadNextHousehold(IList<ITashaHousehold> list)
        {
            if ( this.Reader.LoadLine() == 0 )
            {
                this.AllDataLoaded = true;
                this.OutOfData = true;
                return false;
            }
            Household h = Household.MakeHousehold();
            int tempInt;
            float tempFloat;
            this.Reader.Get( out tempInt, this.HouseholdIDCol );
            h.HouseholdId = tempInt;
            this.Reader.Get( out tempInt, this.ZoneCol );
            h.HomeZone = this.TashaRuntime.ZoneSystem.Get( tempInt );
            this.Reader.Get( out tempFloat, this.ExpansionFactorCol );
            h.ExpansionFactor = tempFloat;
            int dwellingType;
            this.Reader.Get( out dwellingType, this.DwellingTypeCol );
            h.DwellingType = (DwellingType)dwellingType;

            this.Reader.Get( out tempInt, this.CarsCol );
            h.Vehicles = new IVehicle[tempInt];
            for ( int i = 0; i < h.Vehicles.Length; i++ )
            {
                h.Vehicles[i] = Vehicle.MakeVehicle( this.AutoType );
            }

            this.Reader.Get( out tempInt, this.IncomeCol );
            h[this.IncomeName] = tempInt;

            this.PersonLoader.Load( h );
            AssertType( h );
            bool add = true;

            foreach ( var person in h.Persons )
            {
                foreach ( var TripChains in person.TripChains )
                {
                    for ( int j = 0; j < ( TripChains.Trips.Count - 1 ); j++ )
                    {
                        var ThisTrip = TripChains.Trips[j];
                        var NextTrip = TripChains.Trips[j + 1];
                        var TripDuration = (int)( ( NextTrip.TripStartTime - ThisTrip.ActivityStartTime ).ToMinutes() / 15 );

                        if ( TripDuration < 0 )
                        {
                            add = false;
                        }
                    }
                }
            }

            if ( this.MaxTripchainSize > 0 )
            {
                foreach ( var p in h.Persons )
                {
                    foreach ( var tc in p.TripChains )
                    {
                        if ( add == false || tc.Trips.Count > this.MaxTripchainSize )
                        {
                            add = false;
                            break;
                        }
                        foreach ( var trip in tc.Trips )
                        {
                            var mode = trip["ObservedMode"] as IMode;
                            if ( mode == null )
                            {
                                // throw new XTMFRuntimeException( "Tried to check for an observed mode yet none were found!" );
                                add = false;
                                break;
                            }
                        }
                    }
                }
            }

            if ( this.FilterPassenger )
            {
                FilterFacilitatePassenger( h );
            }

            if ( add )
            {
                list.Add( h );
            }
            return true;
        }

        private void FilterFacilitatePassenger(Household h)
        {
            // in the first pass see if we have a passengerMode
            bool foundPassengerMode = false;
            for ( int i = 0; ( !foundPassengerMode ) & ( i < h.Persons.Length ); i++ )
            {
                for ( int j = 0; ( !foundPassengerMode ) & ( j < h.Persons[i].TripChains.Count ); j++ )
                {
                    for ( int k = 0; k < h.Persons[i].TripChains[j].Trips.Count; k++ )
                    {
                        if ( h.Persons[i].TripChains[j].Trips[k]["ObservedMode"] == this.Passenger )
                        {
                            foundPassengerMode = true;
                            break;
                        }
                    }
                }
            }
            if ( foundPassengerMode )
            {
                // if we detected it remove all trips of type facilitate passenger
                for ( int i = 0; i < h.Persons.Length; i++ )
                {
                    for ( int j = 0; j < h.Persons[i].TripChains.Count; j++ )
                    {
                        for ( int k = 0; k < h.Persons[i].TripChains[j].Trips.Count; k++ )
                        {
                            if ( h.Persons[i].TripChains[j].Trips[k].Purpose == Activity.FacilitatePassenger )
                            {
                                // if a trip chain no longer has any trips
                                h.Persons[i].TripChains.RemoveAt( j );
                                j--;
                                break;
                            }
                        }
                    }
                }
            }
        }

        private static void AssertType(Household h)
        {
            if ( h.NumberOfChildren == 0 )
            {
                GetTypeWithNoChildren( h );
            }
            else
            {
                GetTypeWithChildren( h );
            }
        }

        private static void GetTypeWithChildren(Household h)
        {
            if ( h.NumberOfAdults == 1 )
            {
                h.hhType = HouseholdType.LoneParentFamily;
            }
            else if ( h.NumberOfAdults == 2 )
            {
                if ( h.Persons[0].Female == h.Persons[1].Female )
                {
                    h.hhType = HouseholdType.OtherFamily;
                }
                else
                {
                    if ( AgeDifferenceLessThan( 20, h.Persons.ToArray() ) )
                    {
                        h.hhType = HouseholdType.CoupleWithChildren;
                    }
                    else
                    {
                        h.hhType = HouseholdType.OtherFamily;
                    }
                }
            }
            else
            {
                h.hhType = HouseholdType.OtherFamily;
            }
        }

        private static void GetTypeWithNoChildren(Household h)
        {
            if ( h.NumberOfAdults == 1 )
            {
                h.hhType = HouseholdType.OnePerson;
            }
            else if ( h.NumberOfAdults == 2 )
            {
                if ( h.Persons[0].Female == h.Persons[1].Female )
                {
                    h.hhType = HouseholdType.TwoOrMorePerson;
                }
                else
                {
                    if ( AgeDifferenceLessThan( 20, h.Persons ) )
                    {
                        h.hhType = HouseholdType.CoupleWithoutChildren;
                    }
                    else
                    {
                        h.hhType = HouseholdType.TwoOrMorePerson;
                    }
                }
            }
            else
            {
                h.hhType = HouseholdType.TwoOrMorePerson;
            }
        }


        private static bool AgeDifferenceLessThan(int span, IList<ITashaPerson> People)
        {
            int age = -1;
            foreach ( ITashaPerson p in People )
            {
                if ( p.Adult )
                {
                    if ( age == -1 )
                    {
                        age = p.Age;
                    }
                    else
                    {
                        return ( Math.Abs( age - p.Age ) < span );
                    }
                }
            }
            return false;
        }

        public IEnumerator<ITashaHousehold> GetEnumerator()
        {
            ITashaHousehold household;
            while ( this.TryTake( out household ) )
            {
                yield return household;
            }
            this.Reset();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public int Count
        {
            get { return this.Households.Length; }
        }

        public bool IsSynchronized
        {
            get { return true; }
        }

        public object SyncRoot
        {
            get { return this.Lock; }
        }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>( 100, 200, 100 ); }
        }

        public bool OutOfData
        {
            get;
            set;
        }

        public void Reset()
        {
            lock ( this.Lock )
            {
                this.PersonLoader.Reset();
                if ( this.Reader != null )
                {
                    this.Reader.Reset();
                    if ( this.ContainsHeader )
                    {
                        this.Reader.LoadLine();
                    }
                    this.AllDataLoaded = false;
                }
            }
        }

        /// <summary>
        /// This is called before the start method as a way to pre-check that all of the parameters that are selected
        /// are in fact valid for this module.
        /// </summary>
        /// <param name="error">A string that should be assigned a detailed error</param>
        /// <returns>If the validation was successful or if there was a problem</returns>
        public bool RuntimeValidation(ref string error)
        {
            var householdFile = System.IO.Path.Combine( this.TashaRuntime.InputBaseDirectory, this.FileName );
            this.AutoType = this.TashaRuntime.AutoType;
            try
            {
                if ( !System.IO.File.Exists( householdFile ) )
                {
                    error = String.Concat( "The file ", householdFile, " does not exist!" );
                    return false;
                }
            }
            catch
            {
                error = String.Concat( "We were unable to access ", householdFile, " the path may be invalid or unavailable at this time." );
                return false;
            }
            if ( this.FilterPassenger )
            {
                try
                {
                    foreach ( var mode in this.TashaRuntime.AllModes )
                    {
                        if ( mode.ModeName == this.PassengerName )
                        {
                            this.Passenger = mode;
                            return true;
                        }
                    }
                }
                catch
                {
                    foreach ( var mode in this.TashaRuntime.SharedModes )
                    {
                        if ( mode.ModeName == this.PassengerName )
                        {
                            this.Passenger = mode;
                            return true;
                        }
                    }
                }
                error = "We did not find any shared mode named " + this.PassengerName + " to filter with!";
                return false;
            }
            return true;
        }

        public void Dispose()
        {
            this.Dispose( true );
            GC.SuppressFinalize( this );   
        }

        protected virtual void Dispose(bool all)
        {
            if ( this.Reader != null )
            {
                this.Reader.Dispose();
                this.Reader = null;
            }
        }
    }
}
