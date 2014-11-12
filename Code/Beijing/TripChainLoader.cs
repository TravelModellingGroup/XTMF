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
using Datastructure;
using Tasha;
using Tasha.Common;
using XTMF;
using TMG;

namespace Beijing
{
    public class TripChainLoader : IDatachainLoader<ITashaPerson, ITripChain>, IDisposable
    {
        CsvReader Reader;

        [RootModule]
        public ITashaRuntime TashaRuntime;

        [RunParameter( "Household ID", 0, "The 0 indexed column that represents a person's Household ID." )]
        public int HouseholdID;
        [RunParameter( "PersonID", 1, "The 0 indexed column that represents a person's Household ID." )]
        public int PersonID;
        [RunParameter( "Number", 2, "The 0 indexed column that represents a person's Household ID." )]
        public int Number;
        [RunParameter( "ObservedMode", 3, "The 0 indexed column that represents a person's Household ID." )]
        public int ObservedMode;
        [RunParameter( "PurposeOrigin", 4, "The 0 indexed column that represents a person's Household ID." )]
        public int PurposeOrigin;
        [RunParameter( "PlanningDistrictOrigin", 5, "The 0 indexed column that represents a person's Household ID." )]
        public int PlanningDistrictOrigin;
        [RunParameter( "Origin Zone Column", 6, "The 0 indexed column that represents a person's Household ID." )]
        public int OriginZone;
        [RunParameter( "PurposeDestination", 7, "The 0 indexed column that represents a person's Household ID." )]
        public int PurposeDestination;
        [RunParameter( "DestinationPlanningDistrct", 8, "The 0 indexed column that represents a person's Household ID." )]
        public int DestinationPlanningDistrct;
        [RunParameter( "DestinationZone", 9, "The 0 indexed column that represents a person's Household ID." )]
        public int DestinationZone;
        [RunParameter( "JointTourID", 10, "The 0 indexed column that represents a person's Household ID." )]
        public int JointTourID;
        [RunParameter( "JointTourRep", 11, "The 0 indexed column that represents a person's Household ID." )]
        public int JointTourRep;
        [RunParameter( "Distance", 12, "The 0 indexed column that represents a person's Household ID." )]
        public int DistanceCol;
        [RunParameter( "Travel Time", 13, "The 0 indexed column that represents a person's Household ID." )]
        public int TravelTimeCol;
        [RunParameter( "StartTime", 14, "The 0 indexed column that represents a person's Household ID." )]
        public int StartTime;
        [RunParameter( "File Name", "Households/Trips.csv", "The name of the file that we will load the trips from." )]
        public string FileName;
        [RunParameter( "Mode Conversion", "D:Auto,T:Transit,W:Walk", "[Observed mode character]:ModeName,[Another Observed mode character]:AnotherModeName" )]
        public string ModeConversion;
        [RunParameter( "Observed Mode Attachment Name", "ObservedMode", "The name of the attachment for the observed mode." )]
        public string ObservedModeAttachment;
        [RunParameter( "Header", false, "The 0 indexed column that represents a person's Household ID." )]
        public bool Header;
        [RunParameter( "DefaultObsMode", "", "The default mode for an observed trip's name" )]
        public string DefaultModeName;

        private IMode DefaultMode;

        private bool SkipReading = false;
        private long ReadingPosition;

        private Dictionary<char, string> CharacterToModeNameConversion = new Dictionary<char, string>();

        private bool CreateConversionDictionary(ref string error)
        {
            int state = 0;
            var length = this.ModeConversion.Length;
            char currentLetter = (char)0;
            string currentName = String.Empty;
            for ( int i = 0; i < length; i++ )
            {
                var c = this.ModeConversion[i];
                switch ( state )
                {
                    case 0:
                        {
                            if ( ( Char.IsWhiteSpace( c ) ) | c == ',' )
                            {
                                continue;
                            }
                            else
                            {
                                currentLetter = c;
                                state = 1;
                            }
                        }
                        break;
                    case 1:
                        {
                            if ( c == ':' )
                            {
                                state = 2;
                                currentName = String.Empty;
                            }
                            else
                            {
                                error = "The identifier of the mode in the data must be 1 character long!";
                                return false;
                            }
                        }
                        break;
                    case 2:
                        {
                            if ( c == ',' )
                            {
                                this.CharacterToModeNameConversion.Add( currentLetter, currentName );
                                state = 0;
                            }
                            else
                            {
                                currentName += c;
                            }
                        }
                        break;
                }
            }
            if ( state == 2 )
            {
                this.CharacterToModeNameConversion.Add( currentLetter, currentName );
            }
            return true;
        }

        [RunParameter("Household Iterations", 100, "The number of household iterations.")]
        public int HouseholdIterations;

        public bool Load(ITashaPerson person)
        {
            int length = 0;
            int tempInt;
            float tempFloat;
            char tempChar1, tempChar2;
            TripChain currentChain = null;

            if ( this.Reader == null )
            {
                this.Reader = new CsvReader( System.IO.Path.Combine( this.TashaRuntime.InputBaseDirectory, this.FileName ) );
                if ( this.Header )
                {
                    if ( ( length = this.Reader.LoadLine()) == 0 )
                    {
                        return false;
                    }
                }
            }

            while ( true )
            {
                if ( !SkipReading )
                {
                    if ( ( length = this.Reader.LoadLine()) == 0 )
                    {
                        return true;
                    }
                }
                SkipReading = false;
                this.Reader.Get( out tempInt, this.HouseholdID );
                if ( tempInt < person.Household.HouseholdId )
                {
                    SkipReading = false;
                    continue;
                }
                else if ( tempInt > person.Household.HouseholdId )
                {
                    SkipReading = true;
                    return true;
                }
                Trip t = new Trip( HouseholdIterations );
                this.Reader.Get( out tempInt, PersonID );
                char purposeOrigin;
                int personID = tempInt;
                if ( personID != person.Id )
                {
                    SkipReading = true;
                    return true;
                }

                // Set the Times.
                string tempStr;

                this.Reader.Get( out tempStr, StartTime );
                string tempStr2;
                this.Reader.Get( out tempStr2, TravelTimeCol );
                t.TravelTime = new Time( tempStr2 );
                t.TripStartTime = new Time( tempStr );
                t.ActivityStartTime = t.TripStartTime + t.TravelTime;

                this.Reader.Get( out tempChar1, PurposeOrigin );
                this.Reader.Get( out tempChar2, PurposeDestination );
                purposeOrigin = tempChar1;
                t.Purpose = ActivityConverter.Converter.GetActivity( tempChar2 );
                this.Reader.Get( out tempInt, OriginZone );
                t.OriginalZone = this.TashaRuntime.ZoneSystem.Get( tempInt );
                if ( t.OriginalZone == null )
                {
                    throw new XTMFRuntimeException( "We were unable to load a trip starting from zone " + tempInt + " please make sure this zone exists!\r\nHousehold #" + person.Household.HouseholdId );
                }
                this.Reader.Get( out tempInt, Number );
                t.TripNumber = tempInt;
                if ( person.TripChains.Count == 0 && t.OriginalZone.ZoneNumber != person.Household.HomeZone.ZoneNumber )
                {
                    continue;
                }
                this.Reader.Get( out tempInt, DestinationZone );
                t.DestinationZone = this.TashaRuntime.ZoneSystem.Get( tempInt );
                if ( t.DestinationZone == null )
                {
                    throw new XTMFRuntimeException( "We were unable to load a trip ending in zone " + tempInt + " please make sure this zone exists!\r\nHousehold #" + person.Household.HouseholdId );
                }
                if ( ObservedMode >= 0 )
                {
                    this.Reader.Get( out tempChar1, ObservedMode );
                    var allModes = this.TashaRuntime.AllModes;

                    if ( allModes == null )
                    {
                        throw new XTMFRuntimeException( "We did not find any modes! Please make sure that you have loaded all modes" );
                    }

                    var numberOfModes = allModes.Count;
                    string name;
                    if ( !this.CharacterToModeNameConversion.TryGetValue( tempChar1, out name ) )
                    {
                        t.Attach( this.ObservedModeAttachment, this.TashaRuntime.AllModes[0] );
                    }
                    else
                    {
                        bool found = false;
                        for ( int i = 0; i < numberOfModes; i++ )
                        {
                            if ( allModes[i].ModeName == name )
                            {
                                t.Attach( this.ObservedModeAttachment, this.TashaRuntime.AllModes[i] );
                                found = true;
                                break;
                            }
                        }
                        if ( !found )
                        {
                            if ( this.DefaultMode == null )
                            {
                                //throw new XTMFRuntimeException( "An unknown observed mode was found and there is no default mode selected!" );
                            }
                            else
                            {
                                t.Attach( this.ObservedModeAttachment, this.DefaultMode );
                            }
                        }
                    }
                }

                // Load the Beijing specific data                
                this.Reader.Get( out tempFloat, this.DistanceCol );
                t["Distance"] = tempFloat;

                //if (lastChain != (chain = int.Parse(parts[TripChainNumber])))
                if ( currentChain == null || ( t.OriginalZone.ZoneNumber == person.Household.HomeZone.ZoneNumber && purposeOrigin == 'H' ) )
                {
                    person.TripChains.Add( currentChain = TripChain.MakeChain( person ) );
                    this.Reader.Get( out tempInt, JointTourID );
                    currentChain.JointTripID = tempInt;
                    this.Reader.Get( out tempInt, JointTourRep );
                    currentChain.JointTripRep = ( tempInt - 1 == personID );
                }

                t.TripChain = currentChain;
                t.TripChain.Trips.Add( t );

                ReadingPosition += length + 2;
            }
        }

        public void Reset()
        {
            if ( this.Reader != null )
            {
                this.Reader.Reset();
            }
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

        /// <summary>
        /// This is called before the start method as a way to pre-check that all of the parameters that are selected
        /// are in fact valid for this module.
        /// </summary>
        /// <param name="error">A string that should be assigned a detailed error</param>
        /// <returns>If the validation was successful or if there was a problem</returns>
        public bool RuntimeValidation(ref string error)
        {
            var tripFile = System.IO.Path.Combine( this.TashaRuntime.InputBaseDirectory, this.FileName );
            try
            {
                if ( !System.IO.File.Exists( tripFile ) )
                {
                    error = String.Concat( "The file ", tripFile, " does not exist!" );
                    return false;
                }
            }
            catch
            {
                error = String.Concat( "We were unable to access ", tripFile, " the path may be invalid or unavailable at this time." );
                return false;
            }
            if ( !this.CreateConversionDictionary( ref error ) )
            {
                return false;
            }
            if ( !String.IsNullOrWhiteSpace( this.DefaultModeName ) )
            {
                bool found = false;
                if ( this.TashaRuntime.AutoMode != null )
                {
                    if ( this.TashaRuntime.AutoMode.ModeName == this.DefaultModeName )
                    {
                        found = true;
                        this.DefaultMode = this.TashaRuntime.AutoMode;
                    }
                    if ( !found )
                    {
                        foreach ( var mode in this.TashaRuntime.OtherModes )
                        {
                            if ( mode.ModeName == this.DefaultModeName )
                            {
                                this.DefaultMode = mode;
                                found = true;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    foreach ( var mode in this.TashaRuntime.AllModes )
                    {
                        if ( mode.ModeName == this.DefaultModeName )
                        {
                            this.DefaultMode = mode;
                            found = true;
                            break;
                        }
                    }
                }
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
