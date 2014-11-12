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
using System.IO;
using Tasha.Common;
using Tasha.XTMFModeChoice;
using XTMF;

namespace Tasha.Validation.ValidateModeChoice
{
    public class PrintingConflicts : IPostHouseholdIteration
    {
        [RunParameter( "Output File", "NumberOfConflicts.csv", "The file where we can store the household utilities." )]
        public string OutputFile;

        private ConcurrentDictionary<int, int[]> Conflicts = new ConcurrentDictionary<int, int[]>();

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get;
            set;
        }

        public void HouseholdComplete(ITashaHousehold household, bool success)
        {
            if ( success )
            {
                lock ( this )
                {
                    var writeHeader = !File.Exists( OutputFile );
                    using ( StreamWriter writer = new StreamWriter( OutputFile, true ) )
                    {
                        if ( writeHeader )
                        {
                            writer.WriteLine( "HouseholdID, Iteration, Number of Conflicts" );
                        }

                        var householdConflicts = Conflicts[household.HouseholdId];
                        for ( int i = 0; i < householdConflicts.Length; i++ )
                        {
                            writer.Write( household.HouseholdId );
                            writer.Write( ',' );
                            writer.Write( i );
                            writer.Write( ',' );
                            writer.WriteLine( householdConflicts[i] );
                        }
                    }
                }
            }
        }

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {
            var resource = household["ResourceAllocator"] as HouseholdResourceAllocator;
            if ( resource.Conflicts != null )
            {
                var iterationConflicts = resource.Conflicts.Count;
                Conflicts[household.HouseholdId][hhldIteration] = iterationConflicts;
            }
            else
            {
                Conflicts[household.HouseholdId][hhldIteration] = 0;
            }
        }

        public void HouseholdStart(ITashaHousehold household, int householdIterations)
        {
            Conflicts.TryAdd( household.HouseholdId, new int[householdIterations] );
        }

        public void IterationFinished(int iteration, int totalIterations)
        {
            
        }

        public void IterationStarting(int iteration, int totalIterations)
        {
            
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}