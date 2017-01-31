/*
    Copyright 2016 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Threading.Tasks;
using Tasha.Common;
using System.IO;
using TMG;
using Datastructure;
using XTMF;
using System.Runtime.CompilerServices;
using System.Threading;
using TMG.Input;

namespace Tasha.Emissions
{

    public class GatherAutoEmissionsTripRecord : IPostHouseholdIteration
    {
        private class Index
        {
            internal readonly IZone Home;
            internal readonly IZone Origin;
            internal readonly IZone Destination;
            internal readonly int StartHour;

            public Index(IZone home, IZone origin, IZone destination, int startHour)
            {
                Home = home;
                Origin = origin;
                Destination = destination;
                StartHour = startHour;
            }

            public override bool Equals(object obj)
            {
                var other = obj as Index;
                if (other != null)
                {
                    return other.Origin == Origin && other.Destination == Destination && other.StartHour == StartHour;
                }
                return false;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 53124123;
                    // 1447 is prime
                    hash = (hash * 1447) ^ StartHour.GetHashCode();
                    hash = (hash * 1447) ^ Home.GetHashCode();
                    hash = (hash * 1447) ^ Origin.GetHashCode();
                    hash = (hash * 1447) ^ Destination.GetHashCode();
                    return hash;
                }
            }
        }
        private Dictionary<Index, float> Data;

        [RootModule]
        public ITravelDemandModel Root;
        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get; set; }

        [SubModelInformation(Required = true, Description = "CSV: Home,Origin,Destination,Mode,StartHour,ExpandedPersons.")]
        public FileLocation SaveTo;

        private SpinLock WriteLock = new SpinLock(false);

        private int GetFlatIndex(IZone zone)
        {
            return ZoneSystem.GetFlatIndex(zone.ZoneNumber);
        }

        private int HouseholdIterations = 1;

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {
            // Gather the number of household iterations
            HouseholdIterations = totalHouseholdIterations;
            var homeZone = household.HomeZone;
            // now execute
            var persons = household.Persons;
            for (int i = 0; i < persons.Length; i++)
            {
                var expFactor = persons[i].ExpansionFactor;
                var tripChains = persons[i].TripChains;

                for (int j = 0; j < tripChains.Count; j++)
                {
                    var jointTour = tripChains[j].JointTrip;
                    if (tripChains[j].JointTrip && !tripChains[j].JointTripRep)
                    {
                        continue;
                    }
                    var trips = tripChains[j].Trips;
                    // check to see if we should be running access or egress for this person on their trip chain
                    bool initialAccessTrip = true;
                    for (int k = 0; k < trips.Count; k++)
                    {
                        var startTime = trips[k].TripStartTime;
                        int accessModeIndex = -1;
                        var modeChosen = trips[k].Mode;
                        if (Passenger.Mode == modeChosen)
                        {

                            var driversTrip = trips[k]["Driver"] as ITrip;
                            // driver originData
                            var driverOrigin = driversTrip.OriginalZone;
                            var passengerOrigin = trips[k].OriginalZone;
                            var passengerDestination = trips[k].DestinationZone;
                            var driverDestination = driversTrip.DestinationZone;

                            var driverTripChain = driversTrip.TripChain;
                            var driverOnJoint = driverTripChain.JointTrip;
                            float driverExpansionFactor = driverTripChain.Person.ExpansionFactor;
                            // subtract out the old data
                            if (IsDriverAlreadyOnRoad(driversTrip))
                            {
                                AddToMatrix(startTime, -driverExpansionFactor, driverOrigin, driverDestination, homeZone);
                            }
                            // add in our 3 trip leg data
                            if (driverOrigin != passengerOrigin)
                            {
                                // this really is driver on joint
                                AddToMatrix(startTime, driverExpansionFactor, driverOrigin, passengerOrigin, homeZone);
                            }
                            AddToMatrix(startTime, driverExpansionFactor, passengerOrigin, passengerDestination, homeZone);
                            if (passengerDestination != driverDestination)
                            {
                                AddToMatrix(startTime, driverExpansionFactor, passengerDestination, driverDestination, homeZone);
                            }
                        }
                        else if ((accessModeIndex = UsesAccessMode(modeChosen)) >= 0)
                        {
                            IZone origin, destination;
                            if (AccessModes[accessModeIndex].GetTranslatedOD(tripChains[j], trips[k], initialAccessTrip, out origin, out destination))
                            {
                                AddToMatrix(startTime, expFactor, origin, destination, homeZone);
                            }
                            initialAccessTrip = false;
                        }
                        else if (IsThisModeOneWeShouldCount(modeChosen))
                        {
                            AddToMatrix(startTime, expFactor, trips[k].OriginalZone, trips[k].DestinationZone, homeZone);
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsDriverAlreadyOnRoad(ITrip driversTrip)
        {
            // The pure passenger trip chain only has 1 trip
            // And all other trip chains need to have at least 2
            return driversTrip.TripChain.Trips.Count > 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddToMatrix(Time startTime, float expFactor, IZone origin, IZone destination, IZone homeZone)
        {
            var hour = startTime.Hours;
            var index = new Index(homeZone, origin, destination, hour);
            bool gotLock = false;
            WriteLock.Enter(ref gotLock);
            float previous;
            Data.TryGetValue(index, out previous);
            Data[index] = previous + expFactor;
            if (gotLock) WriteLock.Exit(true);
        }

        public sealed class ModeLink : IModule
        {
            [RootModule]
            public ITashaRuntime Root;

            [RunParameter("Mode Name", "Auto", "The name of the mode")]
            public string ModeName;

            [DoNotAutomate]
            public ITashaMode Mode;

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public bool RuntimeValidation(ref string error)
            {
                foreach (var mode in Root.AllModes)
                {
                    if (mode.ModeName == ModeName)
                    {
                        Mode = mode;
                        return true;
                    }
                }
                error = "In '" + Name + "' we were unable to find a mode called '" + ModeName + "'";
                return false;
            }
        }

        public sealed class AccessModeLink : IModule
        {
            [RootModule]
            public ITashaRuntime Root;

            [RunParameter("Mode Name", "DAT", "The name of the mode")]
            public string ModeName;

            [RunParameter("Count Access", true, "True to _Count for access, false to _Count for egress.")]
            public bool CountAccess;

            [RunParameter("Access Tag Name", "AccessStation", "The tag used for storing the zone used for access.")]
            public string AccessZoneTagName;

            internal ITashaMode Mode;

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool GetTranslatedOD(ITripChain chain, ITrip trip, bool initialTrip, out IZone origin, out IZone destination)
            {
                if (CountAccess ^ (!initialTrip))
                {
                    origin = trip.OriginalZone;
                    destination = chain[AccessZoneTagName] as IZone;
                    return destination != null;
                }
                else
                {
                    origin = chain[AccessZoneTagName] as IZone;
                    destination = trip.DestinationZone;
                    return origin != null;
                }
            }

            public bool RuntimeValidation(ref string error)
            {
                foreach (var mode in Root.AllModes)
                {
                    if (mode.ModeName == ModeName)
                    {
                        Mode = mode;
                        return true;
                    }
                }
                error = "In '" + Name + "' we were unable to find a mode called '" + ModeName + "'";
                return false;
            }
        }

        [SubModelInformation(Required = false, Description = "The modes to listen for.")]
        public ModeLink[] Modes;

        [SubModelInformation(Required = true, Description = "The link to the passenger mode.")]
        public ModeLink Passenger;

        [SubModelInformation(Required = false, Description = "The access modes to listen for.")]
        public AccessModeLink[] AccessModes;

        /// <summary>
        /// check to see if the mode being used for this trip is one that we are interested in.
        /// </summary>
        /// <param name="trip"></param>
        /// <returns></returns>
        private bool IsThisModeOneWeShouldCount(ITashaMode mode)
        {
            for (int i = 0; i < Modes.Length; i++)
            {
                if (Modes[i].Mode == mode)
                {
                    return true;
                }
            }
            return false;
        }

        private int UsesAccessMode(ITashaMode mode)
        {
            for (int i = 0; i < AccessModes.Length; i++)
            {
                if (AccessModes[i].Mode == mode)
                {
                    return i;
                }
            }
            return -1;
        }

        private SparseArray<IZone> ZoneSystem;
        private int NumberOfZones;

        public void IterationStarting(int iteration, int totalIterations)
        {
            // get the newest zone system
            ZoneSystem = Root.ZoneSystem.ZoneArray;
            NumberOfZones = ZoneSystem.Count;
            Data = new Dictionary<Index, float>();
        }

        public void IterationFinished(int iteration, int totalIterations)
        {
            using (var writer = new StreamWriter(SaveTo))
            {
                writer.WriteLine("HomeZone,Origin,Destination,StartHour,ExpandedPersons");
                foreach(var entry in from rec in Data
                                     orderby rec.Key.Home.ZoneNumber,
                                        rec.Key.Origin.ZoneNumber,
                                        rec.Key.Destination.ZoneNumber,
                                        rec.Key.StartHour
                                     select rec)
                {
                    var index = entry.Key;
                    writer.Write(index.Home.ZoneNumber);
                    writer.Write(',');
                    writer.Write(index.Origin.ZoneNumber);
                    writer.Write(',');
                    writer.Write(index.Destination.ZoneNumber);
                    writer.Write(',');
                    writer.Write(index.StartHour);
                    writer.Write(',');
                    writer.WriteLine(entry.Value);
                }
            }
            Data.Clear();
        }

        public void HouseholdComplete(ITashaHousehold household, bool success)
        {
        }

        public void HouseholdStart(ITashaHousehold household, int householdIterations)
        {
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

}
