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
using System.Linq;
using Datastructure;
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Scheduler
{
    public class LocationChoiceModel : ILocationChoiceModel
    {
        [RunParameter("LocationChoiceModelHomeCache", "LocationChoiceModelHomeCache.zfc", "The location of the home cache.")]
        public string LocationChoiceModelHomeCache;

        [RunParameter("LocationChoiceModelWorkCache", "LocationChoiceModelWorkCache.zfc", "The location of the work cache.")]
        public string LocationChoiceModelWorkCache;

        [RootModule]
        public ITashaRuntime TashaRuntime;

        internal static ZoneCache<LocationChoiceInformation> LocationChoiceData;

        internal static ZoneCache<LocationChoiceInformation> LocationChoiceHomeData;

        private static int highestZone;

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
        ///
        /// </summary>
        /// <param name="e">The episode to find a zone for</param>
        /// <param name="person">The person to look at (their household)</param>
        /// <returns></returns>
        public IZone GetLocationHomeBased(IEpisode e, ITashaPerson person, Random random)
        {
            return GetLocationHomeBased( e.ActivityType, person.Household.HomeZone, random );
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="a">The type of activity to find a zone for</param>
        /// <param name="zone">The "source" zone to base distances etc. off of</param>
        /// <returns></returns>
        public IZone GetLocationHomeBased(Activity a, IZone zone, Random random)
        {
            int offset = 0;

            switch ( a )
            {
                case Activity.WorkAtHomeBusiness:
                    offset = 0;
                    break;

                case Activity.WorkBasedBusiness:
                    offset = 1;
                    break;

                case Activity.IndividualOther:
                case Activity.JointOther:
                    offset = 2;
                    break;

                case Activity.Market:
                case Activity.JointMarket:
                    offset = 3;
                    break;
            }
            if ( zone == null )
            {
                throw new XTMFRuntimeException( "Zone is NULL!" );
            }
            //obtain a value between 0 and 1
            double rand_num = random.NextDouble();
            var lcd = LocationChoiceHomeData[zone.ZoneNumber];
            var zones = TashaRuntime.ZoneSystem.ZoneArray.GetFlatData();
            for ( int i = 0; i < zones.Length; i++ )
            {
                if ( lcd.Data[i][offset] >= rand_num )
                {
                    return zones[i];
                }
            }
            return zone;
        }

        public IZone GetLocationWorkBased(IZone zone, ITashaPerson person, Random random)
        {
            int occupationIndex = 0;

            switch ( person.Occupation )
            {
                case Occupation.Office:
                    occupationIndex = 0;
                    break;

                case Occupation.Manufacturing:
                    occupationIndex = 1;
                    break;

                case Occupation.Retail:
                    occupationIndex = 2;
                    break;

                case Occupation.Professional:
                    occupationIndex = 3;
                    break;
            }

            //obtain a value between 0 and 1
            double rand_num = random.NextDouble();
            var lcd = LocationChoiceData[zone.ZoneNumber];
            var zones = TashaRuntime.ZoneSystem.ZoneArray.GetFlatData();
            for ( int i = 0; i < zones.Length; i++ )
            {
                if ( lcd.Data[i][occupationIndex] >= rand_num )
                {
                    return zones[i];
                }
            }
            return person.EmploymentZone;
        }

        public void LoadLocationChoiceCache()
        {
            highestZone = TashaRuntime.ZoneSystem.ZoneArray.GetFlatData().Last().ZoneNumber;

            if ( LocationChoiceData == null )
            {
                LocationChoiceData =
                    new ZoneCache<LocationChoiceInformation>( GetFullPath( LocationChoiceModelWorkCache ), DataToLCI );
                LocationChoiceHomeData =
                    new ZoneCache<LocationChoiceInformation>( GetFullPath( LocationChoiceModelHomeCache ), DataToLCI );
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
            return true;
        }

        private LocationChoiceInformation DataToLCI(int n, float[] data)
        {
            LocationChoiceInformation lci = new LocationChoiceInformation();
            //data.length should be equal to number of internal zones, 4 is 4 possible
            //parameter calculations
            //and the 2 is for the SUM value / 7
            //ignore "1" value because data[0] is just the zone nUmber we are looking at
            lci.Data = new float[data.Length / 4][];
            for ( int i = 0; i < lci.Data.Length; i++ )
            {
                lci.Data[i] = new float[4];
            }
            int sectionLength = ( data.Length / 4 );
            for ( int i = 0; i < 4; i++ )
            {
                for ( int j = 0; j < sectionLength; j++ )
                {
                    lci.Data[j][i] = data[( i * sectionLength ) + j];
                }
            }
            return lci;
        }

        private string GetFullPath(string localPath)
        {
            var fullPath = localPath;
            if ( !System.IO.Path.IsPathRooted( fullPath ) )
            {
                fullPath = System.IO.Path.Combine( TashaRuntime.InputBaseDirectory, fullPath );
            }
            return fullPath;
        }

        public IZone GetLocation(IEpisode ep, Random random)
        {
            switch ( ep.ActivityType )
            {
                case Activity.Market:
                case Activity.JointMarket:
                case Activity.IndividualOther:
                case Activity.JointOther:
                    return GetLocationHomeBased( ep.ActivityType, ep.Owner.Household.HomeZone, random );
                case Activity.WorkBasedBusiness:
                    {
                        var empZone = ep.Owner.EmploymentZone;
                        if ( empZone.ZoneNumber == TashaRuntime.ZoneSystem.RoamingZoneNumber )
                        {
                            return GetLocationHomeBased( Activity.WorkBasedBusiness, ep.Owner.Household.HomeZone, random );
                        }
                        else
                        {
                            return GetLocationWorkBased( empZone, ep.Owner, random );
                        }
                    }
                case Activity.PrimaryWork:
                case Activity.SecondaryWork:
                case Activity.WorkAtHomeBusiness:
                    return ( ep.Zone == null || ep.Zone.ZoneNumber == TashaRuntime.ZoneSystem.RoamingZoneNumber ) ? GetLocationHomeBased( ep, ep.Owner, random ) : ep.Zone;
                default:
                    return ep.Zone;
            }
        }

        public float[] GetLocationProbabilities(IEpisode ep)
        {
            throw new NotImplementedException();
        }

        internal class LocationChoiceInformation
        {
            public float[][] Data;
        }
    }
}