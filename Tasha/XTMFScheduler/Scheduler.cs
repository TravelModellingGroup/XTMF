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
using Tasha.Common;
using Tasha.Scheduler;
using TMG;
using XTMF;
using TashaProject = Tasha.Scheduler.IProject;

namespace Tasha.XTMFScheduler
{
    public class Scheduler : ITashaScheduler
    {
        [SubModelInformation( Required = false, Description = "The different projects that will be available." )]
        public List<TashaProject> Projects;

        [SubModelInformation( Required = true, Description = "Combines episodes into a schedule and generates trips." )]
        public IScheduleFactory SchedulingAgorithm;

        [RunParameter( "Random Seed", 12345, "A number to fix the starting point of the random number generation." )]
        public int Seed;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public void LoadOneTimeLocalData()
        {
        }

        public void Run(ITashaHousehold household)
        {
            Random householdRandom = new( Seed * household.HouseholdId );
            var persons = household.Persons;
            List<IActivityEpisode>[] episodes = InitializeEpisodes( persons );
            GenerateEpisodes( household, householdRandom, persons, episodes );
            OrderPriorities( episodes );
            ScheduleEpisodes(householdRandom, episodes );
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public Time TravelTime(ITashaPerson person, IZone origin, IZone destination, Time tashaTime)
        {
            return Time.Zero;
        }

        private static void BubbleSortList(List<IActivityEpisode> list)
        {
            for ( int i = 0; i < list.Count; i++ )
            {
                for ( int j = i + 1; j < list.Count; j++ )
                {
                    if ( list[i].Priority > list[i - 1].Priority )
                    {
                        var temp = list[i - 1];
                        list[i - 1] = list[i];
                        list[i] = temp;
                    }
                }
            }
        }

        private static List<IActivityEpisode>[] InitializeEpisodes(ITashaPerson[] persons)
        {
            List<IActivityEpisode>[] episodes = new List<IActivityEpisode>[persons.Length];
            for ( int p = 0; p < persons.Length; p++ )
            {
                episodes[p] = [];
            }
            return episodes;
        }

        private static void OrderPriorities(List<IActivityEpisode>[] episodes)
        {
            for ( int i = 0; i < episodes.Length; i++ )
            {
                BubbleSortList( episodes[i] );
            }
        }

        private int CountEpisodes(List<IActivityEpisode>[] episodes)
        {
            var total = episodes[0].Count;
            for ( int i = 1; i < episodes.Length; i++ )
            {
                total += episodes[i].Count;
            }
            return total;
        }

        private void GenerateEpisodes(ITashaHousehold household, Random householdsRandom, ITashaPerson[] persons, List<IActivityEpisode>[] episodes)
        {
            for ( int i = 0; i < Projects.Count; i++ )
            {
                for ( int p = 0; p < persons.Length; p++ )
                {
                    Projects[i].Generate( household, persons[p], episodes[p], householdsRandom );
                }
            }
        }

        private void ScheduleEpisodes(Random householdRandom, List<IActivityEpisode>[] episodes)
        {
            int totalEpisodes = CountEpisodes( episodes );
            int[] index = new int[episodes.Length];
            ISchedule[] schedules = new ISchedule[episodes.Length];
            for ( int i = 0; i < schedules.Length; i++ )
            {
                schedules[i] = SchedulingAgorithm.Generate();
            }
            // insert into the schedule
            for ( int episodeIndex = 0; episodeIndex < totalEpisodes; episodeIndex++ )
            {
                int personToRun = -1;
                int maxPriority = int.MinValue;
                for ( int i = 0; i < index.Length; i++ )
                {
                    // make sure there is another episode to run
                    if ( index[i] < episodes[i].Count )
                    {
                        var episode = episodes[i][index[i]];
                        if ( maxPriority < episode.Priority )
                        {
                            maxPriority = episode.Priority;
                            personToRun = i;
                        }
                    }
                }
                if ( personToRun < 0 )
                {
                    break;
                }
                schedules[personToRun].Insert( householdRandom, episodes[personToRun][index[personToRun]] );
            }
        }
    }
}