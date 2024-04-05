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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Tasha.Common;
using TMG;
using TMG.Input;
using XTMF;

namespace Tasha.Scheduler
{
    public class MakingNumberOfAdultDistributions : ITashaRuntime
    {
        public static int MaxFrequency;

        public static int NumberOfAdultDistributions;

        public static int NumberOfAdultFrequencies;

        [RunParameter("FullTimeActivity", "4:40", typeof(Time), "The highest number of attempts to schedule an episode")]
        public Time FullTimeActivityDateTime;

        [RunParameter("Max Frequency", 10, "The highest frequency number.")]
        public int MaxFrequencyLocal;

        [RunParameter("MaxPrimeWorkStartTimeForReturnHomeFromWork", "12:00", typeof(Time), "The number of start time quantums for the distributions")]
        public Time MaxPrimeWorkStartTimeForReturnHomeFromWorkDateTime;

        [RunParameter("MinPrimaryWorkDurationForReturnHomeFromWork", "2:00", typeof(Time), "The number of start time quantums for the distributions")]
        public Time MinPrimaryWorkDurationForReturnHomeFromWorkDateTime;

        [RunParameter("Number of Adults", 9, "The maximum number of adults to process for.")]
        public int NumberOfAdults;

        [RunParameter("NumberOfAdultDistributions", 6, "The total number of distributions for adults.")]
        public int NumberOfAdultDistributionsLocal;

        [RunParameter("NumberOfAdultFrequencies", 9, "The total number of frequencies for adults.")]
        public int NumberOfAdultFrequenciesLocal;

        [RunParameter("#OfDistributions", 262, "The number of distributions")]
        public int NumberOfDistributionsLocal;

        [RunParameter("Estimated Households", 148112, "A Guess at the number of households (for progress)")]
        public int NumberOfHouseholds;

        [SubModelInformation(Description = "Primary Mode used for travel times", Required = true)]
        public ITashaMode PrimaryMode;

        [SubModelInformation(Required = true, Description = "The Output File.")]
        public FileLocation ResultFile;

        [RunParameter("ReturnHomeFromWorkMaxEndTime", "15:00", typeof(Time), "The number of start time quantums for the distributions")]
        public Time ReturnHomeFromWorkMaxEndTimeDateTime;

        [RunParameter("SecondaryWorkMinStartTime", "15:00", typeof(Time), "The number of start time quantums for the distributions")]
        public Time SecondaryWorkMinStartTimeDateTime;

        [RunParameter("SecondaryWork Threshold", "19:00", typeof(Time), "The highest number of attempts to schedule an episode")]
        public Time SecondaryWorkThresholdDateTime;

        [RunParameter("Start Time Quantums", 96, "The number of start time quantums.")]
        public int StartTimeQuantums;

        private int CurrentHousehold;

        private string Status = "Initializing!";

        [DoNotAutomate]
        public List<ITashaMode> AllModes { get; set; }

        [DoNotAutomate]
        public ITashaMode AutoMode { get; set; }

        [SubModelInformation(Description = "The type of vehicle that auto is", Required = true)]
        public IVehicleType AutoType { get; set; }

        [RunParameter("End of Day", "28:00", typeof(Time), "The time that Tasha will end at.")]
        public Time EndOfDay { get; set; }

        [SubModelInformation(Description = "The model that will load our household", Required = true)]
        public IDataLoader<ITashaHousehold> HouseholdLoader { get; set; }

        [RunParameter("Input Directory", "../../Input", "The directory that the input files will be in.")]
        public string InputBaseDirectory { get; set; }

        [RunParameter("Number of Iterations", 1, "How many iterations do you want?")]
        public int TotalIterations { get; set; }

        [RunParameter("JointMarketInflation", 1f, "Inflate observed joint market activities to generate more episodes")]
        public float JointMarketInflation { get; set; }

        [RunParameter("JointOtherInflation", 1f, "Inflate observed joint other activities to generate more episodes")]
        public float JointOtherInflation { get; set; }

        [RunParameter("MarektInflation", 1f, "Inflate observed individual market activities to generate more episodes")]
        public float MarketInflation { get; set; }

        [DoNotAutomate]
        public ITashaModeChoice ModeChoice { get; set; }

        public string Name
        {
            get;
            set;
        }

        [SubModelInformation(Description = "Network Data", Required = false)]
        public IList<INetworkData> NetworkData { get; set; }

        [DoNotAutomate]
        public List<ITashaMode> NonSharedModes { get; set; }

        [RunParameter("OtherInflation", 1f, "Inflate observed individual other activities to generate more episodes")]
        public float OtherInflation { get; set; }

        [DoNotAutomate]
        public List<ITashaMode> OtherModes { get; set; }

        public string OutputBaseDirectory { get; set; }

        [RunParameter("Parallel", false, "Should we run in Parallel?")]
        public bool Parallel { get; set; }

        [DoNotAutomate]
        public List<IPostHousehold> PostHousehold { get; set; }

        [DoNotAutomate]
        public List<IPostIteration> PostIteration { get; set; }

        [DoNotAutomate]
        public List<ISelfContainedModule> PostRun { get; set; }

        [DoNotAutomate]
        public List<IPostScheduler> PostScheduler { get; set; }

        [DoNotAutomate]
        public List<IPreIteration> PreIteration { get; set; }

        [DoNotAutomate]
        public List<ISelfContainedModule> PreRun { get; set; }

        public float Progress
        {
            get { return ( (float)CurrentHousehold / NumberOfHouseholds ); }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>( 32, 76, 169 ); }
        }

        [RunParameter("Random Seed", 12345, "The seed for the random number generator")]
        public int RandomSeed { get; set; }

        [SubModelInformation(Description = "The available resources for this model system.", Required = false)]
        public List<IResource> Resources { get; set; }

        [RunParameter("ReturnFromWorkInflation", 1f, "Inflate observed return home from work activities to generate more episodes")]
        public float ReturnFromWorkInflation { get; set; }

        [RunParameter("SecondaryWorkInflation", 1f, "Inflate observed secondary work activities to generate more episodes")]
        public float SecondaryWorkInflation { get; set; }

        [DoNotAutomate]
        public List<ISharedMode> SharedModes { get; set; }

        [RunParameter("Start of Day", "4:00", typeof(Time), "The time that Tasha will start at.")]
        public Time StartOfDay { get; set; }

        [SubModelInformation(Description = "A collection of vehicles that are used by the modes", Required = false)]
        public List<IVehicleType> VehicleTypes { get; set; }

        [RunParameter("WorkAtHomeInflation", 1f, "Inflate observed work at home activities to generate more episodes")]
        public float WorkAtHomeInflation { get; set; }

        [RunParameter("WorkBusinessInflation", 1f, "Inflate observed work business activities to generate more episodes")]
        public float WorkBusinessInflation { get; set; }

        [SubModelInformation(Description = "Zone System", Required = true)]
        public IZoneSystem ZoneSystem { get; set; }

        public ITrip CreateTrip(ITripChain chain, IZone originalZone, IZone destinationZone, Activity purpose, Time startTime)
        {
            throw new NotImplementedException();
        }

        public bool ExitRequest()
        {
            return false;
        }

        [DoNotAutomate]
        public int GetIndexOfMode(ITashaMode mode)
        {
            throw new NotImplementedException();
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            Status = "Loading Data";

            ZoneSystem.LoadData();

            if ( PostHousehold != null )
            {
                foreach ( var module in PostHousehold )
                {
                    module.Load( TotalIterations );
                }
            }

            for ( int i = 0; i < TotalIterations; i++ )
            {
                CurrentHousehold = 0;
                HouseholdLoader.LoadData();
                RunIteration( i );
            }

            if ( PostRun != null )
            {
                foreach ( var module in PostRun )
                {
                    module.Start();
                }
            }
            ZoneSystem.UnloadData();
        }

        public override string ToString()
        {
            return Status;
        }

        private void AssignEpisodes(ITashaPerson person, ref Time workStartTime, ref Time workEndTime, Random random)
        {
            person.InitializePersonalProjects();
            var personData = (SchedulerPersonData)person["SData"];

            foreach ( var tripChain in person.TripChains )
            {
                for ( int j = 0; j < ( tripChain.Trips.Count - 1 ); j++ )
                {
                    var thisTrip = tripChain.Trips[j];
                    var nextTrip = tripChain.Trips[j + 1];
                    thisTrip.Mode = PrimaryMode;
                    var startTime = thisTrip.OriginalZone == null || thisTrip.DestinationZone == null ? thisTrip.TripStartTime : thisTrip.ActivityStartTime;
                    var endTime = nextTrip.TripStartTime;
                    var duration = endTime - startTime;
                    if ( duration < Time.Zero )
                    {
                        endTime = Time.EndOfDay;
                    }
                    if ( endTime < startTime )
                    {
                        startTime = thisTrip.TripStartTime;
                    }

                    if ( thisTrip.Purpose == Activity.PrimaryWork || thisTrip.Purpose == Activity.SecondaryWork || thisTrip.Purpose == Activity.WorkBasedBusiness )
                    {
                        var newEpisode = new ActivityEpisode(new TimeWindow( startTime, endTime ), thisTrip.Purpose, tripChain.Person );
                        newEpisode.Zone = thisTrip.DestinationZone;
                        if ( thisTrip.Purpose == Activity.PrimaryWork )
                        {
                            if ( workStartTime == Time.Zero || newEpisode.StartTime < workStartTime )
                            {
                                workStartTime = newEpisode.StartTime;
                            }
                            if ( workEndTime == Time.Zero || newEpisode.EndTime > workEndTime )
                            {
                                workEndTime = newEpisode.EndTime;
                            }
                        }
                        personData.WorkSchedule.Schedule.Insert( newEpisode, random );
                    }
                    if ( thisTrip.Purpose == Activity.School )
                    {
                        var newEpisode = new ActivityEpisode(new TimeWindow( startTime, endTime ), thisTrip.Purpose, tripChain.Person );
                        newEpisode.Zone = thisTrip.DestinationZone;
                        personData.SchoolSchedule.Schedule.Insert( newEpisode, random );
                    }
                }
            }
        }

        private void Run(ITashaHousehold household, BlockingCollection<Result> resultList)
        {
            var numberOfPeople = household.Persons.Length;
            if ( household.NumberOfAdults >= 2 )
            {
                Time[] workStartTimes = new Time[numberOfPeople];
                Time[] workEndTimes = new Time[numberOfPeople];
                for ( int i = 0; i < numberOfPeople; i++ )
                {
                    AssignEpisodes( household.Persons[i], ref workStartTimes[i], ref workEndTimes[i], null );
                }
                int[] jointMarket = new int[NumberOfAdults];
                int[] jointOther = new int[NumberOfAdults];
                bool any = false;
                foreach ( var person in household.Persons )
                {
                    foreach ( var tc in person.TripChains )
                    {
                        if ( tc.JointTripRep )
                        {
                            var chains = tc.JointTripChains;
                            int adults = 0;
                            for ( int i = 0; i < chains.Count; i++ )
                            {
                                if ( chains[i].Person.Adult )
                                {
                                    adults++;
                                }
                            }
                            any = true;
                            adults = adults > NumberOfAdults ? NumberOfAdults : adults;
                            foreach ( var trip in tc.Trips )
                            {
                                switch ( trip.Purpose )
                                {
                                    case Activity.JointOther:
                                        jointOther[adults]++;
                                        break;
                                    case Activity.JointMarket:
                                        jointMarket[adults]++;
                                        break;
                                }
                            }
                        }
                    }
                }
                if ( any )
                {
                    resultList.Add( new Result()
                    {
                        Adults = household.NumberOfAdults,
                        Children = household.Persons.Length - household.NumberOfAdults > 0,
                        ExpansionFactor = household.ExpansionFactor,
                        JointMarketAdults = jointMarket,
                        JointOthersAdults = jointOther
                    } );
                }
            }
            System.Threading.Interlocked.Increment( ref CurrentHousehold );
            household.Recycle();
        }

        private void RunIteration(int i)
        {
            if ( NetworkData != null )
            {
                System.Threading.Tasks.Parallel.ForEach( NetworkData,
                    delegate (INetworkData network)
                {
                    network.LoadData();
                } );
            }

            if ( PostScheduler != null )
            {
                foreach ( var module in PostScheduler )
                {
                    module.IterationStarting( i );
                }
            }

            if ( PostHousehold != null )
            {
                foreach ( var module in PostHousehold )
                {
                    module.IterationStarting( i );
                }
            }

            RunSerial();

            if ( NetworkData != null )
            {
                foreach ( var network in NetworkData )
                {
                    network.UnloadData();
                }
            }

            if ( PostScheduler != null )
            {
                foreach ( var module in PostScheduler )
                {
                    module.IterationFinished( i );
                }
            }

            if ( PostHousehold != null )
            {
                foreach ( var module in PostHousehold )
                {
                    module.IterationFinished( i );
                }
            }
            HouseholdLoader.Reset();
        }

        private struct Result
        {
            internal int Adults;
            internal bool Children;
            internal float ExpansionFactor;
            internal int[] JointOthersAdults;
            internal int[] JointMarketAdults;
        }

        private void RunSerial()
        {
            Status = "Calculating Distributions...";
            var households = HouseholdLoader.ToArray();
            var resultList = new BlockingCollection<Result>();
            var saveResultsTask = System.Threading.Tasks.Task.Factory.StartNew( () =>
                {
                    // setup data
                    float[][] combinedResults = new float[6][];
                    for ( int i = 0; i < combinedResults.Length; i++ )
                    {
                        combinedResults[i] = new float[NumberOfAdults];
                    }
                    //process results
                    foreach ( var result in resultList.GetConsumingEnumerable() )
                    {
                        int offset;
                        if ( result.Children )
                        {
                            offset = result.Adults >= 3 ? 0 : 1;
                        }
                        else
                        {
                            offset = 2;
                        }
                        var jointOther = combinedResults[offset];
                        for ( int i = 0; i < jointOther.Length; i++ )
                        {
                            jointOther[i] += result.JointOthersAdults[i] * result.ExpansionFactor;
                        }
                        var jointMarket = combinedResults[offset + 3];
                        for ( int i = 0; i < jointOther.Length; i++ )
                        {
                            jointMarket[i] += result.JointMarketAdults[i] * result.ExpansionFactor;
                        }
                    }
                    // save results
                    //NO HEADER
                    //DistributionID,NumberOfAdults,ExpandedSum,Probability,CDF
                    using StreamWriter writer = new(ResultFile.GetFilePath());
                    for (int dist = 0; dist < combinedResults.Length; dist++)
                    {
                        var cdf = 0.0;
                        var factor = 0.0;
                        // get sum of expansions
                        for (int i = 0; i < combinedResults[dist].Length; i++)
                        {
                            factor += combinedResults[dist][i];
                        }
                        // compute factor
                        factor = 1 / factor;
                        if (double.IsInfinity(factor) | double.IsNaN(factor))
                        {
                            factor = 0;
                        }
                        for (int i = 0; i < combinedResults[dist].Length; i++)
                        {
                            var probability = combinedResults[dist][i] * factor;
                            cdf += probability;
                            writer.Write(dist);
                            writer.Write(',');
                            writer.Write(i);
                            writer.Write(',');
                            writer.Write(combinedResults[dist][i]);
                            writer.Write(',');
                            writer.Write(combinedResults[dist][i] * factor);
                            writer.Write(',');
                            writer.WriteLine(cdf);
                        }
                    }
                } );
            System.Threading.Tasks.Parallel.For( 0, households.Length, i =>
            {
                Run( households[i], resultList );
            } );
            resultList.CompleteAdding();
            saveResultsTask.Wait();
        }
    }
}