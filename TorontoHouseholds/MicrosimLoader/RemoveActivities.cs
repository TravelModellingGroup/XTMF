/*
    Copyright 2022 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Tasha.Common;
using TMG.Tasha.MicrosimLoader;
using XTMF;

namespace TMG.Tasha
{
    [ModuleInformation(Description = "This module is designed to remove trips from loaded households via the Microsim household loader.")]
    public sealed class RemoveActivities : IDataLoader<ITashaHousehold>
    {
        [RootModule]
        public ITravelDemandModel Root;

        [SubModelInformation(Required = true, Description = "The loader that includes trips that we will process.")]
        public IDataLoader<ITashaHousehold> MainLoader;

        [SubModelInformation(Required = true, Description = "Survival Rates for activity.")]
        public IDataSource<SparseArray<float>> PrimaryWorkProfessionalRates;

        [SubModelInformation(Required = true, Description = "Survival Rates for activity.")]
        public IDataSource<SparseArray<float>> PrimaryWorkGeneralRates;

        [SubModelInformation(Required = true, Description = "Survival Rates for activity.")]
        public IDataSource<SparseArray<float>> PrimaryWorkSalesRates;

        [SubModelInformation(Required = true, Description = "Survival Rates for activity.")]
        public IDataSource<SparseArray<float>> PrimaryWorkManufacturingRates;
        
        private SparseArray<float>[] _primaryWork;

        [SubModelInformation(Required = true, Description = "Survival Rates for activity.")]
        public IDataSource<SparseArray<float>> SecondaryWorkRates;
        private SparseArray<float> _secondaryWork;

        [SubModelInformation(Required = true, Description = "Survival Rates for activity.")]
        public IDataSource<SparseArray<float>> WorkBasedBusinessRates;
        private SparseArray<float> _workBasedBusiness;

        [SubModelInformation(Required = true, Description = "Survival Rates for activity.")]
        public IDataSource<SparseArray<float>> SchoolRates;
        private SparseArray<float> _school;

        [SubModelInformation(Required = true, Description = "Survival Rates for activity.")]
        public IDataSource<SparseArray<float>> OtherRates;
        private SparseArray<float> _other;

        [SubModelInformation(Required = true, Description = "Survival Rates for activity.")]
        public IDataSource<SparseArray<float>> MarketRates;
        private SparseArray<float> _market;

        [RunParameter("Random Seed", 12345, "The random number seed to use for this algorithm.")]
        public int RandomSeed;

        public bool OutOfData => MainLoader.OutOfData;

        public int Count => MainLoader.Count;

        public object SyncRoot => MainLoader.SyncRoot;

        public bool IsSynchronized => MainLoader.IsSynchronized;

        public string Name { get; set; }

        public float Progress => MainLoader.Progress;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

        public void CopyTo(ITashaHousehold[] array, int index) => MainLoader.CopyTo(array, index);

        public void CopyTo(Array array, int index) => MainLoader.CopyTo(array, index);

        public IEnumerator<ITashaHousehold> GetEnumerator() => ProcessHouseholds();

        public void LoadData() => MainLoader.LoadData();

        public void Reset() => MainLoader.Reset();

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public ITashaHousehold[] ToArray() => MainLoader.ToArray();

        public bool TryAdd(ITashaHousehold item) => MainLoader.TryAdd(item);

        public bool TryTake(out ITashaHousehold item) => MainLoader.TryTake(out item);

        IEnumerator IEnumerable.GetEnumerator() => ProcessHouseholds();

        /// <summary>
        /// Stream each processed household
        /// </summary>
        /// <returns>An enumerable stream of processed households.</returns>
        private IEnumerator<ITashaHousehold> ProcessHouseholds()
        {
            Random random = new Random(RandomSeed);
            var zoneSystem = Root.ZoneSystem.ZoneArray;
            LoadRates();
            foreach (var household in MainLoader)
            {
                ProcessHousehold(household, random, zoneSystem);
                yield return household;
            }
        }

        /// <summary>
        /// Load in the episode survival rates
        /// </summary>
        private void LoadRates()
        {
            _primaryWork = new SparseArray<float>[4];
            _primaryWork[0] = LoadRates(PrimaryWorkProfessionalRates);
            _primaryWork[1] = LoadRates(PrimaryWorkGeneralRates);
            _primaryWork[2] = LoadRates(PrimaryWorkSalesRates);
            _primaryWork[3] = LoadRates(PrimaryWorkManufacturingRates);
            _secondaryWork = LoadRates(SecondaryWorkRates);
            _workBasedBusiness = LoadRates(WorkBasedBusinessRates);
            _school = LoadRates(SchoolRates);
            _other = LoadRates(OtherRates);
            _market = LoadRates(MarketRates);
        }

        /// <summary>
        /// Get the survival rates from the given data source.
        /// </summary>
        /// <param name="dataSource">The data source to extract from.</param>
        /// <returns>A sparse array with the survival rates.</returns>
        private SparseArray<float> LoadRates(IDataSource<SparseArray<float>> dataSource)
        {
            dataSource.LoadData();
            var ret = dataSource.GiveData();
            dataSource.UnloadData();
            return ret;
        }

        [SubModelInformation(Required = true, Description = "The model for choosing the activity destinations.")]
        public ILocationChoiceModel LocationChoice;

        /// <summary>
        /// Process the household removing activities that do not survive and rebuilding its tours.
        /// </summary>
        /// <param name="household">The household to process.</param>
        /// <param name="random">Our random number generator.</param>
        /// <param name="zoneSystem">The zone system for the model.</param>
        private void ProcessHousehold(ITashaHousehold household, Random random, SparseArray<IZone> zoneSystem)
        {
            var schedule = new Schedule();
            schedule.Episodes = new IEpisode[20];
            int homeZoneIndex = GetHomeZoneIndex(zoneSystem, household);
            RemoveJointTrips(random, household, homeZoneIndex);
            foreach (var person in household.Persons)
            {
                RemoveIndividualTrips(random, person, homeZoneIndex, GetWorkZoneIndex(zoneSystem, person, homeZoneIndex));
                CleanupTripChains(person);
                BuildScheduleFromTripChains(schedule, person.TripChains, person);
                UpdateIndividualLocationChoices(person, schedule, random);
            }
            // TODO: Update household activity destination choices
            foreach(var person in household.Persons)
            {
                RebuildTours(person);
            }
        }

        /// <summary>
        /// Gets the index of work activity rates to use.
        /// </summary>
        /// <param name="occ">The occupation of the person to lookup.</param>
        /// <returns>The index of work activity rates.</returns>
        private static int GetWorkIndex(Occupation occ)
        {
            switch (occ)
            {
                case Occupation.Office:
                    return 1;
                case Occupation.Retail:
                    return 2;
                case Occupation.Manufacturing:
                    return 3;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Gets the flat index for the household's zone.
        /// </summary>
        /// <param name="zoneSystem">The model's zone system.</param>
        /// <param name="household">The household to get the index for.</param>
        /// <returns>The flat index of where the household lives.</returns>
        private static int GetHomeZoneIndex(SparseArray<IZone> zoneSystem, ITashaHousehold household)
            => zoneSystem.GetFlatIndex(household.HomeZone.ZoneNumber);

        /// <summary>
        /// Gets the work zone's flat index for the given person, or returns the home zone index if unavailable.
        /// </summary>
        /// <param name="zoneSystem">The zone system to reference</param>
        /// <param name="person">The person to explore.</param>
        /// <param name="homeZoneIndex">The person's home zone index.</param>
        /// <returns>The index of the person's work zone if available.</returns>
        private static int GetWorkZoneIndex(SparseArray<IZone> zoneSystem, ITashaPerson person, int homeZoneIndex)
        {
            if (person.EmploymentZone?.ZoneNumber is int zone)
            {
                var workIndex = zoneSystem.GetFlatIndex(zone);
                return workIndex >= 0 ? workIndex : homeZoneIndex;
            }
            else
            {
                return homeZoneIndex;
            }
        }

        private void RemoveIndividualTrips(Random random, ITashaPerson person, int homeZoneIndex, int workZoneIndex)
        {
            Predicate<ITrip> testIfWeShouldRemove = 
                (trip => 
                   (trip.Purpose == Activity.PrimaryWork && random.NextDouble() >= _primaryWork[GetWorkIndex(person.Occupation)].GetFlatData()[workZoneIndex])
                || (trip.Purpose == Activity.SecondaryWork && random.NextDouble() >= _secondaryWork.GetFlatData()[homeZoneIndex])
                || (trip.Purpose == Activity.WorkBasedBusiness && random.NextDouble() >= _workBasedBusiness.GetFlatData()[workZoneIndex])
                || (trip.Purpose == Activity.School && random.NextDouble() >= _school.GetFlatData()[homeZoneIndex])
                || (trip.Purpose == Activity.IndividualOther && random.NextDouble() >= _other.GetFlatData()[homeZoneIndex])
                || (trip.Purpose == Activity.Market && random.NextDouble() >= _market.GetFlatData()[homeZoneIndex])
                );
            // Remove all of the work trip chains
            foreach (var tripChain in person.TripChains)
            {
                tripChain.Trips.RemoveAll(testIfWeShouldRemove);
            }
        }

        private void RemoveJointTrips(Random random, ITashaHousehold household, int homeZoneIndex)
        {
            List<ITrip> toRemove = null;
            AccumulateJointTripsToRemove(random, household, homeZoneIndex, ref toRemove);
            RemoveSelectedJointTrips(household, toRemove);
        }

        private void AccumulateJointTripsToRemove(Random random, ITashaHousehold household, int homeZoneIndex, ref List<ITrip> toRemove)
        {
            foreach (var person in household.Persons)
            {
                foreach (var tripChain in person.TripChains)
                {
                    if (tripChain.JointTrip && tripChain.JointTripRep)
                    {
                        var otherTours = tripChain.JointTripChains;
                        var trips = tripChain.Trips;
                        for (int i = 0; i < trips.Count; i++)
                        {
                            if (trips[i].Purpose == Activity.JointOther)
                            {
                                if (random.NextDouble() >= _other.GetFlatData()[homeZoneIndex])
                                {
                                    AddToRemove(ref toRemove, trips[i], otherTours, i);
                                }
                            }
                            else if (trips[i].Purpose == Activity.JointMarket)
                            {
                                if (random.NextDouble() >= _market.GetFlatData()[homeZoneIndex])
                                {
                                    AddToRemove(ref toRemove, trips[i], otherTours, i);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void AddToRemove(ref List<ITrip> toRemove, ITrip trip, List<ITripChain> otherchains, int index)
        {
            toRemove = toRemove ?? new List<ITrip>(4);
            toRemove.Add(trip);
            foreach (var chain in otherchains)
            {
                toRemove.Add(chain.Trips[index]);
            }
        }

        private static void RemoveSelectedJointTrips(ITashaHousehold household, List<ITrip> toRemove)
        {
            if (toRemove != null)
            {
                foreach (var person in household.Persons)
                {
                    foreach (var tripChain in person.TripChains)
                    {
                        var trips = tripChain.Trips;
                        for (int i = 0; i < trips.Count; i++)
                        {
                            if (toRemove.Contains(trips[i]))
                            {
                                trips.RemoveAt(i--);
                            }
                        }
                    }
                }
            }
        }

        private static void CleanupTripChains(ITashaPerson person)
        {
            // Remove all of the empty trip chains
            person.TripChains.RemoveAll(tc => tc.Trips.Count <= 1);
        }

        private void UpdateIndividualLocationChoices(ITashaPerson person, Schedule schedule, Random random)
        {
            int currentEpisode = 0;
            // Update the location choices for Market and Other Trips
            foreach (var tripChain in person.TripChains)
            {
                // Only the representative is going to rebuild the destination choice
                if (tripChain.JointTrip && !tripChain.JointTripRep)
                {
                    currentEpisode += tripChain.Trips.Count;
                    continue;
                }
                // We don't need to rebuild the origin zones at this step
                foreach (var trip in tripChain.Trips)
                {
                    if(trip is Trip t)
                    {
                        if (IsIndividualNWSPurpose(trip.Purpose))
                        {
                            t.DestinationZone = LocationChoice.GetLocation(schedule.Episodes[currentEpisode], random);
                        }
                    }
                    currentEpisode++;
                }
            }
        }

        /// <summary>
        /// Tests if the purpose is an individual other or market trip.
        /// </summary>
        /// <param name="purpose">The purpose to test.</param>
        /// <returns>True if it is an individual other or market trip.</returns>
        private static bool IsIndividualNWSPurpose(Activity purpose)
        {
            switch(purpose)
            {
                case Activity.IndividualOther:
                case Activity.Market:
                    return true;
                default:
                    return false;
            }
        }

        private void BuildScheduleFromTripChains(Schedule schedule, List<ITripChain> tripChains, ITashaPerson person)
        {
            var episodes = schedule.Episodes;
            int position = 0;
            foreach (var tc in tripChains)
            {
                var trips = tc.Trips;
                // Check if we are going to overflow, if we are expand
                UpdateArrayLength(schedule, ref episodes, position + trips.Count);
                for (int i = 0; i < trips.Count - 1; i++)
                {
                    var duration = trips[i + 1].TripStartTime - trips[i].ActivityStartTime;
                    episodes[position++] = new Episode()
                    {
                        ActivityType = trips[i].Purpose,
                        StartTime = trips[i].ActivityStartTime,
                        Duration = duration,
                        EndTime = trips[i].ActivityStartTime + duration,
                        OriginalDuration = duration,
                        ContainingSchedule = schedule,
                        Owner = person,
                        TravelTime = trips[i].TravelTime,
                        Zone = trips[i].DestinationZone
                    };
                }
            }
            // Clear out the remaining slots
            Array.Clear(episodes, position, episodes.Length - position);
        }

        private static void RebuildTours(ITashaPerson person)
        {
            // Stitch the remaining trip chains back together
            foreach (var tripChain in person.TripChains)
            {
                for (int i = 1; i < tripChain.Trips.Count; i++)
                {
                    ((ActivityPurposeTrip)tripChain.Trips[i - 1]).DestinationZone = tripChain.Trips[i].OriginalZone;
                }
            }
        }

        private static void UpdateArrayLength(Schedule schedule, ref IEpisode[] episodes, int currentPosition)
        {
            if (currentPosition >= episodes.Length)
            {
                Array.Resize(ref episodes, episodes.Length * 2);
                schedule.Episodes = episodes;
            }
        }
    }
}
