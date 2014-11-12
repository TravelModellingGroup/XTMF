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
using Datastructure;
using Tasha.Common;
using TMG;
using XTMF;

namespace DYL.Tasha
{
    public class PersonLoader : IDatachainLoader<ITashaHousehold, ITashaPerson>, IDisposable
    {
        [RunParameter( "FileName", "Households/People.csv", "The file name of the csv file that we will load people from." )]
        public string FileName;

        [RunParameter( "Header", false, "Is there a header in the CSV file?" )]
        public bool Header;

        [RunParameter( "Age", 2, "The 0 indexed column that represents a person's age." )]
        public int PersonAgeCol;

        [RunParameter( "DriversLicence", 4, "The 0 indexed column that represents if a person has a driver's license." )]
        public int PersonDriversLicenceCol;

        [RunParameter( "EmploymentStatus", 6, "The 0 indexed column that represents a person's employment status." )]
        public int PersonEmploymentStatusCol;

        [RunParameter( "EmploymentZone", 11, "The 0 indexed column that represents a person's work zone." )]
        public int PersonEmploymentZoneCol;

        [RunParameter( "FreeParking", 8, "The 0 indexed column that represents a person receives free parking for work." )]
        public int PersonFreeParkingCol;

        [RunParameter( "Gender", 3, "The 0 indexed column that represents a person's gender (M/F)." )]
        public int PersonGenderCol;

        [RunParameter( "Household ID", 0, "The 0 indexed column that represents a person's Household ID." )]
        public int PersonHouseholdID;

        [RunParameter( "ID", 1, "The 0 indexed column that represents a person's ID." )]
        public int PersonIDCol;

        [RunParameter( "Occupation", 7, "The 0 indexed column that represents a person's occupation." )]
        public int PersonOccupationCol;

        [RunParameter( "Student", 9, "The 0 indexed column that represents a person is a student." )]
        public int PersonStudentCol;

        [RunParameter( "StudentZone", 14, "The 0 indexed column that represents a person's school zone." )]
        public int PersonStudentZoneCol;

        [RunParameter( "TransitPass", 5, "The 0 indexed column that represents a person has a transit pass." )]
        public int PersonTransitPassCol;

        [SubModelInformation( Description = "The school location choice model.", Required = false )]
        public TMG.ICalculation<ITashaPerson, IZone> SchoolLocationChoiceModel;

        [RootModule]
        public ITashaRuntime TashaRuntime;

        [SubModelInformation( Description = "The loader for trips and their chains, only include this if you are not using a scheduler.", Required = false )]
        public IDatachainLoader<ITashaPerson, ITripChain> TripchainLoader;

        [RunParameter( "Unknown Zone#", 99999, "The zone number representing a zone that we don't know about" )]
        public int UnknownZoneNumber;

        [SubModelInformation( Description = "The work location choice model.", Required = false )]
        public TMG.ICalculation<ITashaPerson, IZone> WorkLocationChoiceModel;

        private ConcurrentBag<ITashaPerson> AvailablePeople = new ConcurrentBag<ITashaPerson>();
        private bool ContainsData = false;
        private ConcurrentQueue<ITashaPerson> LoadedItems = new ConcurrentQueue<ITashaPerson>();

        private CsvReader Reader;

        public PersonLoader()
        {
        }

        ~PersonLoader()
        {
            this.Dispose( false );
        }

        public string Name
        {
            get;
            set;
        }

        public bool OutOfData
        {
            get { return false; }
        }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>( 100, 200, 100 ); }
        }

        public void Dispose()
        {
            this.Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose(bool managedOnly)
        {
            Person.ReleasePersonPool();
            if ( this.Reader != null )
            {
                this.Reader.Close();
                this.Reader = null;
            }
        }

        public bool Load(ITashaHousehold household)
        {
            if ( this.Reader == null )
            {
                this.Reader = new CsvReader( System.IO.Path.Combine( this.TashaRuntime.InputBaseDirectory, this.FileName ) );
                if ( Header )
                {
                    this.Reader.LoadLine();
                }
            }
            if ( !this.ContainsData )
            {
                if ( this.Reader.LoadLine() == 0 )
                {
                    return false;
                }
            }
            int hhldid = household.HouseholdId;
            int tempInt = 0;
            char tempChar;
            var persons = new List<ITashaPerson>();
            while ( true )
            {
                this.Reader.Get( out tempInt, PersonHouseholdID );
                if ( tempInt != household.HouseholdId )
                {
                    if ( tempInt < household.HouseholdId )
                    {
                        if ( this.Reader.LoadLine() == 0 )
                        {
                            return false;
                        }
                        continue;
                    }
                    this.ContainsData = true;
                    household.Persons = persons.ToArray();
                    return true;
                }

                //Person p = Person.GetPerson();
                Person p = Person.GetPerson();
                p.Household = household;
                this.Reader.Get( out tempInt, PersonIDCol );
                p.Id = tempInt;
                this.Reader.Get( out tempInt, PersonAgeCol );
                p.Age = tempInt;
                this.Reader.Get( out tempChar, PersonGenderCol );
                p.Female = ( tempChar == 'F' ) | ( tempChar == 'f' );
                this.Reader.Get( out tempChar, PersonDriversLicenceCol );
                p.Licence = ( tempChar == 'Y' );
                this.Reader.Get( out tempChar, PersonTransitPassCol );
                p.TransitPass = (TransitPass)tempChar;
                this.Reader.Get( out tempChar, PersonEmploymentStatusCol );
                p.EmploymentStatus = (TTSEmploymentStatus)( tempChar );
                this.Reader.Get( out tempChar, PersonOccupationCol );
                p.Occupation = GetOccupation( tempChar );
                this.Reader.Get( out tempChar, PersonFreeParkingCol );
                p.FreeParking = tempChar == 'Y' ? true : false;
                this.Reader.Get( out tempChar, PersonStudentCol );
                p.StudentStatus = GetStudentStatus( tempChar );
                this.Reader.Get( out tempInt, PersonStudentZoneCol );
                p.SchoolZone = tempInt != 0 ? ( tempInt == this.UnknownZoneNumber && SchoolLocationChoiceModel != null ? SchoolLocationChoiceModel.ProduceResult( p ) : this.TashaRuntime.ZoneSystem.Get( tempInt ) ) : null;
                this.Reader.Get( out tempInt, PersonEmploymentZoneCol );
                p.EmploymentZone = tempInt != 0 ? ( tempInt == this.UnknownZoneNumber && WorkLocationChoiceModel != null ? WorkLocationChoiceModel.ProduceResult( p ) : this.TashaRuntime.ZoneSystem.Get( tempInt ) ) : null;
                if ( this.TripchainLoader != null )
                {
                    this.TripchainLoader.Load( p );
                }
                persons.Add( p );
                if ( this.Reader.LoadLine() == 0 )
                {
                    this.ContainsData = false;
                    household.Persons = persons.ToArray();
                    return true;
                }
            }
        }

        public void Reset()
        {
            if ( this.TripchainLoader != null )
            {
                this.TripchainLoader.Reset();
            }
            if ( this.Reader != null )
            {
                this.ContainsData = false;
                this.Reader.Reset();
                if ( Header )
                {
                    this.Reader.LoadLine();
                }
                this.ContainsData = false;
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
            var personFile = System.IO.Path.Combine( this.TashaRuntime.InputBaseDirectory, this.FileName );
            try
            {
                if ( !System.IO.File.Exists( personFile ) )
                {
                    error = String.Concat( "The file ", personFile, " does not exist!" );
                    return false;
                }
            }
            catch
            {
                error = String.Concat( "We were unable to access ", personFile, " the path may be invalid or unavailable at this time." );
                return false;
            }
            return true;
        }

        private static TTSEmploymentStatus GetEmploymentStatus(char status)
        {
            switch ( status )
            {
                case 'O':
                    return TTSEmploymentStatus.NotEmployed;

                case 'F':
                    return TTSEmploymentStatus.FullTime;

                case 'P':
                    return TTSEmploymentStatus.PartTime;

                default:
                    return TTSEmploymentStatus.Unknown;
            }
        }

        private static Occupation GetOccupation(char occ)
        {
            switch ( occ )
            {
                case 'G':
                    return Occupation.Office;

                case 'M':
                    return Occupation.Manufacturing;

                case 'P':
                    return Occupation.Professional;

                case 'S':
                    return Occupation.Retail;

                case 'F':
                    return Occupation.Farmer;

                case 'O':
                    return Occupation.NotEmployed;
            }
            return Occupation.Unknown;
        }

        private static StudentStatus GetStudentStatus(char status)
        {
            switch ( status )
            {
                case 'O':
                    return StudentStatus.NotStudent;

                case 'S':
                    return StudentStatus.FullTime;

                case 'P':
                    return StudentStatus.PartTime;

                default:
                    return StudentStatus.Unknown;
            }
        }
    }
}