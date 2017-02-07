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
using System.IO;
using Datastructure;
using Tasha.Common;
using TMG;
using TMG.Input;
using XTMF;

namespace Tasha.PopulationSynthesis
{
    public class ExtractPersonData : IPostHousehold
    {
        [RunParameter( "Age Sets", "0-10,11-15,16-18,19-25,26-30,31-100", typeof( RangeSet ), "The different age categories to break the population into." )]
        public RangeSet AgeSets;

        [RunParameter( "File Name", "HouseholdData.csv", typeof( FileFromOutputDirectory ), "The location to save our data to." )]
        public FileFromOutputDirectory SaveFile;

        private PersonData Data;

        public string Name { get; set; }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public void Execute(ITashaHousehold household, int iteration)
        {
            var persons = household.Persons;
            var pd = household.HomeZone.PlanningDistrict;
            var expFactor = household.ExpansionFactor;
            for ( int i = 0; i < persons.Length; i++ )
            {
                Data.AddEntry( persons[i], pd, expFactor );
            }
        }

        public void IterationFinished(int iteration)
        {
            Data.Save();
            Data = null;
        }

        public void Load(int maxIterations)
        {
            // We will handel this at the start of an iteration
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void IterationStarting(int iteration)
        {
            Data = new PersonData( this );
        }

        private class PersonData
        {
            private List<PersonEntry> EntryList;

            private GatewayLock EntryLock;

            private ExtractPersonData self;

            public PersonData(ExtractPersonData self)
            {
                this.self = self;
                EntryList = new List<PersonEntry>( 25 );
                EntryLock = new GatewayLock();
            }

            internal void AddEntry(ITashaPerson tashaPerson, int pd, float expFactor)
            {
                bool success = false;
                var ageCat = self.AgeSets.IndexOf( tashaPerson.Age );
                var occ = tashaPerson.Occupation;
                var empStat = tashaPerson.EmploymentStatus;
                var female = tashaPerson.Female;
                var dlic = tashaPerson.Licence;
                EntryLock.PassThrough( () =>
                {
                    for ( int i = 0; i < EntryList.Count; i++ )
                    {
                        if ( EntryList[i].AddIfEquals( ageCat, occ, empStat, dlic, female, expFactor ) )
                        {
                            success = true;
                            return;
                        }
                    }
                } );
                if ( !success )
                {
                    EntryLock.Lock( () =>
                    {
                        for ( int i = 0; i < EntryList.Count; i++ )
                        {
                            if ( EntryList[i].AddIfEquals( ageCat, occ, empStat, dlic, female, expFactor ) )
                            {
                                return;
                            }
                        }
                        // if we get here it doesn't exist yet
                        // we don't need to lock here since the writer is always unique
                        EntryList.Add( new PersonEntry()
                        {
                            AgeCat = ageCat,
                            Occupation = occ,
                            EmploymentStatus = empStat,
                            DriversLicense = dlic,
                            Female = female,
                            ExpansionFactor = expFactor,
                            PlaningDistrict = pd
                        } );
                    } );
                }
            }

            internal void Save()
            {
                using ( StreamWriter writer = new StreamWriter( self.SaveFile.GetFileName() ) )
                {
                    WriterHeader( writer );
                    for ( int entry = 0; entry < EntryList.Count; entry++ )
                    {
                        var personType = EntryList[entry];
                        writer.Write( personType.PlaningDistrict );
                        writer.Write( ',' );
                        writer.Write( personType.AgeCat );
                        writer.Write( ',' );
                        writer.Write( personType.Female ? 'F' : 'M' );
                        writer.Write( ',' );
                        writer.Write( personType.DriversLicense ? 'Y' : 'N' );
                        writer.Write( ',' );
                        switch ( personType.EmploymentStatus )
                        {
                            case TTSEmploymentStatus.FullTime:
                                writer.Write( 'F' );
                                break;

                            case TTSEmploymentStatus.PartTime:
                                writer.Write( 'P' );
                                break;

                            default:
                                writer.Write( 'O' );
                                break;
                        }
                        writer.Write( ',' );
                        switch ( personType.Occupation )
                        {
                            case Occupation.Professional:
                                writer.Write( 'P' );
                                break;

                            case Occupation.Office:
                                writer.Write( 'G' );
                                break;

                            case Occupation.Retail:
                                writer.Write( 'S' );
                                break;

                            case Occupation.Manufacturing:
                                writer.Write( 'M' );
                                break;

                            default:
                                writer.Write( 'O' );
                                break;
                        }
                        writer.Write( ',' );
                        writer.WriteLine( personType.ExpansionFactor );
                    }
                }
            }

            private void WriterHeader(StreamWriter writer)
            {
                writer.WriteLine( "PD,AgeCategory,Gender,License,EmploymentStatus,Occupation,ExpansionFactor" );
            }

            private class PersonEntry
            {
                internal int AgeCat;
                internal bool DriversLicense;
                internal TTSEmploymentStatus EmploymentStatus;
                internal float ExpansionFactor;
                internal bool Female;
                internal Occupation Occupation;
                internal int PlaningDistrict;

                internal bool AddIfEquals(int ageCat, Occupation occ, TTSEmploymentStatus empStat, bool dlic, bool female, float expFac)
                {
                    if (
                            AgeCat == ageCat
                        & Female == female
                        & Occupation == occ
                        & EmploymentStatus == empStat
                        & DriversLicense == dlic
                            )
                    {
                        lock ( this )
                        {
                            ExpansionFactor += expFac;
                        }
                        return true;
                    }
                    return false;
                }
            }
        }
    }
}