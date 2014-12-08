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
using System.Threading;
using Tasha.Common;
using XTMF;
using TMG.Estimation;
using Tasha.Scheduler;
using Tasha.Common;

namespace Tasha.Estimation.LocationChoice
{
    public class EvaluateLocationChoice : IPostHousehold
    {
        [SubModelInformation(Required = true, Description = "The location choice model to evaluate.")]
        public ILocationChoiceModel LocationChoice;

        [RootModule]
        public IEstimationClientModelSystem Root;

        [RunParameter("Tests", 300, "How many random pops should we do to simulate the probability?")]
        public int Tests;

        public string Name
        {
            get; set;
        }

        public float Progress
        {
            get
            {
                return 0f;
            }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get
            {
                return null;
            }
        }

        SpinLock FitnessLock = new SpinLock(false);
        private float Fitness = 0.0f;

        [RunParameter("Random Seed", 423165524, "The seed to base the random generation from.")]
        public int RandomSeed;

        public void Execute(ITashaHousehold household, int iteration)
        {
            var localFitness = 0.0f;
            var persons = household.Persons;
            Random random = new Random(household.HouseholdId * RandomSeed);
            for(int personIndex = 0; personIndex < persons.Length; personIndex++)
            {
                var tripChains = persons[personIndex].TripChains;
                for(int tcIndex = 0; tcIndex < tripChains.Count; tcIndex++)
                {
                    var trips = tripChains[tcIndex].Trips;
                    IEpisode[] episodes = BuildScheduleFromTrips(trips);
                    for(int tripIndex = 0; tripIndex < trips.Count - 1; tripIndex++)
                    {
                        var activtiyType = episodes[tripIndex].ActivityType;
                        TMG.IZone revieldChoice = trips[tripIndex].DestinationZone;
                        switch(activtiyType)
                        {
                            case Activity.WorkBasedBusiness:
                            case Activity.Market:
                            case Activity.IndividualOther:
                            case Activity.JointMarket:
                            case Activity.JointOther:
                                {
                                    int correct = 0;
                                    for(int test = 0; test < Tests; test++)
                                    {
                                        var choice = LocationChoice.GetLocation(episodes[tripIndex], random);
                                        if(choice == revieldChoice)
                                        {
                                            correct++;
                                        }
                                    }
                                    localFitness += (float)Math.Log((correct + 1.0f) / (Tests + 1.0f));
                                    break;
                                }
                        }
                    }
                }
            }
            // evaluate the household
            bool taken = false;
            FitnessLock.Enter(ref taken);
            Fitness += localFitness;
            if(taken) FitnessLock.Exit();
        }

        private IEpisode[] BuildScheduleFromTrips(List<ITrip> trips)
        {
            ITashaPerson owner = trips[0].TripChain.Person;
            PersonSchedule schedule = new PersonSchedule(owner);
            // we don't do the last trip since it is always to home.
            for(int i = 0; i < trips.Count - 1; i++)
            {
                Episode ep = CreateEpisode(trips[i], trips[i + 1], owner);
                ep.Zone = trips[i].DestinationZone;
                schedule.InsertAt(ep, i);
            }
            return schedule.Episodes;
        }

        private Episode CreateEpisode(ITrip trip, ITrip nextTrip, ITashaPerson owner)
        {
            return new ActivityEpisode(0, new TimeWindow(trip.ActivityStartTime, nextTrip.ActivityStartTime), trip.Purpose, owner);
        }

        public void IterationFinished(int iteration)
        {
            Root.RetrieveValue = () => Fitness;
        }

        public void IterationStarting(int iteration)
        {
            Fitness = 0.0f;
            // reload all of the probabilities
            LocationChoice.LoadLocationChoiceCache();
        }

        public void Load(int maxIterations)
        {

        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
