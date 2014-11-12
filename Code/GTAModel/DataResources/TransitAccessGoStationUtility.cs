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
using System.Text;
using System.Threading.Tasks;
using Datastructure;
using TMG.Input;
using TMG.Modes;
using XTMF;

namespace TMG.GTAModel.DataResources
{
    [ModuleInformation(Description =
        @"This module is designed to store the Transit Access GO (TAG) station utility choices for GTAModel 2.5.")]
    public class TransitAccessGoStationUtility : IDataSource<SparseTwinIndex<Tuple<IZone[], IZone[], float[]>>>
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

        [RunParameter("Premium Transit Network Name", "Transit", "The name of the Premium transit network to use for computing our transit travel times.")]
        public string PremiumTransitNetworkString;

        [RunParameter("Ivtt Factor", 0.0f, "The factor to apply to the transit in vehicle travel time.")]
        public float IvttFactor;

        [RunParameter("Walk Factor", 0.0f, "The factor to apply to the walk time.")]
        public float WalkTimeFactor;

        [RunParameter("Wait Factor", 0.0f, "The factor to apply to the wait time.")]
        public float WaitTimeFactor;

        [RunParameter("Boarding Factor", 0.0f, "The factor to apply to the boardings.")]
        public float BoardingFactor;

        [RunParameter("Cost Factor", 0.0f, "The factor to apply to the cost of transit (Including GO/Premium/Local).")]
        public float CostFactor;

        [RunParameter("Trains Factor", 0.0f, "The factor to apply to the number of trains that pass through the station.")]
        public float TrainsFactor;

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
        /// The Premium TransitNetwork that we will extract using the TransitNetworkString
        /// </summary>
        private ITripComponentData PremiumTransitNetwork;

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
            return this.Data;
        }

        public bool Loaded
        {
            get { return this.Data != null; }
        }

        private int LastIteration = -1;

        public void LoadData()
        {
            if ( this.Data == null | this.LastIteration != this.Root.CurrentIteration )
            {
                this.LastIteration = this.Root.CurrentIteration;
                var zoneArray = this.Root.ZoneSystem.ZoneArray;
                var zones = zoneArray.GetFlatData();
                int[] accessZones = null;
                float[] parking = null, trains = null;
                SparseTwinIndex<Tuple<IZone[], IZone[], float[]>> data = null;
                // these will be flagged to true if we needed to load a network
                bool loadedGo = false, loadedTransit = false, loadedPremiumTransit = false;
                Parallel.Invoke( () =>
                    {
                        accessZones = GetAccessZones( zoneArray );
                        data = zoneArray.CreateSquareTwinArray<Tuple<IZone[], IZone[], float[]>>();
                    },
                    () =>
                    {
                        LoadStationData( zoneArray, out parking, out trains );
                    },
                    () =>
                    {
                        if ( !this.GoTransitNetwork.Loaded )
                        {
                            this.GoTransitNetwork.LoadData();
                            loadedGo = true;
                        }
                    },
                    () =>
                    {
                        if ( !this.TransitNetwork.Loaded )
                        {
                            this.TransitNetwork.LoadData();
                            loadedTransit = true;
                        }
                    },
                    () =>
                    {
                        if ( !this.PremiumTransitNetwork.Loaded )
                        {
                            this.PremiumTransitNetwork.LoadData();
                            loadedPremiumTransit = true;
                        }
                    } );
                var flatData = data.GetFlatData();
                Console.WriteLine( "Computing TAG Access station utilities." );
                Stopwatch watch = Stopwatch.StartNew();
                float[][] egressUtility = new float[accessZones.Length][];
                int[][] egressZones = new int[accessZones.Length][];
                float[][] egressTime = new float[accessZones.Length][];
                for ( int i = 0; i < egressUtility.Length; i++ )
                {
                    egressUtility[i] = new float[zones.Length];
                    egressTime[i] = new float[zones.Length];
                    egressZones[i] = new int[zones.Length];
                }
                // compute the egress data
                Parallel.For( 0, accessZones.Length, (int i) =>
                    {
                        var interchange = accessZones[i];
                        for ( int j = 0; j < zones.Length; j++ )
                        {
                            // you also don't need to compute the access station choices for destinations that are access stations
                            if ( zones[j].RegionNumber <= 0 )
                            {
                                egressTime[i][j] = float.NaN;
                                egressUtility[i][j] = float.NaN;
                                egressZones[i][j] = -1;
                            }
                            ComputeEgressStation( interchange, i, j, accessZones, trains, parking, zones, out egressUtility[i][j], out egressTime[i][j], out egressZones[i][j] );
                        }
                    } );
                // using the egress data compute access stations
                Parallel.For( 0, zones.Length, (int o) =>
                {
                    // There is no need to compute drive access subway when you are starting at an access station
                    int regionO = zones[o].RegionNumber;
                    if ( regionO == 0 | accessZones.Contains( o ) ) return;
                    // for the rest of the zones though, compute it to all destinations
                    for ( int d = 0; d < zones.Length; d++ )
                    {
                        // you also don't need to compute the access station choices for destinations that are access stations
                        var regionD = zones[d].RegionNumber;
                        if ( regionD == 0 | accessZones.Contains( d ) ) continue;
                        if ( ( regionO == 1 ) | ( regionO != regionD ) )
                        {
                            ComputeUtility( o, d, zones, accessZones, egressUtility, egressTime, egressZones, flatData );
                        }
                    }
                } );
                watch.Stop();
                Console.WriteLine( "It took " + watch.ElapsedMilliseconds + "ms to compute the TAG access stations." );
                // if we loaded the data make sure to unload it
                if ( loadedGo )
                {
                    this.GoTransitNetwork.UnloadData();
                }
                if ( loadedTransit )
                {
                    this.TransitNetwork.UnloadData();
                }
                if ( loadedPremiumTransit )
                {
                    this.PremiumTransitNetwork.UnloadData();
                }
                this.Data = data;
            }
        }

        /// <summary>
        /// Load in the station data from file
        /// </summary>
        /// <param name="zones">A copy of the zone system</param>
        /// <param name="parking">Parking data</param>
        /// <param name="trains">Train frequency data</param>
        private void LoadStationData(SparseArray<IZone> zones, out float[] parking, out float[] trains)
        {
            float[] p, t;
            var numberOfZones = zones.GetFlatData().Length;
            p = new float[numberOfZones];
            t = new float[numberOfZones];
            foreach ( var point in this.StationInformationReader.Read() )
            {
                var index = zones.GetFlatIndex( point.O );
                if ( index >= 0 )
                {
                    p[index] = point.D;
                    t[index] = point.Data;
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
            List<int> accessIndexes = new List<int>();
            foreach ( var rangeSet in this.StationZoneRange )
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
            for ( int i = 0; i < flatAccessZones.Length; i++ )
            {
                float result, distance;
                IZone egressZone;
                if ( ComputeUtility( o, d, zones, flatAccessZones[i], i, flatAccessZones, ( distances == null | soFar < this.MaximumAccessStations
                    ? float.MaxValue : distances[distances.Length - 1] ), egressUtility, egressTime, egressZones, out result, out distance, out egressZone )
                    /*& ( result >= this.MinimumStationUtility )*/ )
                {
                    if ( odData == null )
                    {
                        distances = new float[this.MaximumAccessStations];
                        for ( int j = 0; j < distances.Length; j++ )
                        {
                            distances[j] = float.MaxValue;
                        }
                        odData = new Tuple<IZone[], IZone[], float[]>( resultZones = new IZone[this.MaximumAccessStations], resultEgressZones = new IZone[this.MaximumAccessStations], results = new float[this.MaximumAccessStations] );
                    }
                    // if we have extra room or if this access station is closest than the farthest station we have accepted
                    if ( ( soFar < this.MaximumAccessStations ) | ( distance < distances[results.Length - 1] ) )
                    {
                        Insert( zones, flatAccessZones, results, distances, resultZones, resultEgressZones, i, egressZone, result, distance );
                        soFar++;
                    }
                }
            }
            // now we can compute the higher level station utilities
            if ( results != null )
            {
                // now raise everything to the e to save processing time later
                for ( int i = 0; i < results.Length; i++ )
                {
                    results[i] = (float)Math.Exp( results[i] );
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
        /// <param name="currentAccessStationIndex">The current access station that is being processed</param>
        /// <param name="result">The value of the access station that is being processed</param>
        /// <param name="distance">The distance to the access station that is being processed</param>
        private static void Insert(IZone[] zones, int[] flatAccessZones, float[] results, float[] distances,
            IZone[] resultZones, IZone[] egressZones, int currentAccessStationIndex, IZone egressZone, float result, float distance)
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
        /// <param name="maxDistance">The maximum distance allowed</param>
        /// <param name="result">the utility of using this interchange</param>
        /// <param name="distance">The distance the origin is from the interchange in auto travel time</param>
        /// <returns>True if this is a valid interchange zone, false if not feasible.</returns>
        private bool ComputeUtility(int o, int d, IZone[] zones, int interchange, int accessStationIndex, int[] egressZones, float maxDistance, float[][] egressUtility, float[][] egressTime,
            int[][] selectedEgressZones, out float result, out float distance, out IZone egressZone)
        {
            float accessTravelTime;
            float v = ComputeAccessUtility( o, interchange, out accessTravelTime );
            // our total travel time / distance is the egress time plus the time it takes to get to the station to begin with
            distance = egressTime[accessStationIndex][d] + accessTravelTime;
            if ( egressTime[accessStationIndex][d] == 0 )
            {
                throw new XTMFRuntimeException( "In '" + this.Name + "' the egress time between zone " + zones[egressZones[accessStationIndex]].ZoneNumber + " and " + zones[d].ZoneNumber + " was equal to 0!" );
            }
            if ( distance < this.ComputeWeightedTimeWithoutRail( o, d ) )
            {
                var egressUtil = egressUtility[accessStationIndex][d];
                // Step 1, compute the egress station, and the utility from access station to egress to destination
                if ( distance < maxDistance & !float.IsNaN( v ) & !float.IsNaN( egressUtil ) )
                {
                    egressZone = zones[selectedEgressZones[accessStationIndex][d]];
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
        /// <param name="origin">The origin of the trip, flat</param>
        /// <param name="interchange">The access station, flat</param>
        /// <returns>The utility of picking the access station, NaN if it isn't possible</returns>
        private float ComputeAccessUtility(int origin, int interchange, out float weightedTravelTime)
        {
            float v = 0.0f;
            Time ivtt, boarding, wait, walk;
            float cost;
            // figure out which data to use
            if ( !this.PremiumTransitNetwork.GetAllData( origin, interchange, this.TimeOfDay, out ivtt, out walk, out wait, out boarding, out cost ) | ivtt <= Time.Zero )
            {
                if ( !this.TransitNetwork.GetAllData( origin, interchange, this.TimeOfDay, out ivtt, out walk, out wait, out boarding, out cost ) | walk <= Time.Zero )
                {
                    weightedTravelTime = float.NaN;
                    return float.NaN;
                }
            }
            // once we have the data we can then compute the utility
            v += this.IvttFactor * ivtt.ToMinutes()
                + this.WalkTimeFactor * walk.ToMinutes()
                + this.WaitTimeFactor * wait.ToMinutes()
                + this.BoardingFactor * boarding.ToMinutes()
                + this.CostFactor * cost;
            // we can also compute the weighted travel time here in order to avoid additional lookups
            weightedTravelTime = ivtt.ToMinutes()
                + this.EgressWaitPerception * wait.ToMinutes()
                + this.EgressWalkPerception * walk.ToMinutes();
            return v;
        }

        /// <summary>
        /// Compute which egress station to use
        /// </summary>
        /// <param name="interchange">The access zone to start from, flat</param>
        /// <param name="destination">The destination zone that we need to get to, flat</param>
        /// <param name="parking">The amount of parking available</param>
        /// <param name="egressZones">The list of all possible egress zones, flat</param>
        /// <param name="egressUtility">The utility of taking the given egress station</param>
        /// <param name="egressZone">The egress station to use for this access station</param>
        /// <returns>If we were successful in finding an egress station for this access station.</returns>
        private bool ComputeEgressStation(int interchange, int interchangeIndex, int destination, int[] egressZones, float[] trains, float[] parking, IZone[] zones, out float egressUtility, out float egressTime, out int egressZone)
        {
            int bestZone = -1;
            // Set the best utility initially to the time it takes to go from the access station to the destination
            // if we can't find an egress station better than this the Access -> Egress pair isn't valid
            float maxEgressTime = this.ComputeWeightedTimeWithoutRail( interchange, destination );
            float bestTravelTime = float.MaxValue;

            for ( int i = 0; i < egressZones.Length; i++ )
            {
                // you are not allowed to egress from the station you originally accessed
                float egressGeneralTime = ComputeWeightedTimeWithoutRail( egressZones[i], destination );
                var goTime = this.GoTransitNetwork.TravelTime( interchange, egressZones[i], this.TimeOfDay ).ToMinutes();
                if ( goTime <= 0 )
                {
                    continue;
                }
                if ( !float.IsNaN( egressGeneralTime ) & ( egressGeneralTime <= maxEgressTime ) )
                {
                    // Now add on the go transit time
                    egressGeneralTime += goTime;
                    if ( egressGeneralTime < bestTravelTime )
                    {
                        bestZone = egressZones[i];
                        bestTravelTime = egressGeneralTime;
                    }
                }
            }
            // If there is no egress station we are invalid
            if ( ( bestZone < 0 ) | ( bestZone == interchange ) )
            {
                egressUtility = float.NaN;
                egressZone = -1;
                egressTime = float.NaN;
                return false;
            }
            egressUtility = ComputeEgressStationUtility( interchange, bestZone, destination )
                + this.TrainsFactor * trains[interchange];
            egressZone = bestZone;
            egressTime = this.ComputeWeightedTimeWithoutRail( bestZone, destination );
            return true;
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
            Time ivtt, wait, walk, boardings;
            float cost;
            if ( !this.PremiumTransitNetwork.GetAllData( origin, destination, this.TimeOfDay, out ivtt, out walk, out wait, out boardings, out cost ) | ivtt <= Time.Zero )
            {
                if ( !this.TransitNetwork.GetAllData( origin, destination, this.TimeOfDay, out ivtt, out walk, out wait, out boardings, out cost ) | walk <= Time.Zero )
                {
                    return float.NaN;
                }
            }
            return ivtt.ToMinutes()
                    + this.EgressWaitPerception * wait.ToMinutes()
                    + this.EgressWalkPerception * walk.ToMinutes();
        }

        /// <summary>
        /// Compute the general time for the given egress zone.  This will be used
        /// to compare all of the possible egress stations for the one to use.
        /// </summary>
        /// <param name="interchange">The access zone to start from, flat</param>
        /// <param name="egress">The egress station to test,flat</param>
        /// <param name="destination">The final destination to reach, flat</param>
        /// <returns>The weighted time it takes to get to the destination from this access station.</returns>
        private float ComputeEgressGeneralTime(int interchange, int egress, int destination)
        {
            var goIvtt = this.GoTransitNetwork.InVehicleTravelTime( interchange, egress, this.TimeOfDay ).ToMinutes();
            float withoutGo;
            // don't bother to compute the weighted travel time if there is no 
            if ( goIvtt > 0 && !float.IsNaN( ( withoutGo = ComputeWeightedTimeWithoutRail( egress, destination ) ) ) )
            {
                return goIvtt + withoutGo;
            }
            return float.NaN;
        }

        /// <summary>
        /// Compute the utility of taking a particular egress station
        /// </summary>
        /// <param name="interchange">The access station to start from</param>
        /// <param name="egress">The station to get off at</param>
        /// <param name="destination">The destination to end the trip</param>
        /// <returns>The utility of using the egress station, NaN if no real egress station is used.</returns>
        private float ComputeEgressStationUtility(int interchange, int egress, int destination)
        {
            Time ivtt, wait, walk, boardings;
            float cost;
            var goIvtt = this.GoTransitNetwork.InVehicleTravelTime( interchange, egress, this.TimeOfDay );
            var goWalk = this.GoTransitNetwork.WalkTime( interchange, egress, this.TimeOfDay );
            var goCost = this.GoTransitNetwork.TravelCost( interchange, egress, this.TimeOfDay );
            if ( this.TransitNetwork.GetAllData( egress, destination, this.TimeOfDay, out ivtt, out walk, out wait, out boardings, out cost ) )
            {
                if ( ivtt <= Time.Zero )
                {
                    cost = 0f;
                }
                return this.IvttFactor * ( goIvtt + ivtt ).ToMinutes()
                    + this.WaitTimeFactor * wait.ToMinutes()
                    + this.WalkTimeFactor * ( goWalk + walk ).ToMinutes()
                    + this.BoardingFactor * boardings.ToMinutes()
                    + this.CostFactor * ( cost + goCost );
            }
            return float.NaN;
        }

        public void UnloadData()
        {
            this.Data = null;
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
            foreach ( var network in this.Root.NetworkData )
            {
                if ( network.NetworkType == this.TransitNetworkString )
                {
                    this.TransitNetwork = network as ITripComponentData;
                }
                if ( network.NetworkType == this.GoTransitNetworkString )
                {
                    this.GoTransitNetwork = network as ITripComponentData;
                }
                if ( network.NetworkType == this.PremiumTransitNetworkString )
                {
                    this.PremiumTransitNetwork = network as ITripComponentData;
                }
            }
            // Make sure the networks we require are available
            if ( this.PremiumTransitNetwork == null )
            {
                error = "In '" + this.Name + "' a premium transit network named '" + this.PremiumTransitNetworkString + "' could not be found.";
                return false;
            }
            if ( this.TransitNetwork == null )
            {
                error = "In '" + this.Name + "' a transit network named '" + this.TransitNetworkString + "' could not be found.";
                return false;
            }
            if ( this.GoTransitNetwork == null )
            {
                error = "In '" + this.Name + "' a go transit network named '" + this.GoTransitNetworkString + "' could not be found.";
                return false;
            }
            return true;
        }
    }
}
