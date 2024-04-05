/*
    Copyright 2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;
using Tasha.Common;
using TMG.Input;
using TMG;
using Datastructure;
using System.Threading.Tasks;
using System.Threading;
using TMG.Emme;
using TMG.Functions;
using System.IO;

namespace Tasha.EMME;

[ModuleInformation(Description = "Generate demand matrices by hourly bins.")]
public sealed class CreateHourlyMatrices : IPostHouseholdIteration
{
    [RootModule]
    public ITravelDemandModel Root;
    public string Name { get; set; }

    public float Progress { get; set; }

    public Tuple<byte, byte, byte> ProgressColour { get; set; }

    private float[][][] Matrix;

    [RunParameter("Matrix Prefix", "Transit-", "The name to have before the time bin when saving the matrix in the given directory.")]
    public string Prefix;

    [SubModelInformation(Required = true, Description = "The location to save the matrix.")]
    public FileLocation MatrixSaveLocation;

    private SpinLock WriteLock = new(false);

    [RunParameter("Minimum Age", 0, "The minimum age a person needs to be in order to be included in the demand.")]
    public int MinimumAge;

    [RunParameter("Household Iterations", 10, "The number of household iterations done during mode choice.")]
    public int HouseholdIterations;

    public void HouseholdComplete(ITashaHousehold household, bool success)
    {

    }

    public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
    {
        // now execute
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
                                if (AccessModes[l].GetTranslatedOD(tripChains[j], trips[k], access, out IZone origin, out IZone destination))
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
        // Make sure that we are always in the positive time bin space, even if it takes 2 days.
        var timeBin = (((int)startTime.ToMinutes() / 60) + 48) % 24;
        var originIndex = ZoneSystem.GetFlatIndex(origin.ZoneNumber);
        var destinationIndex = ZoneSystem.GetFlatIndex(destination.ZoneNumber);
        while(timeBin < 0)
        {
            timeBin += 24;
        }
        var row = Matrix[timeBin][originIndex];
        bool gotLock = false;
        WriteLock.Enter(ref gotLock);
        row[destinationIndex] += expFactor;
        if (gotLock) WriteLock.Exit(true);
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

        [RunParameter("Count Access", true, "True to _Count for access, false to _Count for egress.")]
        public bool CountAccess;

        [RunParameter("Access Tag Name", "AccessStation", "The tag used for storing the zone used for access.")]
        public string AccessZoneTagName;

        [RunParameter("Tour Level Access StationChoice", true, "Is the access station choice done at a tour level (true) or at the trip level (false).")]
        public bool TourLevelAccessStationChoice;

        internal ITashaMode Mode;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool GetTranslatedOD(ITripChain chain, ITrip trip, bool access, out IZone origin, out IZone destination)
        {
            if (TourLevelAccessStationChoice)
            {
                if (CountAccess ^ (!access))
                {
                    origin = trip.OriginalZone;
                    destination = chain[AccessZoneTagName] as IZone;
                    return destination != null;
                }
                origin = chain[AccessZoneTagName] as IZone;
                destination = trip.DestinationZone;
                return origin != null;
            }
            else
            {
                var accessZone = trip[AccessZoneTagName] as IZone;
                if (CountAccess)
                {
                    origin = trip.OriginalZone;
                    destination = accessZone;
                }
                else
                {
                    origin = accessZone;
                    destination = trip.DestinationZone;
                }
                return accessZone != null;
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
            // one matrix for each 15 minute period
            Matrix = new float[24][][];
            for (int timeBin = 0; timeBin < Matrix.Length; timeBin++)
            {
                Matrix[timeBin] = new float[NumberOfZones][];
                for (int i = 0; i < Matrix[timeBin].Length; i++)
                {
                    Matrix[timeBin][i] = new float[NumberOfZones];
                }
            }
        }
        else
        {
            // clear out old trips
            for (int i = 0; i < Matrix.Length; i++)
            {
                for (int j = 0; j < Matrix[i].Length; j++)
                {
                    Array.Clear(Matrix[i][j], 0, Matrix[i][j].Length);
                }
            }
        }
    }

    public void IterationFinished(int iteration)
    {
        Parallel.For(0, Matrix.Length, (int i) =>
        {
            // write to disk, converting the matrix to normalize the household iterations out.
            VectorHelper.Multiply(Matrix[i], Matrix[i], 1.0f / HouseholdIterations);
            var saveTo = Path.Combine(MatrixSaveLocation, $"{Prefix}{i}.mtx.gz");
            Directory.CreateDirectory(MatrixSaveLocation);
            new EmmeMatrix(ZoneSystem, Matrix[i]).Save(saveTo, true);
        });
    }

    public void Load(int maxIterations)
    {

    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
