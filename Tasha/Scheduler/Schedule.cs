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
            this.Episodes = new Episode[20];
            this.EpisodeCount = 0;
        }

        internal int NumberOfEpisodes
        {
            get
            {
                return this.Episodes == null ? 0 : this.EpisodeCount;
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
            for ( int i = 0; i < this.EpisodeCount; i++ )
            {
                this.Episodes[i] = null;
            }
            this.EpisodeCount = 0;
        }

        /// <summary>
        /// Forces an episode to be inserted
        /// </summary>
        public abstract bool ForcedEpisodeInsert(Episode ep);

        public abstract void GenerateTrips(ITashaHousehold household, int householdIterations, Time minimumAtHomeTime);

        public Time GetFirstEpisodeStartTime()
        {
            if ( this.EpisodeCount == 0 || Episodes[0] == null )
            {
                return Time.Zero;
            }
            return this.Episodes[0].StartTime;
        }

        public Time GetLastEpisodeEndTime()
        {
            if ( this.EpisodeCount == 0 )
            {
                return Time.Zero;
            }
            return this.Episodes[this.EpisodeCount - 1].EndTime;
        }

        public abstract bool Insert(Episode ep, Random random);

        /// <summary>
        /// Add a whole Schedule to this schedule
        /// </summary>
        /// <param name="schedule">The schedule you wish to add</param>
        public void Insert(Schedule schedule, Random random)
        {
            for ( int i = 0; i < schedule.EpisodeCount; i++ )
            {
                if ( schedule.Episodes[i].ActivityType != Activity.NullActivity )
                {
                    var episode = (Episode)schedule.Episodes[i];
                    // take ownership of this episode
                    episode.ContainingSchedule = this;
                    this.Insert( episode, random );
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
            if ( ( pos < 0 ) | ( pos > this.EpisodeCount ) )
            {
                throw new XTMFRuntimeException( "Tried to insert into an schedule at position " + pos
                    + " where there are currently " + this.EpisodeCount + " episodes." );
            }

            if ( this.EpisodeCount + 1 >= this.Episodes.Length )
            {
                // if we are assigning to the end, but it isn't large enough, expand
                IncreaseArraySize();
            }

            if ( pos != this.EpisodeCount )
            {
                Array.Copy( this.Episodes, pos, this.Episodes, pos + 1, this.EpisodeCount - pos );
            }
            // take ownership of the episode
            ep.ContainingSchedule = this;
            this.Episodes[pos] = ep;
            this.EpisodeCount++;

            CheckEpisodeIntegrity();
        }

        public ConflictReport InsertCase(ITashaPerson owner, Episode ep, bool travelTime)
        {
            ConflictReport report;
            report.Type = ScheduleConflictType.NoConflict;
            report.Position = this.EpisodeCount;

            for ( int i = 0; i < this.EpisodeCount; i++ )
            {
                if ( this.Episodes[i].EndTime + this.Episodes[i].TravelTime < ep.StartTime ) continue;
                Time epEnd = ep.EndTime;
                Time ithEnd = this.Episodes[i].EndTime;
                if ( travelTime )
                {
                    ep.TravelTime = ( this.EpisodeCount - 1 > i ) ?
                        Scheduler.TravelTime( owner, ep.Zone, this.Episodes[i + 1].Zone, ep.EndTime ) : Time.Zero;
                    epEnd += ep.TravelTime;
                    ithEnd += this.Episodes[i].TravelTime;
                }
                report.Position = i;
                // Check for Complete overlap of the ith position
                if ( this.Episodes[i].StartTime >= ep.StartTime && ( epEnd >= ithEnd || ep.EndTime >= this.Episodes[i].EndTime ) )
                {
                    report.Type = ScheduleConflictType.CompleteOverlap;
                }
                else if ( this.EpisodeCount - 1 > i && this.Episodes[i + 1].StartTime >= ep.StartTime && epEnd >= this.Episodes[i + 1].EndTime )
                {
                    report.Type = ScheduleConflictType.CompleteOverlap;
                }
                else if ( this.Episodes[i].StartTime < ep.StartTime && ep.EndTime < this.Episodes[i].EndTime )
                {
                    report.Type = ScheduleConflictType.Split;
                }
                else if ( this.Episodes[i].StartTime >= ep.StartTime && ep.EndTime < this.Episodes[i].EndTime )
                {
                    report.Type = ScheduleConflictType.Prior;
                }
                else if ( this.Episodes[i].StartTime < ep.StartTime && ep.EndTime >= this.Episodes[i].EndTime )
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
            for ( int i = 0; i < this.EpisodeCount; i++ )
            {
                if ( lastEnd > this.Episodes[i].StartTime )
                {
                    throw new XTMFRuntimeException( Dump( this ) );
                }
                else if ( this.Episodes[i].StartTime > mustEndBefore | this.Episodes[i].EndTime > mustEndBefore )
                {
                    throw new XTMFRuntimeException( Dump( this ) );
                }
                else if ( this.Episodes[i].EndTime < this.Episodes[i].StartTime )
                {
                    if ( this.Episodes[i].Owner != null )
                    {
                        throw new XTMFRuntimeException( "There is an episode that ends before it starts in household #" + this.Episodes[i].Owner.Household.HouseholdId );
                    }
                    else
                    {
                        throw new XTMFRuntimeException( "There is an episode that ends before it starts and has no owner!"
                            + "\r\nActivity Type    :" + this.Episodes[i].ActivityType.ToString()
                            + "\r\nStart Time       :" + this.Episodes[i].StartTime
                            + "\r\nDuration         :" + this.Episodes[i].Duration
                            + "\r\nOriginal Duration:" + this.Episodes[i].OriginalDuration
                            + "\r\nEnd Time         :" + this.Episodes[i].EndTime );
                    }
                }
                else if ( this.Episodes[i].OriginalDuration < Tasha.Scheduler.Scheduler.PercentOverlapAllowed * this.Episodes[i].Duration )
                {
                    throw new XTMFRuntimeException( "Episode is smaller than the allowed overlap!" );
                }
                lastEnd = this.Episodes[i].EndTime;
            }
        }

        protected static string Dump(Schedule sched)
        {
            StringBuilder builder = new StringBuilder();
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
            builder.Append( e.ActivityType.ToString() );
            builder.Append( ", Start -> " );
            builder.Append( e.StartTime.ToString() );
            builder.Append( ", End -> " );
            builder.Append( e.EndTime.ToString() );
            builder.Append( ", TT -> " );
            builder.Append( e.TravelTime.ToString() );
        }

        private void IncreaseArraySize()
        {
            // if we don't have room create a new array of 2x the size
            var temp = new Episode[this.EpisodeCount * 2];
            // copy all of the old data
            Array.Copy( this.Episodes, temp, this.EpisodeCount );
            // and now use that larger array
            this.Episodes = temp;
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