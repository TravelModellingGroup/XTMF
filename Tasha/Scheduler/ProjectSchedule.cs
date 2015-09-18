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
using Tasha.Common;
using XTMF;

namespace Tasha.Scheduler
{
    internal sealed class ProjectSchedule : Schedule
    {
        /// <summary>
        /// The household this project schedule is for
        /// </summary>
        private ITashaHousehold Household;

        public ProjectSchedule(ITashaHousehold household)
        {
            this.Household = household;
        }

        internal Project Project
        {
            get;
            set;
        }

        public override bool CheckEpisodeInsert(IEpisode episode, ref TimeWindow feasibleWindow)
        {
            throw new NotImplementedException();
        }

        public override Time CheckOverlap(Episode episode)
        {
            Time total = Time.Zero;
            for(int i = 0; i < this.EpisodeCount; i++)
            {
                if(this.Episodes[i].StartTime <= episode.StartTime
                    && this.Episodes[i].EndTime >= episode.EndTime)
                {
                    // this [i] completely covers the given episode
                    total += episode.Duration;
                }
                else if(this.Episodes[i].StartTime >= episode.StartTime
                    && this.Episodes[i].EndTime >= episode.EndTime)
                {
                    // if the episode happens before we start, but we end after
                    total += episode.EndTime - this.Episodes[i].StartTime;
                }
                else if(this.Episodes[i].StartTime <= episode.StartTime
                    && this.Episodes[i].EndTime <= episode.EndTime)
                {
                    //if we started before this episode, but we finished first
                    total += this.Episodes[i].EndTime - episode.StartTime;
                }
                else if(this.Episodes[i].StartTime >= episode.StartTime
                    && this.Episodes[i].EndTime <= episode.EndTime)
                {
                    // if the episode is larger and 100% covering this [i]
                    total += this.Episodes[i].Duration;
                }
            }
            return total;
        }

        public override int CleanUp(Time minPrimaryWorkLength)
        {
            throw new NotImplementedException();
        }

        public override bool ForcedEpisodeInsert(Episode ep)
        {
            throw new NotImplementedException();
        }

        public override void GenerateTrips(ITashaHousehold household, int householdIteration, Time minimumAtHomeTime)
        {
            throw new NotImplementedException();
        }

        public override bool Insert(Episode ep, Random random)
        {
            /* This is where episodes are first put near their other common types
             * RULES:
             * 1) Unless it is a Work Business episode, we are not allowed to have a "split" conflict type
             * 2) We are not allowed to squish things past the threshold allowed (50% by default)
            */
            // Learn what type of case we are going to be in
            ConflictReport conflict = this.InsertCase(null, ep, false);
            switch(conflict.Type)
            {
                case ScheduleConflictType.NoConflict:
                    {
                        this.InsertAt(ep, conflict.Position);
                        return true;
                    }
                case ScheduleConflictType.Split:
                    {
                        if((ep.ActivityType != Activity.WorkBasedBusiness) & (ep.ActivityType != Activity.ReturnFromWork)) return false;
                        if(this.Episodes[conflict.Position].ActivityType != Activity.PrimaryWork) return false;
                        // Since it is a primary work episode we need to split it
                        var postEp = new ActivityEpisode(0, new TimeWindow(ep.EndTime, this.Episodes[conflict.Position].EndTime), Activity.PrimaryWork,
                             this.Episodes[conflict.Position].Owner);
                        postEp.Zone = this.Episodes[conflict.Position].Zone;
                        ((Episode)this.Episodes[conflict.Position]).EndTime = ep.StartTime;
                        this.InsertAt(ep, conflict.Position + 1);
                        this.InsertAt(postEp, conflict.Position + 2);
                        return true;
                    }
                case ScheduleConflictType.Posterior:
                    {
                        // the given position is the element we need to go after
                        Time earlyTimeBound = Time.StartOfDay;
                        Time lateTimeBound = Time.EndOfDay;
                        Episode prior = (Episode)this.Episodes[conflict.Position];
                        Episode middle = ep;
                        Episode post = (Episode)((conflict.Position < this.EpisodeCount - 1)
                            ? this.Episodes[conflict.Position + 1] : null);
                        if(conflict.Position >= 1)
                        {
                            earlyTimeBound = this.Episodes[conflict.Position - 1].EndTime;
                        }
                        if(this.EpisodeCount - conflict.Position > 2)
                        {
                            lateTimeBound = this.Episodes[conflict.Position + 2].StartTime;
                        }
                        if(this.Insert(earlyTimeBound, prior, middle, post, lateTimeBound))
                        {
                            this.InsertAt(ep, conflict.Position + 1);
                            return true;
                        }
                        return false;
                    }
                case ScheduleConflictType.Prior:
                    {
                        // The given position is the element we need to go before
                        Time earlyTimeBound = Time.StartOfDay;
                        Time lateTimeBound = Time.EndOfDay;
                        Episode prior = (Episode)(conflict.Position > 0 ? this.Episodes[conflict.Position - 1] : null);
                        Episode middle = ep;
                        Episode post = (Episode)this.Episodes[conflict.Position];
                        if(conflict.Position >= 2)
                        {
                            earlyTimeBound = this.Episodes[conflict.Position - 2].EndTime;
                        }
                        if(this.EpisodeCount - conflict.Position > 1)
                        {
                            lateTimeBound = this.Episodes[conflict.Position + 1].StartTime;
                        }
                        if(this.Insert(earlyTimeBound, prior, middle, post, lateTimeBound))
                        {
                            this.InsertAt(ep, conflict.Position);
                            return true;
                        }
                        return false;
                    }
                case ScheduleConflictType.CompleteOverlap:
                    {
                        // There are no cases where a complete overlap is allowed
                        return false;
                    }
                default:
                    {
                        // We came across a type of conflict that we do not know how to handle!
                        throw new NotImplementedException(String.Format("This conflict type \"{0}\" has not been coded for yet!",
                            Enum.GetName(typeof(ScheduleConflictType), conflict.Type)));
                    }
            }
        }

        public override bool TestInsert(Episode episode)
        {
            throw new NotImplementedException();
        }

        private static bool FillInGaps(Episode middle, ref Time priorOverlap, ref Time postOverlap)
        {
            if(priorOverlap <= Time.Zero)
            {
                // check to see if there is enough time to move the middle closer to the prior
                if(postOverlap <= -priorOverlap)
                {
                    Relocate(middle, (middle.StartTime - postOverlap));
                    return true;
                }
                else
                {
                    // prior overlap < 0, so just add it
                    Relocate(middle, (middle.StartTime + priorOverlap));
                    // subtract out the reduced time
                    postOverlap += priorOverlap;
                }
            }
            if(postOverlap <= Time.Zero)
            {
                // check to see if there is enough time to move the middle closer to the prior
                if(priorOverlap <= -postOverlap)
                {
                    Relocate(middle, (middle.StartTime + priorOverlap));
                    return true;
                }
                else
                {
                    // prior overlap < 0, so subtract it
                    Relocate(middle, (middle.StartTime - postOverlap));
                    // subtract out the reduced time
                    priorOverlap += postOverlap;
                }
            }
            return false;
        }

        private static void FixDurationToInsert(ref Time earlyTimeBound, Episode prior, Episode middle, Episode post, ref Time lateTimeBound)
        {
            // If we get here then we need to calculate the ratios and then assign the start times accordingly
            Time totalOriginalDuration = (prior != null ? prior.OriginalDuration : Time.Zero)
                + middle.OriginalDuration
                + (post != null ? post.OriginalDuration : Time.Zero);
            Time minPrior = (prior != null ? Tasha.Scheduler.Scheduler.PercentOverlapAllowed * prior.OriginalDuration : Time.Zero);
            Time minMid = Tasha.Scheduler.Scheduler.PercentOverlapAllowed * middle.OriginalDuration;
            Time minPost = (post != null ? Tasha.Scheduler.Scheduler.PercentOverlapAllowed * post.OriginalDuration : Time.Zero);
            Time remainder = (lateTimeBound - earlyTimeBound) - (minPrior + minMid + minPost);
            float ratioPrior = 0;
            float ratioMiddle = 0;
            if(prior != null)
            {
                prior.StartTime = earlyTimeBound;
                ratioPrior = prior.OriginalDuration / totalOriginalDuration;
                prior.EndTime = prior.StartTime + minPrior + (ratioPrior * remainder);
                middle.StartTime = prior.EndTime;
            }
            else
            {
                middle.StartTime = earlyTimeBound;
            }
            ratioMiddle = middle.OriginalDuration / totalOriginalDuration;
            middle.EndTime = middle.StartTime + minMid + (ratioMiddle * remainder);
            if(post != null)
            {
                // we do not need to include the ratio calculation for post since we know it needs to end at the end of the allowed time
                // and that it needs to start right after the middle
                post.StartTime = middle.EndTime;
                post.EndTime = lateTimeBound;
            }
        }

        private static bool MiddlePostInsert(ref Time earlyTimeBound, Episode middle, Episode post, ref Time lateTimeBound)
        {
            Time overlap = (middle.EndTime) - post.StartTime;
            if(overlap <= Time.Zero)
            {
                return true;
            }
            // if we can move forward, move forward
            if((middle.StartTime) - earlyTimeBound > overlap)
            {
                Relocate(middle, (middle.StartTime - overlap));
                return true;
            }
            // if that is not enough, move as forward as we can
            Relocate(middle, (earlyTimeBound));
            overlap = (middle.EndTime) - post.StartTime;
            Relocate(post, (post.StartTime + overlap));
            return true;
        }

        private static bool PriorMiddleInsert(ref Time earlyTimeBound, Episode prior, Episode middle, ref Time lateTimeBound)
        {
            Time overlap = (prior.EndTime) - middle.StartTime;
            if(overlap <= Time.Zero)
            {
                return true;
            }
            // if we can move back, move back
            if(lateTimeBound - (middle.EndTime) > overlap)
            {
                Relocate(middle, (prior.EndTime));
                return true;
            }
            // move back as far as we can
            Relocate(middle, (lateTimeBound - middle.Duration));
            // recalculate the overlap
            overlap = (prior.EndTime) - middle.StartTime;
            Relocate(prior, prior.StartTime - overlap);
            return true;
        }

        private static void Relocate(Episode ep, Time startTime)
        {
            var dur = ep.Duration;
            ep.EndTime = (ep.StartTime = startTime) + dur;
        }

        private bool AllThreeInsert(ref Time earlyTimeBound, Episode prior, Episode middle, Episode post, ref Time lateTimeBound)
        {
            Time priorOverlap = (prior.EndTime) - middle.StartTime;
            Time postOverlap = (middle.EndTime) - post.StartTime;
            Time frontGap = prior.StartTime - earlyTimeBound;
            Time backGap = lateTimeBound - (post.EndTime);
            // see if we can just fill in the gaps
            if(FillInGaps(middle, ref priorOverlap, ref postOverlap))
            {
                return true;
            }
            // push the episodes away from the middle with a proportion to what they overlap it with
            if(priorOverlap < Time.Zero) priorOverlap = Time.Zero;
            if(postOverlap < Time.Zero) postOverlap = Time.Zero;
            // make sure that there is actually an overlap
            var overlap = priorOverlap + postOverlap;
            if(overlap == Time.Zero) return true;
            overlap = Time.Zero;
            var newPriorStartTime = prior.StartTime - priorOverlap;
            bool priorFailed = false;
            if(newPriorStartTime < earlyTimeBound)
            {
                overlap = earlyTimeBound - newPriorStartTime;
                newPriorStartTime = earlyTimeBound;
                priorFailed = true;
            }
            var newPostStartTime = post.StartTime + postOverlap;
            if(newPostStartTime + post.Duration > lateTimeBound)
            {
                overlap += (newPostStartTime + post.Duration) - lateTimeBound;
                newPostStartTime = lateTimeBound - post.Duration;
            }
            Relocate(prior, newPriorStartTime);
            Relocate(post, newPostStartTime);
            if(overlap == Time.Zero) return true;
            if(priorFailed)
            {
                Relocate(middle, middle.StartTime + overlap);
                Relocate(post, middle.EndTime);
            }
            else
            {
                Relocate(middle, middle.StartTime - overlap);
                Relocate(prior, middle.StartTime - prior.Duration);
            }
            // Sanity Check
            if(post.EndTime > lateTimeBound)
            {
                throw new XTMFRuntimeException("We ended too late when inserting with 3 into a person schedule!\r\n"
                    + Schedule.Dump(this));
            }
            else if(prior.StartTime < earlyTimeBound)
            {
                throw new XTMFRuntimeException("We started too early when inserting with 3 into a person schedule!\r\n"
                    + Schedule.Dump(this));
            }
            return true;
        }

        /// <summary>
        /// Run a quick check to see if it is at all possible to place all of the 3 episodes in the given time bounds
        /// </summary>
        /// <param name="earlyTimeBound">The earliest point</param>
        /// <param name="prior">the first episode in the batch (possibly null if there is no episode prior)</param>
        /// <param name="middle">the second episode in the batch</param>
        /// <param name="post">the last episode in the batch (possibly null if there is no episode post)</param>
        /// <param name="lateTimeBound">the latest point</param>
        /// <returns>If we can fit them all in properly</returns>
        private bool InitialInsertCheckPossible(Time earlyTimeBound, Episode prior, Episode middle, Episode post, Time lateTimeBound)
        {
            Time minPrior = (prior != null ? Tasha.Scheduler.Scheduler.PercentOverlapAllowed * prior.OriginalDuration : Time.Zero);
            Time minMid = Tasha.Scheduler.Scheduler.PercentOverlapAllowed * middle.OriginalDuration;
            Time minPost = (post != null ? Tasha.Scheduler.Scheduler.PercentOverlapAllowed * post.OriginalDuration : Time.Zero);
            // it is possible to fit in if the bounds are larger than the minimum size of all three episodes
            return (lateTimeBound - earlyTimeBound) >= (minPrior + minMid + minPost);
        }

        private bool Insert(Time earlyTimeBound, Episode prior, Episode middle, Episode post, Time lateTimeBound)
        {
            // Do a quick check to see if it is even possible to fit everything in together
            if(!InitialInsertCheckPossible(earlyTimeBound, prior, middle, post, lateTimeBound)) return false;
            // if we get here we know that there is a way to insert the episodes successfully
            if(UnableToJustMoveToInsert(earlyTimeBound, prior, middle, post, lateTimeBound))
            {
                FixDurationToInsert(ref earlyTimeBound, prior, middle, post, ref lateTimeBound);
            }
            else
            {
                return this.ShiftToInsert(ref earlyTimeBound, prior, middle, post, ref lateTimeBound);
            }
            return true;
        }

        private bool ShiftToInsert(ref Time earlyTimeBound, Episode prior, Episode middle, Episode post, ref Time lateTimeBound)
        {
            if(prior != null & post != null)
            {
                return AllThreeInsert(ref earlyTimeBound, prior, middle, post, ref lateTimeBound);
            }
            else if(prior != null)
            {
                return PriorMiddleInsert(ref earlyTimeBound, prior, middle, ref lateTimeBound);
            }
            else if(post != null)
            {
                return MiddlePostInsert(ref earlyTimeBound, middle, post, ref lateTimeBound);
            }
            throw new XTMFRuntimeException("Unexpected shift to insert case!");
        }

        private bool UnableToJustMoveToInsert(Time earlyTimeBound, Episode prior, Episode middle, Episode post, Time lateTimeBound)
        {
            // if the amount of time that we have to work with is larger then we are unable to just move to insert
            return (lateTimeBound - earlyTimeBound)
                < (prior != null ? prior.Duration : Time.Zero)
                + middle.Duration
                + (post != null ? post.Duration : Time.Zero);
        }
    }
}