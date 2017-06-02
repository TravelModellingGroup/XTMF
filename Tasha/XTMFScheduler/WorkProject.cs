/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Diagnostics.CodeAnalysis;
using Datastructure;
using Tasha.Common;
using Tasha.Scheduler;
using XTMF;
using TashaProject = Tasha.Scheduler.IProject;

namespace Tasha.XTMFScheduler
{
    public class WorkProject : TashaProject
    {
        [SubModelInformation( Required = true, Description = "For PD,AgeCategory,EmploymentStatus,Occupation" )]
        public IResource AgeEmpStatOccProbability;

        [SubModelInformation( Required = true, Description = "A link to the different ages used." )]
        public IResource AgeResource;

        [SubModelInformation( Required = true, Description = "For PD,EmploymentStatus,Occupation,Duration" )]
        public IResource EmpStatOccDurationProbability;

        [Parameter( "End Of Day", "28h", typeof( Time ), "The end of the day." )]
        public Time EndOfDay;

        [Parameter( "Start Of Day", "4:00AM", typeof( Time ), "The start of the day." )]
        public Time StartOfDay;

        [SubModelInformation( Required = true, Description = "For PD,EmploymentStatus,Occupation,Duration/15 a probability of the start time." )]
        public IResource StartTimePerPDEmpStatOccDuration;

        /// <summary>
        /// The age categories for the model
        /// </summary>
        private IndexedRangeSet Ages;

        /// <summary>
        /// PD, [EmploymentStatus,Occupation,Duration / 15 minutes]
        /// </summary>
        private SparseArray<SparseTriIndex<float>> DurationProbability;

        /// <summary>
        /// PD, [Age,EmploymentStatus,Occupation]
        /// </summary>
        private SparseArray<SparseTriIndex<float>> GenerationProbability;

        public bool IsHouseholdProject { get { return false; } }

        public string Name { get; set; }

        public float Progress { get { return 0f; } }

        public Tuple<byte, byte, byte> ProgressColour { get { return null; } }

        public bool AssignStartTime(ITashaPerson person, int personIndex, ISchedule[] schedule, IActivityEpisode episode, Random rand)
        {
            switch ( episode.Purpose )
            {
                case Activity.PrimaryWork:
                    return PrimaryWorkStartTime(person, personIndex, schedule, episode, rand );

                default:
                    throw new XTMFRuntimeException( "In '" + Name + "' we received an episode of purpose '" + episode.Purpose + "'" );
            }
        }

        public void Generate(ITashaHousehold household, ITashaPerson person, List<IActivityEpisode> episodes, Random rand)
        {
            var empStatus = person.EmploymentStatus;
            if ( empStatus == TMG.TTSEmploymentStatus.NotEmployed )
            {
                return;
            }
            var flatPd = GenerationProbability.GetFlatIndex( household.HomeZone.PlanningDistrict );
            var occupation = person.Occupation;
            var probability = GenerationProbability.GetFlatData()[flatPd][Ages.IndexOf( person.Age ), (int)empStatus, (int)occupation];
            var pop = rand.NextDouble();
            // If the random number is less than the probability then
            if ( pop <= probability )
            {
                //then generate an activity
                // ReSharper disable once UnusedVariable
                var data = DurationProbability.GetFlatData()[flatPd];
            }
        }

        public void IterationComplete(int currentIteration, int totalIterations)
        {
            GenerationProbability = null;
            Ages = null;
        }

        public void IterationStart(int currentIteration, int totalIterations)
        {
            GenerationProbability = AgeEmpStatOccProbability.AcquireResource<SparseArray<SparseTriIndex<float>>>();
            DurationProbability = EmpStatOccDurationProbability.AcquireResource<SparseArray<SparseTriIndex<float>>>();
            Ages = AgeResource.AcquireResource<IndexedRangeSet>();
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( AgeEmpStatOccProbability.CheckResourceType( typeof( IDataSource<SparseArray<SparseTriIndex<float>>> ) ) )
            {
                error = "In '" + Name + "', the resource '" + AgeEmpStatOccProbability.ResourceName
                    + "' does not contain an IDataSource<SparseTriIndex<float>> for worker generation probabilities!";
                return false;
            }
            if ( EmpStatOccDurationProbability.CheckResourceType( typeof( IDataSource<SparseArray<SparseTriIndex<float>>> ) ) )
            {
                error = "In '" + Name + "', the resource '" + EmpStatOccDurationProbability.ResourceName
                    + "' does not contain an IDataSource<SparseTriIndex<float>> for full time worker duration probabilities!";
                return false;
            }
            if ( AgeResource.CheckResourceType( typeof( IndexedRangeSet ) ) )
            {
                error = "In '" + Name + "', the resource '" + AgeResource.ResourceName + "' for age categories does not contain an IndexedRangeSet!";
                return false;
            }
            return true;
        }

        [SuppressMessage("ReSharper", "UnusedParameter.Local")]
        private bool GiveStartTimeForPrimaryWork(ITashaPerson worker, List<TimeWindow> timeWindows, IActivityEpisode episode, Random rand)
        {
            throw new NotImplementedException();
        }

        private bool PrimaryWorkStartTime(ITashaPerson person, int personIndex, ISchedule[] schedule, IActivityEpisode episode, Random rand)
        {
            var personSchedule = schedule[personIndex];
            var episodeList = personSchedule.GenerateScheduledEpisodeList();
            var episodeDuration = episode.Duration;
            List<TimeWindow> timeWindows = new List<TimeWindow>();
            //If there are no episodes in the schedule, then we can place it anywhere
            if ( episodeList.Length == 0 || episodeList[0] == null )
            {
                timeWindows.Add( new TimeWindow() { StartTime = StartOfDay, EndTime = EndOfDay } );
                return GiveStartTimeForPrimaryWork( person, timeWindows, episode, rand);
            }
            //Check before the first episode
            {
                //Check after the last episode
                var firstStartTime = episodeList[0].StartTime;
                if ( firstStartTime - StartOfDay >= episodeDuration )
                {
                    timeWindows.Add( new TimeWindow() { StartTime = StartOfDay, EndTime = firstStartTime} );
                }
            }
            //Check between each episode
            for ( int i = 0; i < episodeList.Length - 1; i++ )
            {
                var e = episodeList[i + 1];
                if ( e == null )
                {
                    break;
                }
                var endTime = episodeList[i].EndTime;
                var startTime = episodeList[i + 1].StartTime;
                if ( endTime - startTime >= episodeDuration )
                {
                    timeWindows.Add( new TimeWindow() { StartTime = startTime, EndTime = endTime } );
                }
            }
            if ( episodeList.Length > 1 )
            {
                //Check after the last episode
                var lastEpisodeEndTime = episodeList[episodeList.Length - 1].EndTime;
                if ( EndOfDay - lastEpisodeEndTime >= episodeDuration )
                {
                    timeWindows.Add( new TimeWindow() { StartTime = lastEpisodeEndTime, EndTime = EndOfDay } );
                }
            }
            return GiveStartTimeForPrimaryWork(person, timeWindows, episode, rand );
        }
    }
}