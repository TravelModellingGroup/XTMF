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
using TMG.Input;
using XTMF;

namespace Tasha.PopulationSynthesis
{
    public class ExtractHouseholdData : IPostHousehold
    {
        [RunParameter( "Age Sets", "0-10,11-15,16-18,19-25,26-30,31-100", typeof( RangeSet ), "The different age categories to break the population into." )]
        public RangeSet AgeSets;

        [RunParameter( "File Name", "HouseholdData.csv", typeof( FileFromOutputDirectory ), "The location to save our data to." )]
        public FileFromOutputDirectory SaveFile;

        private HouseholdData Data;

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
            int[] agesTemp = new int[AgeSets.Count];
            var persons = household.Persons;
            for ( int i = 0; i < persons.Length; i++ )
            {
                var index = AgeSets.IndexOf( persons[i].Age );
                if ( index >= 0 )
                {
                    agesTemp[index]++;
                }
            }
            Data.Add( agesTemp, household.HomeZone.PlanningDistrict, household.Vehicles.Length, household.ExpansionFactor );
        }

        public void IterationFinished(int iteration)
        {
            Data.Save( this );
        }

        public void Load(int maxIterations)
        {
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void IterationStarting(int iteration)
        {
            InitializeData();
        }

        private void InitializeData()
        {
            Data = new HouseholdData();
        }

        private class HouseholdData
        {
            private List<HouesholdAgeEntry> EntryList;

            private GatewayLock EntryLock;

            public HouseholdData()
            {
                EntryList = new List<HouesholdAgeEntry>( 25 );
                EntryLock = new GatewayLock();
            }

            internal void Add(int[] ageCategoryCount, int planningDistrict, int numberOfCars, float expansionFactor)
            {
                int lastIndex;
                bool success = false;
                EntryLock.PassThrough( () =>
                    {
                        lastIndex = EntryList.Count;
                        for ( int i = 0; i < lastIndex; i++ )
                        {
                            if ( EntryList[i].Equals( ageCategoryCount, planningDistrict, numberOfCars ) )
                            {
                                lock ( EntryList[i] )
                                {
                                    EntryList[i].ExpandedTotal += expansionFactor;
                                    success = true;
                                    return;
                                }
                            }
                        }
                    } );
                if ( !success )
                {
                    EntryLock.Lock( () =>
                        {
                            for ( int i = 0; i < EntryList.Count; i++ )
                            {
                                if ( EntryList[i].Equals( ageCategoryCount, planningDistrict, numberOfCars ) )
                                {
                                    // we don't need to lock here since the writer is always unique
                                    EntryList[i].ExpandedTotal += expansionFactor;
                                    return;
                                }
                            }
                            // if we get here it doesn't exist yet
                            // we don't need to lock here since the writer is always unique
                            EntryList.Add( new HouesholdAgeEntry()
                            {
                                AgeCategoryCount = ageCategoryCount,
                                PlanningDistrict = planningDistrict,
                                NumberOfCars = numberOfCars,
                                ExpandedTotal = expansionFactor
                            } );
                        } );
                }
            }

            internal void Save(ExtractHouseholdData root)
            {
                using StreamWriter writer = new StreamWriter(root.SaveFile.GetFileName());
                WriteHeader(root, writer);
                for (int i = 0; i < EntryList.Count; i++)
                {
                    var entry = EntryList[i];
                    writer.Write(entry.PlanningDistrict);
                    writer.Write(',');
                    for (int j = 0; j < entry.AgeCategoryCount.Length; j++)
                    {
                        writer.Write(entry.AgeCategoryCount[j]);
                        writer.Write(',');
                    }
                    writer.Write(entry.NumberOfCars);
                    writer.Write(',');
                    writer.WriteLine(entry.ExpandedTotal);
                }
            }

            private static void WriteHeader(ExtractHouseholdData root, StreamWriter writer)
            {
                writer.Write( "PD," );
                for ( int i = 0; i < root.AgeSets.Count; i++ )
                {
                    writer.Write( root.AgeSets[i].ToString() );
                    writer.Write( ',' );
                }
                writer.WriteLine( "Vehicles,ExpansionFactor" );
            }

            private class HouesholdAgeEntry
            {
                internal int[] AgeCategoryCount;
                internal float ExpandedTotal;
                internal int NumberOfCars;
                internal int PlanningDistrict;

                internal bool Equals(int[] ageCategoryCount, int planningDistrict, int numberOfCars)
                {
                    if ( PlanningDistrict != planningDistrict ) return false;
                    if ( NumberOfCars != numberOfCars ) return false;
                    for ( int i = 0; i < AgeCategoryCount.Length; i++ )
                    {
                        if ( AgeCategoryCount[i] != ageCategoryCount[i] )
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }
        }
    }
}