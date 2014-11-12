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
using System.IO;
using Tasha.Common;
using TMG;
using XTMF;
using Datastructure;
using Tasha.Scheduler;

namespace Beijing
{
    public class BeijingLocationChoice : ILocationChoiceModel
    {
        [RootModule]
        public ITashaRuntime Root;

        private SparseArray<IZone> ZoneArray;

        private int[] ValidZones;

        [RunParameter("Random Seed", 41232, "The number that will be used as a basis for generating random numbers" )]
        public int RandomSeed;

        [RunParameter("Work Table", "LocationChoice/Work.csv", "The probability table for work." )]
        public string WorkFile;

        [RunParameter("Shopping Table", "LocationChoice/Shopping.csv", "The probability table for shopping." )]
        public string ShoppingFile;

        [RunParameter("Other Table", "LocationChoice/Other.csv", "The probability table for shopping.")]
        public string OtherFile;

        [ThreadStatic]
        private static Random Random;

        float[,] ShoppingProbabilityTable;
        float[,] OtherProbabilityTable;
        float[,] WorkProbabilityTable;

        public void LoadLocationChoiceCache()
        {
            ZoneArray = Root.ZoneSystem.ZoneArray;
            ValidZones = ZoneArray.ValidIndexies().ToArray();
            LoadProbabilityTables();
        }

        private void LoadProbabilityTables()
        {
            var numZones = ValidZones.Length;
            // Load the work probabilities
            LoadProbabilityTable( FailIfNotExist( WorkFile ), ( WorkProbabilityTable = new float[numZones, numZones] ) );
            // Load the shopping probabilities
            LoadProbabilityTable( FailIfNotExist( ShoppingFile ), ( ShoppingProbabilityTable = new float[numZones, numZones] ) );
            // Load the other probabilities
            LoadProbabilityTable( FailIfNotExist( OtherFile ), ( OtherProbabilityTable = new float[numZones, numZones] ) );
        }

        private void LoadProbabilityTable(string fileName, float[,] table)
        {
            using (CsvReader reader = new CsvReader( fileName ))
            {
                var numberOfColumns = reader.LoadLine();
                int[] dIndex = new int[numberOfColumns - 1];
                for ( int i = 0; i < numberOfColumns - 1; i++ )
                {
                    reader.Get( out dIndex[i], i + 1 );
                    dIndex[i] = ZoneArray.GetFlatIndex( dIndex[i] );
                }
                // read in the headers
                while ( !reader.EndOfFile )
                {
                    numberOfColumns = reader.LoadLine();
                    if ( numberOfColumns == 0 )
                    {
                        // counter balance a blank line in the middle
                        continue;
                    }
                    int o;
                    reader.Get( out o, 0 );
                    o = ZoneArray.GetFlatIndex( o );
                    for ( int i = 0; i < numberOfColumns - 1; i++ )
                    {
                        reader.Get( out table[o, dIndex[i]], i + 1 );
                    }
                }
            }
        }

        private string FailIfNotExist(string localPath)
        {
            var path = GetFullPath( localPath );
            try
            {
                if ( !File.Exists( path ) )
                {
                    throw new XTMFRuntimeException( "The file \"" + path + "\" does not exist!" );
                }
            }
            catch (IOException)
            {
                throw new XTMFRuntimeException( "An error occurred wile looking for the file \"" + path + "\"!" );
            }
            return path;
        }

        private string GetFullPath(string localPath)
        {
            var fullPath = localPath;
            if ( !Path.IsPathRooted( fullPath ) )
            {
                fullPath = Path.Combine( Root.InputBaseDirectory, fullPath );
            }
            return fullPath;
        }

        public IZone GetLocationWorkBased(IZone primaryWorkZone, ITashaPerson person, Random random)
        {
            return GetZone( WorkProbabilityTable, primaryWorkZone );
        }

        public IZone GetLocationHomeBased(IEpisode episode, ITashaPerson person, Random random)
        {
            return GetLocationHomeBased( episode.ActivityType, person.Household.HomeZone, random );
        }

        public IZone GetLocationHomeBased(Activity activity, IZone zone, Random random)
        {
            switch ( activity )
            {
                case Activity.PrimaryWork:
                case Activity.SecondaryWork:
                case Activity.WorkAtHomeBusiness:
                case Activity.WorkBasedBusiness:
                    {
                        return GetZone( WorkProbabilityTable, zone );
                    }
                case Activity.Market:
                case Activity.JointMarket:
                    {
                        return GetZone( ShoppingProbabilityTable, zone );
                    }
                case Activity.JointOther:
                case Activity.IndividualOther:
                    {
                        return GetZone( OtherProbabilityTable, zone );
                    }
                default:
                    return null;
            }
        }

        /// <summary>
        /// Using a probability table get the zone to use
        /// </summary>
        /// <param name="probabilityTable">The table of probabilities</param>
        /// <param name="origin">Which row to select from the probability table</param>
        /// <returns>A destination zone to use</returns>
        private IZone GetZone(float[,] probabilityTable, IZone origin)
        {
            if ( Random == null )
            {
                Random = new Random( RandomSeed );
            }
            var rand = Random.NextDouble();
            double total = 0;
            int oIndex = ZoneArray.GetFlatIndex( origin.ZoneNumber );
            for ( int i = 0; i < ValidZones.Length; i++ )
            {
                total += probabilityTable[oIndex, i];
                if ( total >= rand )
                {
                    return ZoneArray[ValidZones[i]];
                }
            }
            return ZoneArray[ValidZones[ValidZones.Length - 1]];
        }

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
            get { return new Tuple<byte, byte, byte>( 50, 100, 50 ); }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public IZone GetLocation(IEpisode ep, Random random)
        {
            switch ( ep.ActivityType )
            {
                case Activity.Market:
                case Activity.JointMarket:
                case Activity.IndividualOther:
                case Activity.JointOther:
                case Activity.WorkAtHomeBusiness:
                    return GetLocationHomeBased( ep.ActivityType, ep.Owner.Household.HomeZone, random );
                case Activity.WorkBasedBusiness:
                    return GetLocationWorkBased( ep.Owner.EmploymentZone, ep.Owner, random );
                default:
                    return ep.Zone;
            }
        }
    }
}
