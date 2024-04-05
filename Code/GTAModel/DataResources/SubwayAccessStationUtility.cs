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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Datastructure;
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.DataResources
{
    [ModuleInformation(Description =
        @"This module is designed to implement the V2.5 access station probability choice calculation.  It will not compute closest station; the using module will have to do that."
        )]
    public class SubwayAccessStationUtility : IDataSource<SparseTwinIndex<Tuple<IZone[], IZone[], float[]>>>
    {

        [RootModule]
        public I4StepModel Root;

        [RunParameter("Time of Day", "7:00AM", typeof(Time), "What time should we build the utilities for?")]
        public Time TimeOfDay;

        [RunParameter("Station Zone Range", "6000-6999", typeof(RangeSet), "The range of where subway station centroids are.")]
        public RangeSet StationZoneRange;

        [RunParameter("Transit Network Name", "Transit", "The name of the transit network to use for computing our transit travel times.")]
        public string TransitNetworkString;

        [RunParameter("Auto Network Name", "Auto", "The name of the transit network to use for computing our auto travel times.")]
        public string AutoNetworkString;

        [RunParameter("Auto Time Factor", 0.0f, "The factor to apply to the auto in vehicle time.")]
        public float AutoTimeFactor;

        [RunParameter("Auto Cost Factor", 0.0f, "The factor to apply to the auto cost.")]
        public float AutoCostFactor;

        [RunParameter("Ivtt Factor", 0.0f, "The factor to apply to the transit in vehicle travel time.")]
        public float IvttFactor;

        [RunParameter("Walk Factor", 0.0f, "The factor to apply to the walk time.")]
        public float WalkTimeFactor;

        [RunParameter("Wait Factor", 0.0f, "The factor to apply to the wait time.")]
        public float WaitTimeFactor;

        [RunParameter("Boarding Factor", 0.0f, "The factor to apply to the boardings.")]
        public float BoardingFactor;

        [RunParameter("Parking Factor", 0.0f, "The factor to apply to the log of the parking spots.")]
        public float ParkingFactor;

        [RunParameter("Parking Cost Factor", 0.0f, "The factor to apply to the cost of parking for the station.")]
        public float ParkingCostFactor;

        [RunParameter("Log Trains Factor", 0.0f, "The factor to apply to the logarithm of the number of trains that pass through the station.")]
        public float TrainsFactor;

        [RunParameter("Closest Station", 0.0f, "The constant to apply if it is the closest access station.")]
        public float ClosestStation;

        [RunParameter("Maximum Access Stations", 5, "The number of access stations for each OD pair allowed.")]
        public int MaximumAccessStations;

        [RunParameter("Access", "true", typeof(bool), "Should we be computing access or egress (true for access).")]
        public bool Access;

        [SubModelInformation(Description = "(Origin = Station Number, Destination = Parking Spots, Data = Number Of Trains)", Required = true)]
        public IReadODData<float> StationInformationReader;

        /// <summary>
        /// The TransitNetwork that we will extract using the TransitNetworkString
        /// </summary>
        private ITripComponentData TransitNetwork;

        /// <summary>
        /// The AutoNetwork that we will extract using the AutoNetworkString
        /// </summary>
        private INetworkData AutoNetwork;

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
            if ( Data == null | LastIteration != Root.CurrentIteration )
            {
                LastIteration = Root.CurrentIteration;
                var zoneArray = Root.ZoneSystem.ZoneArray;
                var zones = zoneArray.GetFlatData();
                int[] accessZones = null;
                float[] parking = null;
                SparseTwinIndex<Tuple<IZone[], IZone[], float[]>> data = null;
                // these will be flagged to true if we needed to load a network
                bool loadedAuto = false, loadedTransit = false;
                Parallel.Invoke( () =>
                    {
                        accessZones = GetAccessZones( zoneArray );
                        data = zoneArray.CreateSquareTwinArray<Tuple<IZone[], IZone[], float[]>>();
                    },
                    () =>
                    {
                        LoadStationData( zoneArray, out parking, out float[] trains );
                    },
                    () =>
                    {
                        if ( !AutoNetwork.Loaded )
                        {
                            AutoNetwork.LoadData();
                            loadedAuto = true;
                        }
                    },
                    () =>
                    {
                        if ( !TransitNetwork.Loaded )
                        {
                            TransitNetwork.LoadData();
                            loadedTransit = true;
                        }
                    } );
                var flatData = data.GetFlatData();
                Console.WriteLine( "Computing DAS Access station utilities." );
                Stopwatch watch = Stopwatch.StartNew();
                Parallel.For( 0, zones.Length, o =>
                {
                    // There is no need to compute drive access subway when you are starting at an access station
                    if ( accessZones.Contains( o ) ) return;
                    // for the rest of the zones though, compute it to all destinations
                    for ( int d = 0; d < zones.Length; d++ )
                    {
                        // you also don't need to compute the access station choices for destinations that are access stations
                        if ( accessZones.Contains( d ) ) continue;
                        ComputeUtility( o, d, zones, accessZones, parking, flatData );
                    }
                } );
                watch.Stop();
                Console.WriteLine( "It took " + watch.ElapsedMilliseconds + "ms to compute the DAS access stations." );
                // if we loaded the data make sure to unload it
                if ( loadedAuto )
                {
                    AutoNetwork.UnloadData();
                }
                if ( loadedTransit )
                {
                    TransitNetwork.UnloadData();
                }
                Data = data;
            }
        }

        private void LoadStationData(SparseArray<IZone> zones, out float[] parking, out float[] trains)
        {
            float[] p, t;
            var numberOfZones = zones.GetFlatData().Length;
            p = new float[numberOfZones];
            t = new float[numberOfZones];
            foreach ( var point in StationInformationReader.Read() )
            {
                var index = zones.GetFlatIndex( point.O );
                if ( index >= 0 )
                {
                    p[index] = point.D;
                    t[index] = (float)Math.Log( point.Data );
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
            foreach ( var rangeSet in StationZoneRange )
            {
                for ( int i = rangeSet.Start; i <= rangeSet.Stop; i++ )
                {
                    //get the flat space index of the zone
                    var index = zoneArray.GetFlatIndex( i );
                    // make sure that the zone actually exists
                    if ( index >= 0 )
                    {
                        // if it does then add it to our possible access stations
                        accessIndexes.Add( index );
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
        /// <param name="data">Where the results will be stored</param>
        /// <param name="parking"></param>
        private void ComputeUtility(int o, int d, IZone[] zones, int[] flatAccessZones, float[] parking, Tuple<IZone[], IZone[], float[]>[][] data)
        {
            // for each access station
            Tuple<IZone[], IZone[], float[]> odData = null;
            float[] results = null;
            float[] distances = null;
            IZone[] resultZones = null;
            int soFar = 0;
            // compute the base access station utilities
            for ( int i = 0; i < flatAccessZones.Length; i++ )
            {
                if ( ComputeUtility( o, d, zones, flatAccessZones[i], parking, distances == null | soFar < MaximumAccessStations
                    ? float.MaxValue : distances[distances.Length - 1], out float result, out float distance ) )
                {
                    if ( odData == null )
                    {
                        distances = new float[MaximumAccessStations];
                        for ( int j = 0; j < distances.Length; j++ )
                        {
                            distances[j] = float.MaxValue;
                        }
                        odData = new Tuple<IZone[], IZone[], float[]>( resultZones = new IZone[MaximumAccessStations], null, results = new float[MaximumAccessStations] );
                    }
                    // if we have extra room or if this access station is closest than the farthest station we have accepted
                    if ( ( soFar < MaximumAccessStations ) | ( distance < distances[results.Length - 1] ) )
                    {
                        Insert( zones, flatAccessZones, results, distances, resultZones, i, result, distance );
                        soFar++;
                    }
                }
            }
            // now we can compute the higher level station utilities
            if ( results != null )
            {
                results[0] += ClosestStation;
                // now raise everything to the e to save processing time later
                for ( int i = 0; i < results.Length; i++ )
                {
                    results[i] = (float)Math.Exp( results[i] );
                }
            }
            // save the OD data into the databank
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
        /// <param name="currentAccessStationIndex">The current access station that is being processed</param>
        /// <param name="result">The value of the access station that is being processed</param>
        /// <param name="distance">The distance to the access station that is being processed</param>
        private static void Insert(IZone[] zones, int[] flatAccessZones, float[] results, float[] distances,
            IZone[] resultZones, int currentAccessStationIndex, float result, float distance)
        {
            // then we need to insert it in properly
            for ( int currentPosition = 0; currentPosition < distances.Length; currentPosition++ )
            {
                // if this is where we should be insert it here
                if ( distance < distances[currentPosition] )
                {
                    // first push everything back
                    for ( int pushToIndex = distances.Length - 1; pushToIndex > currentPosition; pushToIndex-- )
                    {
                        distances[pushToIndex] = distances[pushToIndex - 1];
                        resultZones[pushToIndex] = resultZones[pushToIndex - 1];
                        results[pushToIndex] = results[pushToIndex - 1];
                    }
                    //then insert into our position
                    results[currentPosition] = result;
                    resultZones[currentPosition] = zones[flatAccessZones[currentAccessStationIndex]];
                    distances[currentPosition] = distance;
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
        /// <param name="maxDistance">The maximum distance allowed</param>
        /// <param name="result">the utility of using this interchange</param>
        /// <param name="distanceByAuto">The distance the origin is from the interchange in auto travel time</param>
        /// <param name="parking"></param>
        /// <returns>True if this is a valid interchange zone, false if not feasible.</returns>
        private bool ComputeUtility(int o, int d, IZone[] zones, int interchange, float[] parking, float maxDistance,
            out float result, out float distanceByAuto)
        {
            result = float.NaN;
            Time ivtt, walk, wait, boarding;
            float cost;
            float v = 0.0f;
            var destinationDistance = AutoNetwork.TravelTime( o, d, TimeOfDay ).ToMinutes();
            if ( Access )
            {
                // distance is actually the travel time
                distanceByAuto = AutoNetwork.TravelTime( o, interchange, TimeOfDay ).ToMinutes();
                // there is no need to continue if we have already found the max number of paths that are shorter
                // it also a valid choice to drive longer in order to use the access station compared to just going to the final destination.
                // we also are not feasible if there is no parking spots
                if ( distanceByAuto >= maxDistance | destinationDistance < distanceByAuto | parking[interchange] <= 0 ) return false;
                // get the from interchange to destination costs (we need to include boarding here even though we don't actually use it in our utility function
                // making individual calls for the data would be more expensive

                if ( !TransitNetwork.GetAllData( interchange, d, TimeOfDay, out ivtt, out walk, out wait, out boarding, out cost ) | ivtt <= Time.Zero | walk <= Time.Zero )
                {
                    return false;
                }
            }
            else
            {
                // This will be executed if we want to run the EGRESS model

                // distance is actually the travel time from the station we get off at to our destination
                distanceByAuto = AutoNetwork.TravelTime( interchange, d, TimeOfDay ).ToMinutes();
                // make sure we clip properly
                if ( distanceByAuto >= maxDistance | destinationDistance < distanceByAuto | parking[interchange] <= 0 ) return false;
                // in egress the transit trip is actually before the drive, so origin to the interchange is transit
                if ( !TransitNetwork.GetAllData( o, interchange, TimeOfDay, out ivtt, out walk, out wait, out boarding, out cost ) | ivtt <= Time.Zero | walk <= Time.Zero )
                {
                    return false;
                }
            }
            v += IvttFactor * ivtt.ToMinutes()
                + WaitTimeFactor * wait.ToMinutes()
                + WalkTimeFactor * walk.ToMinutes()
                + BoardingFactor * boarding.ToMinutes();
            v += AutoTimeFactor * distanceByAuto;
            v += AutoCostFactor * AutoNetwork.TravelCost( o, interchange, TimeOfDay );
            v += ParkingFactor * (float)Math.Log( parking[interchange] );
            v += ParkingCostFactor * zones[interchange].ParkingCost;
            // Now add in the origin to interchange zone utilities
            result = v;
            return true;
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
            foreach ( var network in Root.NetworkData )
            {
                if ( network.NetworkType == TransitNetworkString )
                {
                    TransitNetwork = network as ITripComponentData;
                }
                if ( network.NetworkType == AutoNetworkString )
                {
                    AutoNetwork = network;
                }
            }
            // check to make sure that both networks were loaded in, if not report an error
            if ( AutoNetwork == null )
            {
                error = "In '" + Name + "' a auto network named '" + AutoNetworkString + "' could not be found.";
                return false;
            }
            if ( TransitNetwork == null )
            {
                error = "In '" + Name + "' a transit network named '" + TransitNetworkString + "' could not be found.";
                return false;
            }
            return true;
        }
    }
}
