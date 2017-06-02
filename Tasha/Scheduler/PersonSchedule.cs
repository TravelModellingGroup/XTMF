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
using System.Threading;
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Scheduler
{
    public sealed class PersonSchedule : Schedule
    {
        private Time FirstTripTime;

        public PersonSchedule(ITashaPerson owner)
        {
            Owner = owner;
            FirstTripTime = Time.Zero;
        }

        public ITashaPerson Owner
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
            throw new NotImplementedException();
        }

        public override int CleanUp(Time minPrimaryWorkLength)
        {
            /*
             * Phase 1
             * Remove work episodes that have a duration of 30 minutes or less
             */
            RemoveSmallWorkEpisodes(minPrimaryWorkLength);
            return 0;
        }

        public override bool ForcedEpisodeInsert(Episode ep)
        {
            throw new NotImplementedException();
        }

        public override void GenerateTrips(ITashaHousehold household, int householdIterations, Time minimumAtHomeTime)
        {
            /*
             * Now we need to generate the trips.
             * For each episode, we need to check if the person goes home first or not
             * If the do go home we need to add a trip there and a trip to i+1
             */
            SchedulerTripChain currentChain = null;
            int i = 0;
            bool atHome = true;
            int tripNumber = 1;
            var homeZone = household.HomeZone;
            for(; i < EpisodeCount; i++)
            {
                var isAtHomeEpisode = AtHomeActivity(Episodes[i].ActivityType);
                // if we are already at home and we are going to be doing a household activity just continue
                if(atHome && isAtHomeEpisode)
                {
                    continue;
                }
                // if we are not currently working on a trip chain, setup a new one and add it to our person
                if(currentChain == null)
                {
                    currentChain = SchedulerTripChain.GetTripChain(Owner);
                    Owner.TripChains.Add(currentChain);
                }
                // If we are out or we want to head out build our trip to leave
                SchedulerTrip trip = SchedulerTrip.GetTrip(householdIterations);
                trip.Purpose = Episodes[i].ActivityType;
                trip.ActivityStartTime = Episodes[i].StartTime;
                trip.OriginalZone = (i == 0 ? homeZone : Episodes[i - 1].Zone);
                trip.DestinationZone = Episodes[i].Zone;

                // Check to see if we should go home unless we are already there OR the activity we are going to brings us to home anyways
                if(!atHome && !isAtHomeEpisode)
                {
                    Time toHome = Scheduler.TravelTime(Owner, Episodes[i - 1].Zone, homeZone,
                        Episodes[i - 1].EndTime);
                    Time toNext = Scheduler.TravelTime(Owner, homeZone, Episodes[i].Zone,
                        Episodes[i].StartTime);
                    if(toHome + toNext + minimumAtHomeTime < Episodes[i].StartTime - Episodes[i - 1].EndTime)
                    {
                        // Set a course, for home
                        SchedulerHomeTrip toHomeTrip = SchedulerHomeTrip.GetTrip(householdIterations);
                        toHomeTrip.TripStartTime = Episodes[i - 1].EndTime;
                        toHomeTrip.OriginalZone = Episodes[i - 1].Zone;
                        toHomeTrip.DestinationZone = homeZone;
                        toHomeTrip.Purpose = GetGoingHomePurpose();
                        toHomeTrip.TripChain = currentChain;
                        toHomeTrip.TripNumber = tripNumber;
                        currentChain.Trips.Add(toHomeTrip);
                        // the next trip starts back at one
                        tripNumber = 1;
                        // Now we can move onto the next chain
                        currentChain = SchedulerTripChain.GetTripChain(Owner);
                        Owner.TripChains.Add(currentChain);
                        // We also need to update the zone that we are coming from (home)
                        trip.OriginalZone = homeZone;
                    }
                }
                trip.TripChain = currentChain;
                trip.TripNumber = tripNumber++;
                currentChain.Trips.Add(trip);
                if(isAtHomeEpisode)
                {
                    currentChain = null;
                }
                atHome = isAtHomeEpisode;
            }
            // Now that everything is scheduled
            if(!atHome)
            {
                SchedulerHomeTrip toHome = SchedulerHomeTrip.GetTrip(householdIterations);
                toHome.OriginalZone = Episodes[EpisodeCount - 1].Zone;
                if(Episodes[EpisodeCount - 1].Zone == null)
                {
                    throw new XTMFRuntimeException("An episode had a null zone!");
                }
                if(toHome.OriginalZone.ZoneNumber == Tasha.Scheduler.Scheduler.Tasha.ZoneSystem.RoamingZoneNumber)
                {
                    throw new XTMFRuntimeException
                        (
                        "We are trying to make a trip from the roaming zone to home!" +
                        "\r\nHHLD:" + household.HouseholdId +
                        "\r\nPrevious Purpose" + Episodes[EpisodeCount - 1].ActivityType
                        );
                }

                toHome.DestinationZone = homeZone;
                toHome.Purpose = GetGoingHomePurpose();
                toHome.TripChain = currentChain;
                toHome.TripStartTime = Episodes[EpisodeCount - 1].EndTime;
                // this will have already been increased in the while loop
                toHome.TripNumber = tripNumber;
                currentChain.Trips.Add(toHome);
            }
        }

        public bool Insert(Episode ep, IZone location)
        {
            // you must be going somewhere
            if(location == null)
            {
                return false;
            }
            ep.Zone = location;
            /*
             * This is going to be very similar to the ProjectSchedule version
             * The only difference is that we are going to take into account travel times
             */
            ConflictReport conflict = InsertCase(Owner, ep, true);
            switch(conflict.Type)
            {
                case ScheduleConflictType.NoConflict:
                    {
                        InsertAt(ep, conflict.Position);
                        return true;
                    }
                case ScheduleConflictType.CompleteOverlap:
                case ScheduleConflictType.Split:
                    {
                        // At this point work-business episodes have already been dealt with in the project schedule
                        // Thus everything that splits an episode is in fact not allowed!
                        return false;
                    }
                case ScheduleConflictType.Posterior:
                    {
                        // the given position is the element we need to go after
                        Time earlyTimeBound = Time.StartOfDay + FirstTripTime;
                        Time lateTimeBound = Time.EndOfDay;
                        Episode prior = (Episode)Episodes[conflict.Position];
                        Episode middle = ep;
                        Episode post = (Episode)((conflict.Position < EpisodeCount - 1)
                            ? Episodes[conflict.Position + 1] : null);
                        // Make sure to bound the times with the padding of the travel times required
                        if(conflict.Position >= 1)
                        {
                            earlyTimeBound = Episodes[conflict.Position - 1].EndTime + Scheduler.TravelTime(Owner, Episodes[conflict.Position - 1].Zone,
                                prior.Zone, prior.StartTime);
                        }
                        if(EpisodeCount - conflict.Position > 2 && post != null)
                        {
                            lateTimeBound = Episodes[conflict.Position + 2].StartTime - Scheduler.TravelTime(Owner, post.Zone, Episodes[conflict.Position + 2].Zone, post.EndTime);
                        }
                        if(Insert(earlyTimeBound, prior, middle, post, lateTimeBound))
                        {
                            InsertAt(ep, conflict.Position + 1);
                            return true;
                        }
                        return false;
                    }
                case ScheduleConflictType.Prior:
                    {
                        // The given position is the element we need to go before
                        Time earlyTimeBound = Time.StartOfDay + FirstTripTime;
                        Time lateTimeBound = Time.EndOfDay;
                        Episode prior = (Episode)(conflict.Position > 0 ? Episodes[conflict.Position - 1] : null);
                        Episode middle = ep;
                        Episode post = (Episode)Episodes[conflict.Position];

                        // Make sure to bound the times with the padding of the travel times required
                        if(conflict.Position >= 2 && prior != null)
                        {
                            earlyTimeBound = Episodes[conflict.Position - 2].EndTime + Scheduler.TravelTime(Owner, Episodes[conflict.Position - 2].Zone,
                                prior.Zone, prior.StartTime);
                        }
                        if(EpisodeCount - conflict.Position > 1)
                        {
                            lateTimeBound = Episodes[conflict.Position + 1].StartTime - Scheduler.TravelTime(Owner, post.Zone,
                                Episodes[conflict.Position + 1].Zone, post.EndTime);
                        }

                        if(Insert(earlyTimeBound, prior, middle, post, lateTimeBound))
                        {
                            InsertAt(ep, conflict.Position);
                            return true;
                        }
                        return false;
                    }
                default:
                    throw new NotImplementedException("Unknown insert conflict type!");
            }
        }

        public override bool Insert(Episode ep, Random random)
        {
            IZone zone = null;
            ep.ContainingSchedule = this;
            // if we are generating locations now is the time to do it
            if(random != null)
            {
                zone = Tasha.Scheduler.Scheduler.LocationChoiceModel.GetLocation(ep, random);
                // if we can not find a zone, then this episode is infeasible
                if(zone == null)
                {
                    return false;
                }

            }
            return Insert(ep, zone);
        }

        public override bool TestInsert(Episode episode)
        {
            throw new NotImplementedException();
        }

        public static float SkippedWorkEpisodes;
        private static SpinLock SkippedWorkLock = new SpinLock(false);

        internal void InsertWorkSchedule(Schedule schedule, Random random)
        {            
            //Second pass is to add primary work trips
            for(int i = 0; i < schedule.EpisodeCount; i++)
            {
                if(schedule.Episodes[i].ActivityType == Activity.PrimaryWork)
                {
                    if(!Insert((Episode)schedule.Episodes[i], random))
                    {
                        var expFactor = Owner.ExpansionFactor;
                        var taken = false;
                        SkippedWorkLock.Enter(ref taken);
                        SkippedWorkEpisodes += expFactor;
                        if(taken) SkippedWorkLock.Exit(true);
                    }
                }
            }

            //Third pass is to add everything else
            for(int i = 0; i < schedule.EpisodeCount; i++)
            {
                if(schedule.Episodes[i].ActivityType != Activity.PrimaryWork
                    && schedule.Episodes[i].ActivityType != Activity.WorkBasedBusiness)
                {
                    if(!Insert((Episode)schedule.Episodes[i], random))
                    {
                        var expFactor = Owner.ExpansionFactor;
                        var taken = false;
                        SkippedWorkLock.Enter(ref taken);
                        SkippedWorkEpisodes += expFactor;
                        if(taken) SkippedWorkLock.Exit(true);
                    }
                }
            }

            // First pass is to add works based business trips
            for(int i = 0; i < schedule.EpisodeCount; i++)
            {
                if(schedule.Episodes[i].ActivityType == Activity.WorkBasedBusiness)
                {
                    if(!Insert((Episode)schedule.Episodes[i], random))
                    {
                        var expFactor = Owner.ExpansionFactor;
                        var taken = false;
                        SkippedWorkLock.Enter(ref taken);
                        SkippedWorkEpisodes += expFactor;
                        if(taken) SkippedWorkLock.Exit(true);
                    }
                }
            }
           
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
                // prior overlap < 0, so just add it
                Relocate(middle, (middle.StartTime + priorOverlap));
                // subtract out the reduced time
                postOverlap += priorOverlap;
            }
            if(postOverlap <= Time.Zero)
            {
                // check to see if there is enough time to move the middle closer to the prior
                if(priorOverlap <= -postOverlap)
                {
                    Relocate(middle, (middle.StartTime + priorOverlap));
                    return true;
                }
                // prior overlap < 0, so subtract it
                Relocate(middle, (middle.StartTime - postOverlap));
                // subtract out the reduced time
                priorOverlap += postOverlap;
            }
            return false;
        }

        private static bool MiddlePostInsert(ref Time earlyTimeBound, Episode middle, Episode post, ref Time firstTime, ref Time secondTime)
        {
            Time overlap = (middle.EndTime + secondTime) - post.StartTime;
            if(overlap <= Time.Zero)
            {
                return true;
            }
            // if we can move forward, move forward
            if((middle.StartTime - firstTime) - earlyTimeBound > overlap)
            {
                Relocate(middle, (middle.StartTime - overlap));
                return true;
            }
            // if that is not enough, move as forward as we can
            Relocate(middle, (earlyTimeBound + firstTime));
            overlap = (middle.EndTime + secondTime) - post.StartTime;
            Relocate(post, (post.StartTime + overlap));
            return true;
        }

        private static bool PriorMiddleInsert(Episode prior, Episode middle, ref Time lateTimeBound, ref Time firstTime, ref Time secondTime)
        {
            Time overlap = (prior.EndTime + firstTime) - middle.StartTime;
            if(overlap <= Time.Zero)
            {
                return true;
            }
            // if we can move back, move back
            if(lateTimeBound - (middle.EndTime + secondTime) > overlap)
            {
                Relocate(middle, (prior.EndTime + firstTime));
                return true;
            }
            // move back as far as we can
            Relocate(middle, (lateTimeBound - middle.Duration - secondTime));
            // recalculate the overlap
            overlap = (prior.EndTime + firstTime) - middle.StartTime;
            Relocate(prior, prior.StartTime - overlap);
            return true;
        }

        private static void Relocate(Episode ep, Time startTime)
        {
            var dur = ep.Duration;
            ep.EndTime = (ep.StartTime = startTime) + dur;
        }

        private bool AllThreeInsert(ref Time earlyTimeBound, Episode prior, Episode middle, Episode post, ref Time lateTimeBound, ref Time firstTime, ref Time secondTime)
        {
            Time priorOverlap = (prior.EndTime + firstTime) - middle.StartTime;
            Time postOverlap = (middle.EndTime + secondTime) - post.StartTime;
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
                Relocate(post, middle.EndTime + secondTime);
            }
            else
            {
                Relocate(middle, middle.StartTime - overlap);
                Relocate(prior, middle.StartTime - firstTime - prior.Duration);
            }
            // Sanity Check
            if(post.EndTime > lateTimeBound)
            {
                throw new XTMFRuntimeException("We ended too late when inserting with 3 into a person schedule!\r\n"
                    + Dump(this));
            }
            if(prior.StartTime < earlyTimeBound)
            {
                throw new XTMFRuntimeException("We started too early when inserting with 3 into a person schedule!\r\n"
                                               + Dump(this)
                                               + "\r\nFirst Time = " + firstTime
                                               + "\r\nSecond Time = " + secondTime);
            }
            return true;
        }

        private bool AtHomeActivity(Activity activity)
        {
            return activity == Activity.ReturnFromSchool
                    | activity == Activity.ReturnFromWork
                    | activity == Activity.PickupAndReturn
                    | activity == Activity.WorkAtHomeBusiness;
        }

        private void FixDurationToInsert(ref Time earlyTimeBound, Episode prior, Episode middle, Episode post, ref Time lateTimeBound, ref Time firstTime, ref Time secondTime)
        {
            // If we get here then we need to calculate the ratios and then assign the start times accordingly
            if(prior != null)
            {
                var duration = prior.Duration;
                prior.StartTime = earlyTimeBound;
                prior.EndTime = earlyTimeBound + duration;
            }
            if(post != null)
            {
                var duration = post.Duration;
                post.EndTime = lateTimeBound;
                post.StartTime = lateTimeBound - duration;
            }
            Time totalOriginalDuration = (prior != null ? prior.OriginalDuration : Time.Zero)
                + middle.OriginalDuration
                + (post != null ? post.OriginalDuration : Time.Zero);
            Time minPrior = (prior != null ? Tasha.Scheduler.Scheduler.PercentOverlapAllowed * prior.OriginalDuration : Time.Zero);
            Time minMid = Tasha.Scheduler.Scheduler.PercentOverlapAllowed * middle.OriginalDuration;
            Time minPost = (post != null ? Tasha.Scheduler.Scheduler.PercentOverlapAllowed * post.OriginalDuration : Time.Zero);
            Time remainder = (lateTimeBound - earlyTimeBound) - (minPrior + minMid + minPost + firstTime + secondTime);
            float ratioPrior;
            float ratioMiddle;
            if(prior != null)
            {
                ratioPrior = prior.OriginalDuration / totalOriginalDuration;
                prior.StartTime = earlyTimeBound;
                prior.EndTime = prior.StartTime + minPrior + (ratioPrior * remainder);
                middle.StartTime = prior.EndTime + firstTime;
            }
            else
            {
                middle.StartTime = earlyTimeBound + firstTime;
            }
            ratioMiddle = middle.OriginalDuration / totalOriginalDuration;
            middle.EndTime = middle.StartTime + minMid + (ratioMiddle * remainder);
            if(post != null)
            {
                // we do not need to include the ratio calculation for post since we know it needs to end at the end of the allowed time
                // and that it needs to start right after the middle
                post.StartTime = middle.EndTime + secondTime;
                post.EndTime = lateTimeBound;
            }
        }

        private Activity GetGoingHomePurpose()
        {
            return Activity.Home;
        }

        /// <summary>
        /// Run a quick check to see if it is at all possible to place all of the 3 episodes in the given time bounds
        /// </summary>
        /// <param name="earlyTimeBound">The earliest point</param>
        /// <param name="prior">the first episode in the batch (possibly null if there is no episode prior)</param>
        /// <param name="middle">the second episode in the batch</param>
        /// <param name="post">the last episode in the batch (possibly null if there is no episode post)</param>
        /// <param name="lateTimeBound">the latest point</param>
        /// <param name="travelFirst"></param>
        /// <param name="travelSecond"></param>
        /// <returns>If we can fit them all in properly</returns>
        private bool InitialInsertCheckPossible(Time earlyTimeBound, Episode prior, Episode middle, Episode post, Time lateTimeBound,
            out Time travelFirst,
            out Time travelSecond
            )
        {
            Time minPrior = Time.Zero;
            Time minMid = Tasha.Scheduler.Scheduler.PercentOverlapAllowed * middle.OriginalDuration;
            Time minPost = Time.Zero;
            if(prior != null)
            {
                minPrior = Tasha.Scheduler.Scheduler.PercentOverlapAllowed * prior.OriginalDuration;
                travelFirst = Scheduler.TravelTime(Owner, prior.Zone, middle.Zone, prior.EndTime);
            }
            else
            {
                travelFirst = Scheduler.TravelTime(Owner, Owner.Household.HomeZone, middle.Zone, middle.StartTime);
            }
            if(post != null)
            {
                minPost = Tasha.Scheduler.Scheduler.PercentOverlapAllowed * post.OriginalDuration;
                travelSecond = Scheduler.TravelTime(Owner, middle.Zone, post.Zone, middle.EndTime);
            }
            else
            {
                travelSecond = Scheduler.TravelTime(Owner, middle.Zone, Owner.Household.HomeZone, middle.EndTime);
            }
            // it is possible to fit in if the bounds are larger than the minimum size of all three episodes
            return (lateTimeBound - earlyTimeBound) >= (minPrior + minMid + minPost + travelFirst + travelSecond);
        }

        private bool Insert(Time earlyTimeBound, Episode prior, Episode middle, Episode post, Time lateTimeBound)
        {
            if(earlyTimeBound < Time.StartOfDay)
            {
                throw new XTMFRuntimeException("An episode had an early time bound before the start of the day!");
            }
            if(lateTimeBound > Time.EndOfDay)
            {
                throw new XTMFRuntimeException("An episode had a late time before after the end of the day!");
            }
            Time firstTime;
            Time secondTime;
            // Do a quick check to see if it is even possible to fit everything in together
            if(!InitialInsertCheckPossible(earlyTimeBound, prior, middle, post, lateTimeBound, out firstTime, out secondTime))
            {
                return false;
            }
            if(firstTime < Time.Zero)
            {
                throw new XTMFRuntimeException("The First time in an insert is negative! " + firstTime + ".  Going from " + prior.Zone.ZoneNumber + " to " + middle.Zone.ZoneNumber + " at " + middle.StartTime + "!");
            }
            if(secondTime < Time.Zero)
            {
                throw new XTMFRuntimeException("The Second time in an insert is negative! " + secondTime + ".  Going from " + middle.Zone.ZoneNumber + " to "
                    + (post == null ? Owner.Household.HomeZone.ZoneNumber : post.Zone.ZoneNumber) + " at " + (post == null ? middle.EndTime : post.StartTime) + "!");
            }
            // Assign the travel times to the episodes
            if(prior == null)
            {
                FirstTripTime = firstTime;
            }
            else
            {
                prior.TravelTime = firstTime;
            }
            if(post != null)
            {
            }
            middle.TravelTime = secondTime;
            // if we get here we know that there is a way to insert the episodes successfully
            if(UnableToJustMoveToInsert(earlyTimeBound, prior, middle, post, lateTimeBound, ref firstTime, ref secondTime))
            {
                FixDurationToInsert(ref earlyTimeBound, prior, middle, post, ref lateTimeBound, ref firstTime, ref secondTime);
            }
            else
            {
                var ret = ShiftToInsert(ref earlyTimeBound, prior, middle, post, ref lateTimeBound, ref firstTime, ref secondTime);
                return ret;
            }
            return true;
        }

        private void RemoveSmallWorkEpisodes(Time minPrimaryWorkLength)
        {
            for(int i = 0; i < EpisodeCount; i++)
            {
                if(Episodes[i].ActivityType == Activity.PrimaryWork
                    && Episodes[i].Duration < minPrimaryWorkLength)
                {
                    // if this isn't the last episode
                    if(EpisodeCount < i + 1)
                    {
                        if(i > 1)
                        {
                            ((Episode)Episodes[i - 1]).TravelTime = Scheduler.TravelTime(Owner, Episodes[i - 1].Zone,
                                Episodes[i + 1].Zone, Episodes[i - 1].EndTime);
                            ((Episode)Episodes[i + 1]).StartTime = Episodes[i - 1].EndTime + Episodes[i - 1].TravelTime;
                        }
                        else
                        {
                            Time travelTime = Scheduler.TravelTime(Owner, Owner.Household.HomeZone,
                                Episodes[i + 1].Zone, Episodes[i + 1].StartTime);
                            ((Episode)Episodes[i + 1]).StartTime = ((Episode)Episodes[i]).StartTime - FirstTripTime + travelTime;
                            FirstTripTime = ((Episode)Episodes[i + 1]).StartTime - travelTime;
                        }
                        Array.Copy(Episodes, i + 1, Episodes, i, EpisodeCount - i - 1);
                    }
                    EpisodeCount--;
                    Episodes[EpisodeCount] = null;
                    i--;
                }
            }
        }

        private bool ShiftToInsert(ref Time earlyTimeBound, Episode prior, Episode middle, Episode post, ref Time lateTimeBound, ref Time firstTime, ref Time secondTime)
        {
            if(prior != null & post != null)
            {
                return AllThreeInsert(ref earlyTimeBound, prior, middle, post, ref lateTimeBound, ref firstTime, ref secondTime);
            }
            if(prior != null)
            {
                return PriorMiddleInsert(prior, middle, ref lateTimeBound, ref firstTime, ref secondTime);
            }
            if(post != null)
            {
                return MiddlePostInsert(ref earlyTimeBound, middle, post, ref firstTime, ref secondTime);
            }
            throw new XTMFRuntimeException("Unexpected shift to insert case!");
        }

        private bool UnableToJustMoveToInsert(Time earlyTimeBound, Episode prior, Episode middle, Episode post, Time lateTimeBound,
            ref Time travelFirst,
            ref Time travelSecond)
        {
            // if the amount of time that we have to work with is larger then we are unable to just move to insert
            var totalTime = (prior != null ? prior.Duration : Time.Zero)
                + middle.Duration
                + (post != null ? post.Duration : Time.Zero)
                + travelFirst + travelSecond;
            var boundTime = (lateTimeBound - earlyTimeBound);
            return boundTime
                <= totalTime;
        }
    }
}