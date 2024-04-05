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
using System.Text;
using Tasha.Common;
using XTMF;

namespace Tasha.Scheduler
{
    public abstract class Schedule : ISchedule
    {
        public static ITashaScheduler Scheduler;

        public int EpisodeCount;

        public IEpisode[] Episodes { get; private set; }

        public Schedule()
        {
            Episodes = new IEpisode[20];
            EpisodeCount = 0;
        }

        internal int NumberOfEpisodes
        {
            get
            {
                return Episodes == null ? 0 : EpisodeCount;
            }
        }

        public void AddHouseholdProjects(ITashaHousehold household)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Checks to see if an episode can be inserted, and does if it can
        /// </summary>
        public abstract bool CheckEpisodeInsert(IEpisode episode, ref TimeWindow feasibleWindow);

        /// <summary>
        /// Checks to see if an episode can be inserted
        /// </summary>
        public abstract Time CheckOverlap(Episode episode);

        public abstract int CleanUp(Time minPrimaryWorkLength);

        public void Clear()
        {
            for ( int i = 0; i < EpisodeCount; i++ )
            {
                Episodes[i] = null;
            }
            EpisodeCount = 0;
        }

        /// <summary>
        /// Forces an episode to be inserted
        /// </summary>
        public abstract bool ForcedEpisodeInsert(Episode ep);

        public abstract void GenerateTrips(ITashaHousehold household, int householdIterations, Time minimumAtHomeTime);

        public Time GetFirstEpisodeStartTime()
        {
            if ( EpisodeCount == 0 || Episodes[0] == null )
            {
                return Time.Zero;
            }
            return Episodes[0].StartTime;
        }

        public Time GetLastEpisodeEndTime()
        {
            if ( EpisodeCount == 0 )
            {
                return Time.Zero;
            }
            return Episodes[EpisodeCount - 1].EndTime;
        }

        public abstract bool Insert(Episode ep, Random random);

        /// <summary>
        /// Add a whole Schedule to this schedule
        /// </summary>
        /// <param name="schedule">The schedule you wish to add</param>
        /// <param name="random"></param>
        public void Insert(Schedule schedule, Random random)
        {
            for ( int i = 0; i < schedule.EpisodeCount; i++ )
            {
                if ( schedule.Episodes[i].ActivityType != Activity.NullActivity )
                {
                    var episode = (Episode)schedule.Episodes[i];
                    // take ownership of this episode
                    episode.ContainingSchedule = this;
                    Insert( episode, random );
                }
            }
        }

        /// <summary>
        /// Insert into our array data structure
        /// </summary>
        /// <param name="ep">The episode you want to insert</param>
        /// <param name="pos">The position you want to insert it into</param>
        public void InsertAt(Episode ep, int pos)
        {
            // if we are not adding it to the end
            if ( ( pos < 0 ) | ( pos > EpisodeCount ) )
            {
                throw new XTMFRuntimeException(Scheduler, "Tried to insert into an schedule at position " + pos
                    + " where there are currently " + EpisodeCount + " episodes." );
            }

            if ( EpisodeCount + 1 >= Episodes.Length )
            {
                // if we are assigning to the end, but it isn't large enough, expand
                IncreaseArraySize();
            }

            if ( pos != EpisodeCount )
            {
                Array.Copy( Episodes, pos, Episodes, pos + 1, EpisodeCount - pos );
            }
            // take ownership of the episode
            ep.ContainingSchedule = this;
            Episodes[pos] = ep;
            EpisodeCount++;

            CheckEpisodeIntegrity();
        }

        public ConflictReport InsertCase(ITashaPerson owner, Episode ep, bool travelTime)
        {
            ConflictReport report;
            report.Type = ScheduleConflictType.NoConflict;
            report.Position = EpisodeCount;

            for ( int i = 0; i < EpisodeCount; i++ )
            {
                if ( Episodes[i].EndTime + Episodes[i].TravelTime < ep.StartTime ) continue;
                Time epEnd = ep.EndTime;
                Time ithEnd = Episodes[i].EndTime;
                if ( travelTime )
                {
                    ep.TravelTime = ( EpisodeCount - 1 > i ) ?
                        Scheduler.TravelTime( owner, ep.Zone, Episodes[i + 1].Zone, ep.EndTime ) : Time.Zero;
                    epEnd += ep.TravelTime;
                    ithEnd += Episodes[i].TravelTime;
                }
                report.Position = i;
                // Check for Complete overlap of the ith position
                if ( Episodes[i].StartTime >= ep.StartTime && ( epEnd >= ithEnd || ep.EndTime >= Episodes[i].EndTime ) )
                {
                    report.Type = ScheduleConflictType.CompleteOverlap;
                }
                else if ( EpisodeCount - 1 > i && Episodes[i + 1].StartTime >= ep.StartTime && epEnd >= Episodes[i + 1].EndTime )
                {
                    report.Type = ScheduleConflictType.CompleteOverlap;
                }
                else if ( Episodes[i].StartTime < ep.StartTime && ep.EndTime < Episodes[i].EndTime )
                {
                    report.Type = ScheduleConflictType.Split;
                }
                else if ( Episodes[i].StartTime >= ep.StartTime && ep.EndTime < Episodes[i].EndTime )
                {
                    report.Type = ScheduleConflictType.Prior;
                }
                else if ( Episodes[i].StartTime < ep.StartTime && ep.EndTime >= Episodes[i].EndTime )
                {
                    report.Type = ScheduleConflictType.Posterior;
                }
                break;
            }
            return report; // There is no conflict
        }

        public abstract bool TestInsert(Episode episode);

        internal void CheckEpisodeIntegrity()
        {
            Time lastEnd = Time.Zero;
            Time mustEndBefore = Time.EndOfDay + new Time() { Minutes = 5 };
            for ( int i = 0; i < EpisodeCount; i++ )
            {
                if ( lastEnd > Episodes[i].StartTime )
                {
                    throw new XTMFRuntimeException(Scheduler, Dump( this ) );
                }
                if ( Episodes[i].StartTime > mustEndBefore | Episodes[i].EndTime > mustEndBefore )
                {
                    throw new XTMFRuntimeException(Scheduler, Dump( this ) );
                }
                if ( Episodes[i].EndTime < Episodes[i].StartTime )
                {
                    if ( Episodes[i].Owner != null )
                    {
                        throw new XTMFRuntimeException(Scheduler, "There is an episode that ends before it starts in household #" + Episodes[i].Owner.Household.HouseholdId );
                    }
                    throw new XTMFRuntimeException(Scheduler, "There is an episode that ends before it starts and has no owner!"
                                                    + "\r\nActivity Type    :" + Episodes[i].ActivityType
                                                    + "\r\nStart Time       :" + Episodes[i].StartTime
                                                    + "\r\nDuration         :" + Episodes[i].Duration
                                                    + "\r\nOriginal Duration:" + Episodes[i].OriginalDuration
                                                    + "\r\nEnd Time         :" + Episodes[i].EndTime );
                }
                if ( Episodes[i].OriginalDuration < Tasha.Scheduler.Scheduler.PercentOverlapAllowed * Episodes[i].Duration )
                {
                    throw new XTMFRuntimeException(Scheduler, "Episode is smaller than the allowed overlap!"
                                                    + "\r\nActivity Type    :" + Episodes[i].ActivityType
                                                    + "\r\nStart Time       :" + Episodes[i].StartTime
                                                    + "\r\nDuration         :" + Episodes[i].Duration
                                                    + "\r\nOriginal Duration:" + Episodes[i].OriginalDuration
                                                    + "\r\nEnd Time         :" + Episodes[i].EndTime);
                }
                lastEnd = Episodes[i].EndTime;
            }
        }

        protected static string Dump(Schedule sched)
        {
            StringBuilder builder = new();
            bool first = true;
            for ( int i = 0; i < sched.Episodes.Length; i++ )
            {
                if ( sched.Episodes[i] == null ) continue;
                if ( !first )
                {
                    builder.AppendLine();
                }
                StoreEpisode( (Episode)sched.Episodes[i], builder );
                first = false;
            }
            return builder.ToString();
        }

        private static void StoreEpisode(Episode e, StringBuilder builder)
        {
            builder.Append( "Activity -> " );
            builder.Append( e.ActivityType );
            builder.Append( ", Start -> " );
            builder.Append( e.StartTime );
            builder.Append( ", End -> " );
            builder.Append( e.EndTime );
            builder.Append( ", TT -> " );
            builder.Append( e.TravelTime );
        }

        private void IncreaseArraySize()
        {
            // if we don't have room create a new array of 2x the size
            IEpisode[] temp = new IEpisode[EpisodeCount * 2];
            // copy all of the old data
            Array.Copy( Episodes, temp, EpisodeCount );
            // and now use that larger array
            Episodes = temp;
        }

        public IActivityEpisode[] GenerateScheduledEpisodeList()
        {
            throw new NotImplementedException("Available in the Tasha2 Scheduler");
        }

        public void Insert(Random householdRandom, IActivityEpisode episode)
        {
            throw new NotImplementedException( "Available in the Tasha2 Scheduler" );
        }

        public void InsertInside(Random householdRandom, IActivityEpisode episode, IActivityEpisode into)
        {
            throw new NotImplementedException( "Available in the Tasha2 Scheduler" );
        }
    }
}