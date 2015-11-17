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
using TMG.Emme;
using XTMF;
using Tasha.Common;
using TMG;
using Datastructure;
using TMG.Input;
using System.Threading;

namespace Tasha.EMME
{
    public sealed class CreateEmmeBinaryMatrix : IPostHousehold, IPostHouseholdIteration
    {
        [RootModule]
        public ITravelDemandModel Root;
        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get; set; }

        private float[][] Matrix;

        [SubModelInformation(Required = true, Description = "The location to save the matrix.")]
        public FileLocation MatrixSaveLocation;

        [RunParameter("Start Time", "6:00AM", typeof(Time), "The start of the time to record.")]
        public Time StartTime;

        [RunParameter("End Time", "9:00AM", typeof(Time), "The end of the time to record (non inclusive).")]
        public Time EndTime;

        private SpinLock WriteLock = new SpinLock(false);

        [RunParameter("Minimum Age", 0, "The minimum age a person needs to be in order to be included in the demand.")]
        public int MinimumAge;

        public void HouseholdComplete(ITashaHousehold household, bool success)
        {

        }

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {
            Execute(household, hhldIteration);
        }

        public void HouseholdStart(ITashaHousehold household, int householdIterations)
        {

        }

        public void Execute(ITashaHousehold household, int iteration)
        {
            var persons = household.Persons;
            for (int i = 0; i < persons.Length; i++)
            {
                if (persons[i].Age < MinimumAge)
                {
                    continue;
                }
                var expFactor = persons[i].ExpansionFactor;
                var tripChains = persons[i].TripChains;
                for (int j = 0; j < tripChains.Count; j++)
                {
                    if ((tripChains[j].EndTime < StartTime) | (tripChains[j].StartTime) > EndTime)
                    {
                        continue;
                    }
                    var trips = tripChains[j].Trips;
                    bool access = true;
                    for (int k = 0; k < trips.Count; k++)
                    {
                        var startTime = trips[k].TripStartTime;
                        var modeChosen = trips[k].Mode;
                        if (UsesMode(modeChosen))
                        {
                            AddToMatrix(expFactor, startTime, trips[k].OriginalZone, trips[k].DestinationZone);
                        }
                        else
                        {
                            for (int l = 0; l < AccessModes.Length; l++)
                            {
                                if (UsesAccessMode(modeChosen))
                                {
                                    IZone origin, destination;
                                    if (AccessModes[l].GetTranslatedOD(tripChains[j], trips[k], access, out origin, out destination))
                                    {
                                        AddToMatrix(expFactor, startTime, origin, destination);
                                    }
                                    access = false;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void AddToMatrix(float expFactor, Time startTime, IZone origin, IZone destination)
        {
            if (startTime >= StartTime & startTime < EndTime)
            {
                var originIndex = ZoneSystem.GetFlatIndex(origin.ZoneNumber);
                var destinationIndex = ZoneSystem.GetFlatIndex(destination.ZoneNumber);
                bool gotLock = false;
                WriteLock.Enter(ref gotLock);
                Matrix[originIndex][destinationIndex] += expFactor;
                if (gotLock) WriteLock.Exit(true);
            }
        }

        public sealed class ModeLink : IModule
        {
            [RootModule]
            public ITashaRuntime Root;

            [RunParameter("Mode Name", "Auto", "The name of the mode")]
            public string ModeName;

            internal ITashaMode Mode;

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

            [RunParameter("Count Access", true, "True to count for access, false to count for egress.")]
            public bool CountAccess;

            [RunParameter("Access Tag Name", "AccessStation", "The tag used for storing the zone used for access.")]
            public string AccessZoneTagName;

            internal ITashaMode Mode;

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public bool GetTranslatedOD(ITripChain chain, ITrip trip, bool access, out IZone origin, out IZone destination)
            {
                if (CountAccess ^ (!access))
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

        [SubModelInformation(Required = false, Description = "The access modes to listen for.")]
        public AccessModeLink[] AccessModes;

        /// <summary>
        /// check to see if the mode being used for this trip is one that we are interested in.
        /// </summary>
        /// <param name="trip"></param>
        /// <returns></returns>
        private bool UsesMode(ITashaMode mode)
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

        private bool UsesAccessMode(ITashaMode mode)
        {
            for (int i = 0; i < AccessModes.Length; i++)
            {
                if (AccessModes[i].Mode == mode)
                {
                    return true;
                }
            }
            return false;
        }

        private SparseArray<IZone> ZoneSystem;
        private int NumberOfZones;

        public void IterationStarting(int iteration, int totalIterations)
        {
            IterationStarting(iteration);
        }

        public void IterationFinished(int iteration, int totalIterations)
        {
            IterationFinished(iteration);
        }

        public void IterationStarting(int iteration)
        {
            // get the newest zone system
            ZoneSystem = Root.ZoneSystem.ZoneArray;
            NumberOfZones = ZoneSystem.Count;
            if (Matrix == null)
            {
                Matrix = new float[NumberOfZones][];
                for (int i = 0; i < Matrix.Length; i++)
                {
                    Matrix[i] = new float[NumberOfZones];
                }
            }
            else
            {
                // clear out old trips
                for (int i = 0; i < Matrix.Length; i++)
                {
                    Array.Clear(Matrix[i], 0, Matrix[i].Length);
                }
            }
        }

        public IModeAggregationTally[] SpecialGenerators;

        public void IterationFinished(int iteration)
        {
            // Apply the special generators
            for (int i = 0; i < SpecialGenerators.Length; i++)
            {
                SpecialGenerators[i].IncludeTally(Matrix);
            }
            // write to disk
            new EmmeMatrix(ZoneSystem, Matrix).Save(MatrixSaveLocation, true);
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
