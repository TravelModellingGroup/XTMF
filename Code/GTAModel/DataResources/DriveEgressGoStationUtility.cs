﻿/*
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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Datastructure;
using TMG.Input;
using XTMF;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace TMG.GTAModel.DataResources
{
    [ModuleInformation(Description =
        @"This module is designed to store the Drive Access GO (DAG) station utility choices for GTAModel 2.5.")]
    public class DriveEgressGoStationUtility : IDataSource<SparseTwinIndex<Tuple<IZone[], IZone[], float[]>>>
    {

        [RootModule]
        public I4StepModel Root;

        [RunParameter("Time of Day", "7:00AM", typeof(Time), "What time should we build the utilities for?")]
        public Time TimeOfDay;

        [RunParameter("Station Zone Range", "7000-7999", typeof(RangeSet), "The range of where subway station centroids are.")]
        public RangeSet StationZoneRange;

        [RunParameter("GoTransit Network Name", "GoRail", "The name of the transit network to use for computing our transit travel times.")]
        public string GoTransitNetworkString;

        [RunParameter("Transit Network Name", "Transit", "The name of the transit network to use for computing our transit travel times.")]
        public string TransitNetworkString;

        [RunParameter("Auto Network Name", "Auto", "The name of the Premium transit network to use for computing our transit travel times.")]
        public string AutoNetworkString;

        [RunParameter("Closest Station", 0.0f, "The constant to apply if it is the closest access station.")]
        public float ClosestStation;

        [RunParameter("Auto IVTT Factor", 0.0f, "The factor to apply to the auto in vehicle travel time.")]
        public float AivttFactor;

        [RunParameter("Ivtt Factor", 0.0f, "The factor to apply to the transit in vehicle travel time.")]
        public float IvttFactor;

        [RunParameter("Walk Factor", 0.0f, "The factor to apply to the walk time.")]
        public float WalkTimeFactor;

        [RunParameter("Wait Factor", 0.0f, "The factor to apply to the wait time.")]
        public float WaitTimeFactor;

        [RunParameter("Boarding Factor", 0.0f, "The factor to apply to the boardings.")]
        public float BoardingFactor;

        [RunParameter("Auto Cost Factor", 0.0f, "The factor to apply to the cost of auto.")]
        public float AutoCostFactor;

        [RunParameter("Transit Cost Factor", 0.0f, "The factor to apply to the cost of transit (Including GO/Premium/Local).")]
        public float TransitCostFactor;

        [RunParameter("Trains Factor", 0.0f, "The factor to apply to the natural log of the number of trains that pass through the station.")]
        public float TrainsFactor;

        [RunParameter("Parking Factor", 0.0f, "The factor to apply to the natural log of the number of parking space available.")]
        public float ParkingFactor;

        [RunParameter("bfnew", 0.0f, "The factor applied to the go rail cost while computing the transit fare factor.  In DRM V2.5 2006, this parameter was set to 0, which ignores all rail cost.")]
        // ReSharper disable once InconsistentNaming
        public float bfnew;

        [RunParameter("Maximum Access Stations", 5, "The number of access stations for each OD pair allowed.")]
        public int MaximumAccessStations;

        [RunParameter("Egress Wait Perception", 2.0f, "The conversion between wait time for egress to in-vehicle-time units.")]
        public float EgressWaitPerception;

        [RunParameter("Egress Walk Perception", 2.0f, "The conversion between walk time for egress to in-vehicle-time units.")]
        public float EgressWalkPerception;

        [SubModelInformation(Description = "(Origin = Station Number, Destination = Parking Spots, Data = Number Of Trains)", Required = true)]
        public IReadODData<float> StationInformationReader;

        [RunParameter("Minimum Station Utility", -10.0f, "The minimum utility an access station can have.")]
        public float MinimumStationUtility;

        /// <summary>
        /// The Auto Network that we will extract using the TransitNetworkString
        /// </summary>
        private INetworkData AutoNetwork;

        /// <summary>
        /// The TransitNetwork that we will extract using the TransitNetworkString
        /// </summary>
        private ITripComponentData TransitNetwork;

        /// <summary>
        /// The GoTransit Network that we will extract using the GoTransitNetworkString
        /// </summary>
        private ITripComponentData GoTransitNetwork;

        /// <summary>
        /// Our internal holding for the utilities
        /// </summary>
        private SparseTwinIndex<Tuple<IZone[], IZone[], float[]>> Data;

        public SparseTwinIndex<Tuple<IZone[], IZone[], float[]>> GiveData()
        {
            return Data;
        }

        public bool Loaded
        {
            get { return Data != null; }
        }

        private int LastIteration = -1;

        public void LoadData()
        {
            if (Data == null | LastIteration != Root.CurrentIteration)
            {
                LastIteration = Root.CurrentIteration;
                var zoneArray = Root.ZoneSystem.ZoneArray;
                var zones = zoneArray.GetFlatData();
                int[] accessZones = null;
                float[] parking = null, trains = null;
                SparseTwinIndex<Tuple<IZone[], IZone[], float[]>> data = null;
                // these will be flagged to true if we needed to load a network
                bool loadedGo = false, loadedTransit = false, loadedAutoNetwork = false;
                Parallel.Invoke(() =>
                   {
                       accessZones = GetAccessZones(zoneArray);
                       data = zoneArray.CreateSquareTwinArray<Tuple<IZone[], IZone[], float[]>>();
                   },
                    () =>
                    {
                        LoadStationData(zoneArray, out parking, out trains);
                    },
                    () =>
                    {
                        if (!GoTransitNetwork.Loaded)
                        {
                            GoTransitNetwork.LoadData();
                            loadedGo = true;
                        }
                    },
                    () =>
                    {
                        if (!TransitNetwork.Loaded)
                        {
                            TransitNetwork.LoadData();
                            loadedTransit = true;
                        }
                    },
                    () =>
                    {
                        if (!AutoNetwork.Loaded)
                        {
                            AutoNetwork.LoadData();
                            loadedAutoNetwork = true;
                        }
                    });
                var flatData = data.GetFlatData();
                Console.WriteLine("Computing DAG Access station utilities.");
                Stopwatch watch = Stopwatch.StartNew();
                float[][] egressUtility = new float[accessZones.Length][];
                int[][] egressZones = new int[accessZones.Length][];
                float[][] egressTime = new float[accessZones.Length][];
                for (int i = 0; i < egressUtility.Length; i++)
                {
                    egressUtility[i] = new float[zones.Length];
                    egressTime[i] = new float[zones.Length];
                    egressZones[i] = new int[zones.Length];
                }
                // compute the egress data
                Parallel.For(0, accessZones.Length, i =>
                   {
                       var interchange = accessZones[i];
                       for (int j = 0; j < zones.Length; j++)
                       {
                            // you also don't need to compute the access station choices for destinations that are access stations
                            if (zones[j].RegionNumber <= 0)
                           {
                               egressTime[i][j] = float.NaN;
                               egressUtility[i][j] = float.NaN;
                               egressZones[i][j] = -1;
                           }
                           ComputeEgressStation(interchange, j, accessZones, trains, parking, out egressUtility[i][j], out egressTime[i][j], out egressZones[i][j]);
                       }
                   });
                // using the egress data compute access stations
                Parallel.For(0, zones.Length, o =>
               {
                    // There is no need to compute drive access subway when you are starting at an access station
                    int regionO = zones[o].RegionNumber;
                   if (regionO == 0 | accessZones.Contains(o)) return;
                    // for the rest of the zones though, compute it to all destinations
                    for (int d = 0; d < zones.Length; d++)
                   {
                        // you also don't need to compute the access station choices for destinations that are access stations
                        var regionD = zones[d].RegionNumber;
                       if (regionD == 0 | accessZones.Contains(d)) continue;
                       if ((regionO == 1) | (regionO != regionD))
                       {
                           ComputeUtility(o, d, zones, accessZones, egressUtility, egressTime, egressZones, flatData);
                       }
                   }
               });
                watch.Stop();
                Console.WriteLine("It took " + watch.ElapsedMilliseconds + "ms to compute the DAG access stations.");
                // if we loaded the data make sure to unload it
                if (loadedGo)
                {
                    GoTransitNetwork.UnloadData();
                }
                if (loadedTransit)
                {
                    TransitNetwork.UnloadData();
                }
                if (loadedAutoNetwork)
                {
                    AutoNetwork.UnloadData();
                }
                Data = data;
            }
        }

        /// <summary>
        /// Load in the station data from file
        /// </summary>
        /// <param name="zones">A copy of the zone system</param>
        /// <param name="parking">Parking data (Logged)</param>
        /// <param name="trains">Train frequency (Logged)data</param>
        private void LoadStationData(SparseArray<IZone> zones, out float[] parking, out float[] trains)
        {
            float[] p, t;
            var numberOfZones = zones.GetFlatData().Length;
            p = new float[numberOfZones];
            t = new float[numberOfZones];
            foreach (var point in StationInformationReader.Read())
            {
                var index = zones.GetFlatIndex(point.O);
                if (index >= 0)
                {
                    p[index] = point.D == 0 ? 0 : (float)Math.Log(point.D);
                    t[index] = point.Data == 0 ? 0f : (float)Math.Log(point.Data);
                }
            }
            parking = p;
            trains = t;
        }

        /// <summary>
        /// Get the access zones as defined in StationZone Range
        /// </summary>
        /// <param name="zoneArray">The zone system</param>
        /// <returns>An array of flat zone indexes that represent the access stations.</returns>
        private int[] GetAccessZones(SparseArray<IZone> zoneArray)
        {
            List<int> accessIndexes = [];
            foreach (var rangeSet in StationZoneRange)
            {
                for (int i = rangeSet.Start; i <= rangeSet.Stop; i++)
                {
                    //get the flat space index of the zone
                    var index = zoneArray.GetFlatIndex(i);
                    // make sure that the zone actually exists
                    if (index >= 0)
                    {
                        // if it does then add it to our possible access stations
                        accessIndexes.Add(index);
                    }
                }
            }
            return accessIndexes.ToArray();
        }

        /// <summary>
        /// Compute the results of the access station choice model
        /// </summary>
        /// <param name="o">flat origin zone</param>
        /// <param name="d">flat destination zone</param>
        /// <param name="zones">the array of zones</param>
        /// <param name="flatAccessZones">the array of access stations</param>
        /// <param name="egressZones"></param>
        /// <param name="data">Where the results will be stored</param>
        /// <param name="egressUtility"></param>
        /// <param name="egressTime"></param>
        private void ComputeUtility(int o, int d, IZone[] zones, int[] flatAccessZones, float[][] egressUtility, float[][] egressTime,
            int[][] egressZones, Tuple<IZone[], IZone[], float[]>[][] data)
        {
            // for each access station
            Tuple<IZone[], IZone[], float[]> odData = null;
            float[] results = null;
            float[] distances = null;
            IZone[] resultZones = null;
            IZone[] resultEgressZones = null;
            int soFar = 0;
            // compute the base access station utilities
            for (int i = 0; i < flatAccessZones.Length; i++)
            {
                if (ComputeUtility(o, d, zones, flatAccessZones[i], i, flatAccessZones, (distances == null | soFar < MaximumAccessStations
                    ? float.MaxValue : distances[distances.Length - 1]), egressUtility, egressTime, egressZones, out float result, out float distance, out IZone egressZone))
                {
                    if (odData == null)
                    {
                        distances = new float[MaximumAccessStations];
                        for (int j = 0; j < distances.Length; j++)
                        {
                            distances[j] = float.MaxValue;
                        }
                        odData = new Tuple<IZone[], IZone[], float[]>(resultEgressZones = new IZone[MaximumAccessStations], resultZones = new IZone[MaximumAccessStations], results = new float[MaximumAccessStations]);
                    }
                    // if we have extra room or if this access station is closest than the farthest station we have accepted
                    if ((soFar < MaximumAccessStations) | (distance < distances[results.Length - 1]))
                    {
                        Insert(zones, flatAccessZones, results, distances, resultZones, resultEgressZones, i, egressZone, result, distance);
                        soFar++;
                    }
                }
            }
            // now we can compute the higher level station utilities
            if (results != null)
            {
                results[0] += ClosestStation;
                // now raise everything to the e to save processing time later
                for (int i = 0; i < results.Length; i++)
                {
                    results[i] = (float)Math.Exp(results[i]);
                }
            }
            // save the OD data into the data bank
            data[o][d] = odData;
        }

        /// <summary>
        /// Insert the result into our storage
        /// </summary>
        /// <param name="zones">The zones that we are working with</param>
        /// <param name="flatAccessZones">The access stations that exist</param>
        /// <param name="results">The utilities for the different access stations</param>
        /// <param name="distances">The distances for the different access stations</param>
        /// <param name="resultZones">The zones that represent the different access stations</param>
        /// <param name="egressZones"></param>
        /// <param name="currentAccessStationIndex">The current access station that is being processed</param>
        /// <param name="egressZone"></param>
        /// <param name="result">The value of the access station that is being processed</param>
        /// <param name="distance">The distance to the access station that is being processed</param>
        private static void Insert(IZone[] zones, int[] flatAccessZones, float[] results, float[] distances,
            IZone[] resultZones, IZone[] egressZones, int currentAccessStationIndex, IZone egressZone, float result, float distance)
        {
            // then we need to insert it in properly
            for (int currentPosition = 0; currentPosition < distances.Length; currentPosition++)
            {
                // if this is where we should be insert it here
                if (distance < distances[currentPosition])
                {
                    // first push everything back
                    for (int pushToIndex = distances.Length - 1; pushToIndex > currentPosition; pushToIndex--)
                    {
                        distances[pushToIndex] = distances[pushToIndex - 1];
                        resultZones[pushToIndex] = resultZones[pushToIndex - 1];
                        results[pushToIndex] = results[pushToIndex - 1];
                        egressZones[pushToIndex] = egressZones[pushToIndex - 1];
                    }
                    //then insert into our position
                    results[currentPosition] = result;
                    resultZones[currentPosition] = zones[flatAccessZones[currentAccessStationIndex]];
                    distances[currentPosition] = distance;
                    egressZones[currentPosition] = egressZone;
                    break;
                }
            }
        }

        /// <summary>
        /// Computes the utility of a single interchange
        /// </summary>
        /// <param name="o">flat origin zone</param>
        /// <param name="d">flat destination zone</param>
        /// <param name="zones">an array of zones</param>
        /// <param name="interchange">the flat interchange zone to use</param>
        /// <param name="egressZones"></param>
        /// <param name="maxDistance">The maximum distance allowed</param>
        /// <param name="selectedEgressZones"></param>
        /// <param name="result">the utility of using this interchange</param>
        /// <param name="distance">The distance the origin is from the interchange in auto travel time</param>
        /// <param name="accessStationIndex"></param>
        /// <param name="egressUtility"></param>
        /// <param name="egressTime"></param>
        /// <param name="egressZone"></param>
        /// <returns>True if this is a valid interchange zone, false if not feasible.</returns>
        // ReSharper disable once UnusedParameter.Local
        private bool ComputeUtility(int o, int d, IZone[] zones, int interchange, int accessStationIndex, int[] egressZones, float maxDistance, float[][] egressUtility, float[][] egressTime,
            int[][] selectedEgressZones, out float result, out float distance, out IZone egressZone)
        {
            float v = ComputeAccessUtility(d, interchange, out distance);
            // our total travel time / distance is the egress time plus the time it takes to get to the station to begin with
            if (egressTime[accessStationIndex][o] == 0)
            {
                throw new XTMFRuntimeException(this, "In '" + Name + "' the egress time between zone " + zones[egressZones[accessStationIndex]].ZoneNumber + " and " + zones[d].ZoneNumber + " was equal to 0!");
            }
            if (distance <= AutoNetwork.TravelTime(d, o, TimeOfDay).ToMinutes())
            {
                var egressUtil = egressUtility[accessStationIndex][o];
                // Step 1, compute the egress station, and the utility from access station to egress to destination
                if (distance < maxDistance & !float.IsNaN(v) & !float.IsNaN(egressUtil))
                {
                    egressZone = zones[selectedEgressZones[accessStationIndex][o]];
                    // Step 2, compute the utility going from origin to access station
                    v += egressUtil;
                    result = v;
                    return true;
                }
            }
            egressZone = null;
            result = float.NaN;
            return false;
        }

        /// <summary>
        /// Compute the utility of accessing the access station
        /// </summary>
        /// <param name="destination">The origin of the trip, flat</param>
        /// <param name="interchange">The access station, flat</param>
        /// <param name="weightedTravelTime"></param>
        /// <returns>The utility of picking the access station, NaN if it isn't possible</returns>
        private float ComputeAccessUtility(int destination, int interchange, out float weightedTravelTime)
        {
            float v = 0.0f;
            Time ivtt = AutoNetwork.TravelTime(interchange, destination, TimeOfDay);
            float cost = AutoNetwork.TravelCost(interchange, destination, TimeOfDay);
            // once we have the data we can then compute the utility
            v += AivttFactor * ivtt.ToMinutes()
                + AutoCostFactor * cost;
            // we can also compute the weighted travel time here in order to avoid additional lookups
            weightedTravelTime = ivtt.ToMinutes();
            return v;
        }

        /// <summary>
        /// Compute which egress station to use
        /// </summary>
        /// <param name="interchange">The access zone to start from, flat</param>
        /// <param name="origin">The zone that the egress trip is starting at</param>
        /// <param name="egressZones">The list of all possible egress zones, flat</param>
        /// <param name="trains"></param>
        /// <param name="parking">The amount of parking available</param>
        /// <param name="egressUtility">The utility of taking the given egress station</param>
        /// <param name="egressTime"></param>
        /// <param name="egressZone">The egress station to use for this access station</param>
        /// <returns>If we were successful in finding an egress station for this access station.</returns>
        private void ComputeEgressStation(int interchange, int origin, int[] egressZones, float[] trains, float[] parking, out float egressUtility, out float egressTime, out int egressZone)
        {
            int bestZone = -1;
            // only compute the egress logic if we have parking spots available.
            if (parking[interchange] > 0)
            {
                // Set the best utility initially to the time it takes to go from the access station to the destination
                // if we can't find an egress station better than this the Access -> Egress pair isn't valid
                // This looks backwards however the parameters are reversed already
                float maxEgressTime = ComputeWeightedTimeWithoutRail(interchange, origin);
                float bestTravelTime = float.MaxValue;

                for (int i = 0; i < egressZones.Length; i++)
                {
                    // you are not allowed to egress from the station you originally accessed
                    float egressGeneralTime = ComputeWeightedTimeWithoutRail(egressZones[i], origin);
                    var goTime = GoTransitNetwork.InVehicleTravelTime(egressZones[i], interchange, TimeOfDay).ToMinutes();
                    if (goTime <= 0)
                    {
                        continue;
                    }
                    if (!float.IsNaN(egressGeneralTime) & (egressGeneralTime <= maxEgressTime))
                    {
                        // Now add on the go transit time
                        egressGeneralTime += goTime;
                        if (egressGeneralTime < bestTravelTime)
                        {
                            bestZone = egressZones[i];
                            bestTravelTime = egressGeneralTime;
                        }
                    }
                }
            }
            // If there is no egress station we are invalid
            if ((bestZone < 0) | (bestZone == interchange))
            {
                egressUtility = float.NaN;
                egressZone = -1;
                egressTime = float.NaN;
            }
            else
            {
                egressUtility = ComputeEgressStationUtility(interchange, bestZone, origin)
                                + TrainsFactor * trains[bestZone]
                                + ParkingFactor * parking[bestZone];
                egressZone = bestZone;
                egressTime = ComputeWeightedTimeWithoutRail(bestZone, origin);
            }
        }

        /// <summary>
        /// Computes the weighted travel time between two points.
        /// This method does not include the line hull time!
        /// </summary>
        /// <param name="origin">The zone to start from, flat space</param>
        /// <param name="destination">The zone to end at, flat space</param>
        /// <returns>The weighted travel time from egress station to destination</returns>
        private float ComputeWeightedTimeWithoutRail(int origin, int destination)
        {
            if (!TransitNetwork.GetAllData(destination, origin, TimeOfDay, out Time ivtt, out Time walk, out Time wait, out Time boardings, out float cost) | walk <= Time.Zero)
            {
                return float.NaN;
            }
            return ivtt.ToMinutes()
                    + EgressWaitPerception * wait.ToMinutes()
                    + EgressWalkPerception * walk.ToMinutes();
        }

        /// <summary>
        /// Compute the utility of taking a particular egress station
        /// </summary>
        /// <param name="interchange">The access station to start from</param>
        /// <param name="egress">The station to get off at</param>
        /// <param name="origin">The destination to end the trip</param>
        /// <returns>The utility of using the egress station, NaN if no real egress station is used.</returns>
        private float ComputeEgressStationUtility(int interchange, int egress, int origin)
        {

            var goIvtt = GoTransitNetwork.InVehicleTravelTime(egress, interchange, TimeOfDay);
            var goWalk = GoTransitNetwork.WalkTime(egress, interchange, TimeOfDay);
            if (TransitNetwork.GetAllData(origin, egress, TimeOfDay, out Time ivtt, out Time walk, out Time wait, out Time boardings, out float cost))
            {
                if (ivtt <= Time.Zero)
                {
                    cost = 0f;
                }
                return IvttFactor * (goIvtt + ivtt).ToMinutes()
                    + WaitTimeFactor * wait.ToMinutes()
                    + WalkTimeFactor * (goWalk + walk).ToMinutes()
                    + BoardingFactor * boardings.ToMinutes()
                    + TransitCostFactor * cost;
            }
            return float.NaN;
        }

        public void UnloadData()
        {
            Data = null;
        }

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {
            //search through the networks and load in the data based on their names
            foreach (var network in Root.NetworkData)
            {
                if (network.NetworkType == TransitNetworkString)
                {
                    TransitNetwork = network as ITripComponentData;
                }
                if (network.NetworkType == GoTransitNetworkString)
                {
                    GoTransitNetwork = network as ITripComponentData;
                }
                if (network.NetworkType == AutoNetworkString)
                {
                    AutoNetwork = network;
                }
            }
            // Make sure the networks we require are available
            if (AutoNetwork == null)
            {
                error = "In '" + Name + "' an auto network named '" + AutoNetworkString + "' could not be found.";
                return false;
            }
            if (TransitNetwork == null)
            {
                error = "In '" + Name + "' a transit network named '" + TransitNetworkString + "' could not be found.";
                return false;
            }
            if (GoTransitNetwork == null)
            {
                error = "In '" + Name + "' a go transit network named '" + GoTransitNetworkString + "' could not be found.";
                return false;
            }
            return true;
        }
    }
}
