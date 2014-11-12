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
using System.IO;
using System.Linq;
using Datastructure;
using TMG;

namespace Tasha.Scheduler
{
    /// <summary>
    /// This class provides access to the activity
    /// distributions
    /// </summary>
    internal static class ActivityDistribution
    {
        /// <summary>
        /// Our link to the information for activities for each zone
        /// </summary>
        [ThreadStatic]
        private static ZoneCache<ActivityInformation> ActivityData;

        internal static float GetDistribution(IZone zone, int type)
        {
            switch ( type )
            {
                case 0:
                    return ActivityData[zone.ZoneNumber].RetailActivityLevel;

                case 1:
                    return ActivityData[zone.ZoneNumber].OtherActivityLevel;

                default:
                    return ActivityData[zone.ZoneNumber].WorkActivityLevel;
            }
        }

        /// <summary>
        /// Called once by the Scheduler, loads the ability to read the distributions
        /// </summary>
        internal static void LoadDistributions(string fileName, SparseArray<IZone> zoneArray)
        {
            if ( ActivityData == null )
            {
                if ( !File.Exists( fileName ) )
                {
                    GenerateActivityLevels( fileName, zoneArray );
                }
                ActivityData = new ZoneCache<ActivityInformation>( fileName, convert );

                //(TashaConfiguration.GetInputFile(TashaConfiguration.GetDirectory("Scheduler"),"ActivityLevels"),

                //  convert);
            }
        }

        private static ActivityInformation convert(int zone, float[] data)
        {
            ActivityInformation info;
            info.RetailActivityLevel = data[0];
            info.OtherActivityLevel = data[1];
            info.WorkActivityLevel = data[2];
            return info;
        }

        private static void CreateActivityData(string FileName)
        {
            if ( ActivityData == null )
            {
                ActivityData = new ZoneCache<ActivityInformation>( FileName,
                delegate(int zone, float[] data)
                {
                    ActivityInformation info;
                    info.RetailActivityLevel = data[0];
                    info.OtherActivityLevel = data[1];
                    info.WorkActivityLevel = data[2];
                    return info;
                } );
            }
        }

        private static void GenerateActivityLevels(string fileName, SparseArray<IZone> zoneArray)
        {
            var zones = zoneArray.GetFlatData();
            string csvFileName = Path.GetTempFileName();
            using ( StreamWriter writer = new StreamWriter( csvFileName ) )
            {
                writer.WriteLine( "Zone,Retail Level,Other Level,Work Level" );
                for ( int i = 0; i < zones.Length; i++ )
                {
                    writer.Write( zones[i].ZoneNumber );
                    writer.Write( ',' );
                    writer.Write( zones[i].RetailActivityLevel );
                    writer.Write( ',' );
                    writer.Write( zones[i].OtherActivityLevel );
                    writer.Write( ',' );
                    writer.WriteLine( zones[i].WorkActivityLevel );
                }
            }
            SparseZoneCreator creator = new SparseZoneCreator( zones.Last().ZoneNumber + 1, 3 );
            creator.LoadCSV( csvFileName, true );
            creator.Save( fileName );
            File.Delete( csvFileName );
        }

        /// <summary>
        /// How we actually store the information
        /// </summary>
        private struct ActivityInformation
        {
            internal float OtherActivityLevel;
            internal float RetailActivityLevel;
            internal float WorkActivityLevel;
        }
    }
}