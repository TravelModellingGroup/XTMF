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
using Datastructure;
using Tasha.Common;
using XTMF;

namespace TMG.Tasha;

[ModuleInformation(Description = "This module is designed to load trip chains for a given person from TTS like trip records.")]
public class TripChainLoader : IDatachainLoader<ITashaPerson, ITripChain>, IDisposable
{
    [RunParameter("DestinationZone", 9, "The 0 indexed column that represents a person's Household ID." )]
    public int DestinationZone;

    [RunParameter("File Name", "Households/Trips.csv", "The name of the file that we will load the trips from." )]
    public string FileName;

    [RunParameter("Header", false, "The 0 indexed column that represents a person's Household ID." )]
    public bool Header;

    [RunParameter("Household ID", 0, "The 0 indexed column that represents a person's Household ID." )]
    public int HouseholdID;

    [RunParameter("JointTourID", 10, "The 0 indexed column that represents a person's Household ID." )]
    public int JointTourID;

    [RunParameter("JointTourRep", 11, "The 0 indexed column that represents a person's Household ID." )]
    public int JointTourRep;

    [RunParameter("Mode Conversion", "D:Auto,T:Transit,W:Walk", "[Observed mode character]:ModeName,[Another Observed mode character]:AnotherModeName" )]
    public string ModeConversion;

    [RunParameter("LoadNonHomeBasedTours", "", "If non-empty if a tour's first trip in the study period is not from home what name should we use to attach that data?  If nothing is selected then an error will be thrown." )]
    public string NonHomeBasedTourAttachName;

    [RunParameter("Number", 2, "The 2 indexed column that represents a person's Trip Number." )]
    public int Number;

    [RunParameter("ObservedMode", 4, "The 4 indexed column that represents the Observed Mode" )]
    public int ObservedMode;

    [RunParameter("Observed Mode Attachment Name", "ObservedMode", "The name of the attachment for the observed mode." )]
    public string ObservedModeAttachment;

    [RunParameter("Origin Zone Column", 7, "The 8 indexed column that represents the Origin Zone" )]
    public int OriginZone;

    [RunParameter("Override Bad Zones", false, "Continue to load trips where the zone numbers do not exist in the given zone system.  They will be filled in with the 'Bad Zone Number'." )]
    public bool OverrideBadZones;

    [RunParameter("PersonID", 1, "The 1 indexed column that represents a person's Person Number" )]
    public int PersonID;

    [RunParameter("PurposeDestination", 8, "The 9 indexed column that represents where the Trip Ends (Home, Work...)" )]
    public int PurposeDestination;

    [RunParameter("PurposeOrigin", 6, "The 6 indexed column that represents where the trip started (Home, Work...)" )]
    public int PurposeOrigin;

    [RunParameter("StartTime", 3, "The 3 indexed column that represents a the Trip's Start Time." )]
    public int StartTime;

    [RunParameter("Bad Zone Number", -1, "This zone number will be asserted if the origin or destination zone was not found.")]
    public int BadZoneNumber;
    private IZone _badZoneNumber = null;

    [RootModule]
    public ITashaRuntime TashaRuntime;

    private Dictionary<char, string> CharacterToModeNameConversion = [];
    private CsvReader Reader;
    private bool SkipReading;

    ~TripChainLoader()
    {
        Dispose( true );
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
        get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); }
    }

    [RunParameter("Household Iterations", 100, "The number of household iterations.")]
    public int HouseholdIterations;

    public bool Load(ITashaPerson person)
    {
        TripChain currentChain = null;
        _badZoneNumber = TashaRuntime.ZoneSystem.Get(BadZoneNumber);  
        if ( Reader == null )
        {
            Reader = new CsvReader( System.IO.Path.Combine( TashaRuntime.InputBaseDirectory, FileName ) );
            if ( Header )
            {
                if ( ( Reader.LoadLine() ) == 0 )
                {
                    return false;
                }
            }
        }

        while ( true )
        {
            if ( !SkipReading )
            {
                if ( ( Reader.LoadLine() ) == 0 )
                {
                    return true;
                }
            }
            SkipReading = false;
            Reader.Get( out int tempInt, HouseholdID );
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
            Trip t = Trip.GetTrip( HouseholdIterations );
            Reader.Get( out tempInt, PersonID );
            if ( tempInt != person.Id )
            {
                SkipReading = true;
                return true;
            }
            char purposeOrigin;
            int personID = tempInt - 1;
            Reader.Get( out float tempFloat, StartTime );
            t.TripStartTime = new Time( tempFloat / 100.00f );
            Reader.Get( out char tempChar1, PurposeOrigin );
            Reader.Get( out char tempChar2, PurposeDestination );
            purposeOrigin = tempChar1;
            t.Purpose = ActivityConverter.Converter.GetActivity( tempChar2 );
            Reader.Get( out tempInt, OriginZone );
            t.OriginalZone = TashaRuntime.ZoneSystem.Get( tempInt );
            if ( t.OriginalZone == null )
            {
                if ( !OverrideBadZones )
                {
                    throw new XTMFRuntimeException( this, "We were unable to load a trip starting from zone " + tempInt + " please make sure this zone exists!\r\nHousehold #" + person.Household.HouseholdId );
                }
                t.OriginalZone = _badZoneNumber;
            }

            Reader.Get( out tempInt, Number );
            t.TripNumber = tempInt;
            if ( person.TripChains.Count == 0 && ( t.OriginalZone != null && t.OriginalZone.ZoneNumber != person.Household.HomeZone.ZoneNumber ) )
            {
                if ( string.IsNullOrWhiteSpace( NonHomeBasedTourAttachName ) )
                {
                    throw new XTMFRuntimeException(this, "The Trip was thrown out because it didn't start at the original zone!\r\nHousehold #" + person.Household.HouseholdId + "\r\nThe home zone is #" + person.Household.HomeZone.ZoneNumber + " but the trip started at #" + t.OriginalZone.ZoneNumber );
                }
                else
                {
                    if ( currentChain == null )
                    {
                        person.TripChains.Add( currentChain = TripChain.MakeChain( person ) );
                    }
                    currentChain[NonHomeBasedTourAttachName] = ActivityConverter.Converter.GetActivity( purposeOrigin );
                }
            }
            Reader.Get( out tempInt, DestinationZone );
            t.DestinationZone = TashaRuntime.ZoneSystem.Get( tempInt );
            if ( t.DestinationZone == null )
            {
                if ( !OverrideBadZones )
                {
                    throw new XTMFRuntimeException(this, "We were unable to load a trip ending in zone " + tempInt + " please make sure this zone exists!\r\nHousehold #" + person.Household.HouseholdId );
                }
                t.DestinationZone = _badZoneNumber;
            }
            if ( ObservedMode >= 0 )
            {
                Reader.Get( out tempChar1, ObservedMode );
                var allModes = TashaRuntime.AllModes ?? throw new XTMFRuntimeException(this, "The TashaRuntime does not have a a list of modes set for AllModes yet!");
                var numberOfModes = allModes.Count;
                if (!CharacterToModeNameConversion.TryGetValue(tempChar1, out string name))
                {
                    t.Attach(ObservedModeAttachment, allModes[0]);
                }
                else
                {
                    for (int i = 0; i < numberOfModes; i++)
                    {
                        if (allModes[i].ModeName == name)
                        {
                            t.Attach(ObservedModeAttachment, TashaRuntime.AllModes[i]);
                            break;
                        }
                    }
                }
            }
            //if (lastChain != (chain = int.Parse(parts[TripChainNumber])))
            if ( currentChain == null 
                || ( t.OriginalZone != null && t.OriginalZone.ZoneNumber == person.Household.HomeZone.ZoneNumber && purposeOrigin == 'H' ) )
            {
                person.TripChains.Add( currentChain = TripChain.MakeChain( person ) );
                Reader.Get( out tempInt, JointTourID );
                currentChain.JointTripID = tempInt;
                Reader.Get( out tempInt, JointTourRep );
                currentChain.JointTripRep = ( tempInt - 1 == personID );
            }
            t.TripChain = currentChain;
            if ( (t.Purpose == Activity.PrimaryWork || t.Purpose == Activity.SecondaryWork) && t.TripChain.Person.EmploymentZone != t.DestinationZone )
            {
                t.Purpose = Activity.WorkBasedBusiness;
            }
            t.TripChain.Trips.Add( t );
        }
    }

    public void Reset()
    {
        Reader?.Reset();
    }

    /// <summary>
    /// This is called before the start method as a way to pre-check that all of the parameters that are selected
    /// are in fact valid for this module.
    /// </summary>
    /// <param name="error">A string that should be assigned a detailed error</param>
    /// <returns>If the validation was successful or if there was a problem</returns>
    public bool RuntimeValidation(ref string error)
    {
        CreateConversionDictionary();
        return true;
    }

    private void CreateConversionDictionary()
    {
        int state = 0;
        var length = ModeConversion.Length;
        char currentLetter = (char)0;
        string currentName = string.Empty;
        for ( int i = 0; i < length; i++ )
        {
            var c = ModeConversion[i];
            switch ( state )
            {
                case 0:
                    {
                        if (!((char.IsWhiteSpace(c)) | c == ','))
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
                            currentName = string.Empty;
                        }
                    }
                    break;

                case 2:
                    {
                        if ( c == ',' )
                        {
                            CharacterToModeNameConversion.Add( currentLetter, currentName );
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
            CharacterToModeNameConversion.Add( currentLetter, currentName );
        }
    }

    public void Dispose()
    {
        Dispose( true );
        GC.SuppressFinalize( this );
    }

    protected virtual void Dispose(bool all)
    {
        Unload();
    }

    public void Unload()
    {
        if(Reader != null)
        {
            Reader.Dispose();
            Reader = null;
        }
        TripChain.ReleaseChainPool();
        Trip.ReleaseTripPool();
    }
}