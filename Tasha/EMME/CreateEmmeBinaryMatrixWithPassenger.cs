/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using TMG.Functions;

namespace Tasha.EMME
{
    public sealed class CreateEmmeBinaryMatrixWithPassenger : IPostHouseholdIteration
    {
        [RootModule]
        public ITravelDemandModel Root;
        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get; set; }

        private float[][] Matrix;

        [SubModelInformation(Required = true, Description = "The location to save the SOV matrix.")]
        public FileLocation MatrixSaveLocation;

        [RunParameter("Start Time", "6:00AM", typeof(Time), "The start of the time to record.")]
        public Time StartTime;

        [RunParameter("End Time", "9:00AM", typeof(Time), "The end of the time to record (non inclusive).")]
        public Time EndTime;

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
            // now execute
            var persons = household.Persons;
            for (int i = 0; i < persons.Length; i++)
            {
                var expFactor = persons[i].ExpansionFactor;
                var tripChains = persons[i].TripChains;

                for (int j = 0; j < tripChains.Count; j++)
                {
                    if ((tripChains[j].EndTime < StartTime) | (tripChains[j].StartTime) > EndTime)
                    {
                        continue;
                    }
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
                            var driverOrigin = GetFlatIndex(driversTrip.OriginalZone);
                            var passengerOrigin = GetFlatIndex(trips[k].OriginalZone);
                            var passengerDestination = GetFlatIndex(trips[k].DestinationZone);
                            var driverDestination = GetFlatIndex(driversTrip.DestinationZone);

                            var driverTripChain = driversTrip.TripChain;
                            var driverOnJoint = driverTripChain.JointTrip;
                            float driverExpansionFactor = driverTripChain.Person.ExpansionFactor;
                            // subtract out the old data
                            if (IsDriverAlreadyOnRoad(driversTrip))
                            {
                                AddToMatrix(startTime, -driverExpansionFactor, driverOrigin, driverDestination);
                            }
                            // add in our 3 trip leg data
                            if (driverOrigin != passengerOrigin)
                            {
                                // this really is driver on joint
                                AddToMatrix(startTime, driverExpansionFactor, driverOrigin, passengerOrigin);
                            }
                            AddToMatrix(startTime, driverExpansionFactor, passengerOrigin, passengerDestination);
                            if (passengerDestination != driverDestination)
                            {
                                AddToMatrix(startTime, driverExpansionFactor, passengerDestination, driverDestination);
                            }
                        }
                        else if ((accessModeIndex = UsesAccessMode(modeChosen)) >= 0)
                        {
                            IZone origin, destination;
                            if (AccessModes[accessModeIndex].GetTranslatedOD(tripChains[j], trips[k], initialAccessTrip, out origin, out destination))
                            {
                                var originIndex = GetFlatIndex(origin);
                                var destinationIndex = GetFlatIndex(destination);
                                AddToMatrix(startTime, expFactor, originIndex, destinationIndex);
                            }
                            initialAccessTrip = false;
                        }
                        else if (IsThisModeOneWeShouldCount(modeChosen))
                        {
                            var originIndex = GetFlatIndex(trips[k].OriginalZone);
                            var destinationIndex = GetFlatIndex(trips[k].DestinationZone);
                            AddToMatrix(startTime, expFactor, originIndex, destinationIndex);
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
        private void AddToMatrix(Time startTime, float expFactor, int originIndex, int destinationIndex)
        {
            if (startTime >= StartTime & startTime < EndTime)
            {
                var row = Matrix[originIndex];
                bool gotLock = false;
                WriteLock.Enter(ref gotLock);
                row[destinationIndex] += expFactor;
                if (gotLock) WriteLock.Exit(true);
            }
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

        public void IterationFinished(int iteration, int totalIterations)
        {
            //Min each OD to Zero
            MinZero(Matrix);
            if (SpecialGenerators.Length > 0)
            {
                var specialGenerationResults = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>().GetFlatData();
                // Apply the special generators
                for (int i = 0; i < SpecialGenerators.Length; i++)
                {
                    SpecialGenerators[i].IncludeTally(specialGenerationResults);
                }
                // Now scale the by household iterations and integrate it back into the result matrix
                Parallel.For(0, specialGenerationResults.Length, (int i) =>
                {
                    VectorHelper.Multiply(specialGenerationResults[i], 0, specialGenerationResults[i], 0, HouseholdIterations, specialGenerationResults[i].Length);
                    VectorHelper.Add(Matrix[i], 0, Matrix[i], 0, specialGenerationResults[i], 0, specialGenerationResults.Length);
                });
            }
            // write to disk
            new EmmeMatrix(ZoneSystem, Matrix).Save(MatrixSaveLocation, true);
        }

        private void MinZero(float[][] matrix)
        {
            Parallel.For(0, matrix.Length, (int i) =>
            {
                var row = matrix[i];
                for (int j = 0; j < row.Length; j++)
                {
                    row[j] = Math.Max(row[j], 0.0f);
                }
            });
        }

        public void Load(int maxIterations)
        {

        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void HouseholdComplete(ITashaHousehold household, bool success)
        {

        }

        public void HouseholdStart(ITashaHousehold household, int householdIterations)
        {

        }
    }
}
