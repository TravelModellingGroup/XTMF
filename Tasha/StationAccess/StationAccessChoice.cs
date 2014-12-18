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
using System.IO;
using XTMF;
using TMG;
using Tasha.Common;
using TMG.DirectCompute;
using Datastructure;
using System.Threading.Tasks;
using Tasha.ModeChoice;
using TMG.Input;
namespace Tasha.StationAccess
{

    [ModuleInformation(Description =
        @"StationAccessChoice is designed to provide the integration for driving to stations in order to take transit.")]
    public class StationAccessChoice : IAccessStationChoiceModel
    {
        [RootModule]
        public ITashaRuntime Root;

        [RunParameter("Station Zone Ranges", "9000-9999", typeof(RangeSet),
            "A set of ranges that describe which zones represent the stations to have drive access for.")]
        public RangeSet StationZoneRanges;

        [RunParameter("Spatial Zones", "1-5999", typeof(RangeSet),
            "The zone numbers for physical zones that we wish to compute.")]
        public RangeSet SpatialZones;

        [RunParameter("Mode Name", "DAT", typeof(string), "The name of the mode we should be looking for inside of the trip chain.")]
        public string OurModeName;

        private ITashaMode OurMode;

        public int[] ClosestStation;

        [RunParameter("Minimum Station Utility", 4.53999297624e-5f, "The minimum utility a station is allowed to have.")]
        public float MinimumStationUtility;

        [SubModelInformation(Required = true, Description = "Describes the station data.(Origin = Station, Data = capacity)")]
        public IReadODData<float> StationCapacity;

        private SparseArray<float> Capacity;

        public sealed class TimePeriod : IModule
        {
            [Parameter("StartTime", "6:00AM", typeof(Time), "The start of the time period inclusive")]
            public Time StartTime;

            [Parameter("EndTime", "9:00AM", typeof(Time), "The end of the time period exclusive")]
            public Time EndTime;

            [RunParameter("Auto Network Name", "Auto", "The name of the auto network to use.")]
            public string AutoNetworkName;

            [RunParameter("Transit Network Name", "Transit", "The name of the transit network to use.")]
            public string TransitNetworkName;

            [RunParameter("aivtt", 0.0f, "Auto in vehicle time factor.")]
            public float AIVTT;

            [RunParameter("acost", 0.0f, "Auto cost factor.")]
            public float AutoCost;

            [RunParameter("tivtt", 0.0f, "Transit in vehicle time factor for local transit.")]
            public float TIVTT;

            [RunParameter("rivtt", 0.0f, "Transit in vehicle time factor for rail.")]
            public float RIVTT;

            [RunParameter("twalk", 0.0f, "Transit walk time factor.")]
            public float TWALK;

            [RunParameter("twait", 0.0f, "Transit wait time factor.")]
            public float TWAIT;

            [RunParameter("tfare", 0.0f, "Transit fare factor.")]
            public float TransitFare;

            [RunParameter("Capacity", 0.0f, "The weight applied to the capacity of the station.")]
            public float Capacity;

            [RunParameter("CapacityFactor", 0.0f, "The exponential to apply to the capacity factor for the station.")]
            public float CapacityFactorExp;

            [RunParameter("Parking Cost", 0.0f, "The weight applied to the capacity of the station.")]
            public float ParkingCost;

            [RunParameter("Closest Station", 0.0f, "A constant to add for the closest station.")]
            public float ClosestStationFactor;

            internal SparseArray<float> CapacityFactor;

            [SubModelInformation(Required = true, Description = "The source used to read in the capacity factors")]
            public IDataSource<SparseArray<float>> CapacityFactorSource;

            public string Name { get; set; }

            public float Progress { get { return 0f; } }

            public Tuple<byte, byte, byte> ProgressColour { get { return null; } }

            [RootModule]
            public ITravelDemandModel Root;

            internal float[] AccessFromOrigin;
            internal float[] EgressFromOrigin;

            internal float[] AccessToDestination;
            internal float[] EgressToDestination;

            private IZone[] zones;


            internal void Load(RangeSet stationRanges, RangeSet spatialZones, SparseArray<float> capacity, int[] closestStation)
            {
                LoadCapacityFactors();
                CalculateUtilities(stationRanges, spatialZones, capacity.GetFlatData(), closestStation);
            }

            private void CalculateUtilities(RangeSet stationRanges, RangeSet spatialZones, float[] capacity, int[] closestStation)
            {
                INetworkData autoNetwork = GetNetwork(AutoNetworkName);
                ITripComponentData transitNetwork = GetNetwork(TransitNetworkName) as ITripComponentData;
                EnsureNetworks(autoNetwork, transitNetwork);
                var zoneArray = Root.ZoneSystem.ZoneArray;
                zones = zoneArray.GetFlatData();


                int[] stationZones = GetStationZones(stationRanges, capacity, zones);
                var flatCapacityFactor = CapacityFactor.GetFlatData();
                if(AccessFromOrigin == null)
                {
                    AccessToDestination = new float[stationZones.Length * zones.Length];
                    AccessFromOrigin = new float[stationZones.Length * zones.Length];
                    EgressToDestination = new float[stationZones.Length * zones.Length];
                    EgressFromOrigin = new float[stationZones.Length * zones.Length];
                }
                // compute the toAccess utilities
                Parallel.For(0, zones.Length, (int originIndex) =>
                {
                    var zoneNumber = zones[originIndex].ZoneNumber;
                    if(spatialZones.Contains(zoneNumber))
                    {
                        for(int i = 0; i < stationZones.Length; i++)
                        {
                            var accessIndex = stationZones[i];
                            var factor = (float)Math.Pow(flatCapacityFactor[accessIndex], CapacityFactorExp);
                            // calculate access' to access station this will include more factors
                            AccessFromOrigin[originIndex * stationZones.Length + i] = (float)Math.Exp(ComputeUtility(autoNetwork, originIndex, accessIndex)
                                + (Capacity * capacity[accessIndex]
                                + ParkingCost * zones[accessIndex].ParkingCost
                                + (closestStation[originIndex] == accessIndex ? ClosestStationFactor : 0))) * factor;
                            // calculate egress' from access station
                            EgressFromOrigin[originIndex * stationZones.Length + i] = (float)Math.Exp(ComputeUtility(autoNetwork, accessIndex, originIndex)) * factor;
                        }
                    }
                });

                // compute the toDesinstination utilities
                Parallel.For(0, zones.Length, (int destIndex) =>
                {
                    var zoneNumber = zones[destIndex].ZoneNumber;
                    if(spatialZones.Contains(zoneNumber))
                    {
                        for(int i = 0; i < stationZones.Length; i++)
                        {
                            var accessIndex = stationZones[i];
                            var factor = (float)Math.Pow(flatCapacityFactor[accessIndex], CapacityFactorExp);
                            // calculate access' to destination
                            AccessToDestination[destIndex * stationZones.Length + i] = (float)Math.Exp(ComputeUtility(transitNetwork, accessIndex, destIndex)) * factor;
                            // calculate egress' to access station
                            EgressToDestination[destIndex * stationZones.Length + i] = (float)Math.Exp(ComputeUtility(transitNetwork, destIndex, accessIndex)) * factor;
                        }
                    }
                });
            }

            private float ComputeUtility(INetworkData autoNetwork, int originIndex, int destinationIndex)
            {
                autoNetwork.GetAllData(originIndex, destinationIndex, StartTime, out float aivtt, out float cost);
                return AIVTT * aivtt + AutoCost * cost;
            }

            private float ComputeUtility(ITripComponentData transitNetwork, int originIndex, int destIndex)
            {
                if(transitNetwork.GetAllData(originIndex, destIndex, StartTime, out float ivtt, out float walk, out float wait, out float railTime, out float cost) && (railTime > 0))
                {
                    return TIVTT * (ivtt - railTime)
                        + RIVTT * railTime
                        + TWALK * walk
                        + TWAIT * wait
                        + TransitFare * cost;
                }
                return float.NegativeInfinity;
            }

            private void EnsureNetworks(INetworkData autoNetwork, ITripComponentData transitNetwork)
            {
                if(autoNetwork == null)
                {
                    throw new XTMFRuntimeException("In '" + Name + "' we were unable to find an auto network named '" + AutoNetworkName + "'!");
                }
                if(transitNetwork == null)
                {
                    throw new XTMFRuntimeException("In '" + Name + "' we were unable to find an transit network named '" + TransitNetworkName + "'!");
                }
            }

            private INetworkData GetNetwork(string networkName)
            {
                foreach(var network in Root.NetworkData)
                {
                    if(network.NetworkType == networkName) return network;
                }
                return null;
            }

            private void LoadCapacityFactors()
            {
                try
                {
                    CapacityFactorSource.LoadData();
                    CapacityFactor = CapacityFactorSource.GiveData();
                    CapacityFactorSource.UnloadData();
                }
                catch
                {
                    // if we were unable to load it properly make sure that it is unloaded
                    CapacityFactorSource.UnloadData();
                    CapacityFactor = Root.ZoneSystem.ZoneArray.CreateSimilarArray<float>();
                    var flat = CapacityFactor.GetFlatData();
                    for(int i = 0; i < flat.Length; i++)
                    {
                        flat[i] = 1.0f;
                    }
                }
            }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        [SubModelInformation(Required = false, Description = "The per time period information")]
        public TimePeriod[] TimePeriods;

        private bool FirstLoad = true;

        private IZone[] AccessZones;
        private IZone[] zones;
        private int[] AccessZoneIndexes;

        public void Load()
        {
            Console.WriteLine("Loading Station Access Choice...");
            if(FirstLoad == true)
            {
                LoadMode();
                LoadStationCapacity();
                GetAccessZones();
                AssignClosestStations();
                zones = Root.ZoneSystem.ZoneArray.GetFlatData();
                FirstLoad = false;
            }
            LoadTimePeriods();
            Console.WriteLine("Finished Loading Station Access Choice...");
        }

        private void LoadMode()
        {
            foreach(var mode in Root.AllModes)
            {
                if(mode.ModeName == OurModeName)
                {
                    OurMode = mode;
                    return;
                }
            }
            throw new XTMFRuntimeException("In '" + Name + "' we were unable to find a mode named '" + OurModeName + "'.");
        }

        private void AssignClosestStations()
        {
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var temp = new int[zones.Length];
            Parallel.For(0, temp.Length, (int i) =>
            {
                var origin = zones[i];
                int bestIndex = 0;
                double bestDistance = Distance(origin, AccessZones[0]);
                for(int j = 1; j < AccessZones.Length; j++)
                {
                    if((var dist = Distance(origin, AccessZones[j])) < bestDistance)
                    {
                        bestIndex = j;
                        bestDistance = dist;
                    }
                }
                temp[i] = AccessZoneIndexes[bestIndex];
            });
            ClosestStation = temp;
        }

        private static double Distance(IZone origin, IZone accessZone)
        {
            double originX = origin.X, originY = origin.Y;
            double accessX = accessZone.X, accessY = accessZone.Y;
            return Math.Sqrt((originX - accessX) * (originX - accessX)
                            + (originY - accessY) * (originY - accessY));
        }

        private void GetAccessZones()
        {
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var indexes = GetStationZones(StationZoneRanges, Capacity.GetFlatData(), zones);
            AccessZones = new IZone[indexes.Length];
            for(int i = 0; i < indexes.Length; i++)
            {
                AccessZones[i] = zones[indexes[i]];
            }
            AccessZoneIndexes = indexes;
        }

        private void LoadTimePeriods()
        {
            for(int i = 0; i < TimePeriods.Length; i++)
            {
                TimePeriods[i].Load(StationZoneRanges, SpatialZones, Capacity, ClosestStation);
            }
        }

        private void LoadStationCapacity()
        {
            SparseArray<float> capacity = Root.ZoneSystem.ZoneArray.CreateSimilarArray<float>();
            foreach(var point in StationCapacity.Read())
            {
                if(!capacity.ContainsIndex(point.O))
                {
                    throw new XTMFRuntimeException("In '" + Name + "' we found an invalid zone '" + point.O + "' while reading in the station capacities!");
                }
                // use the log of capacity
                capacity[point.O] = (float)Math.Log(point.Data + 1.0f);
            }
            Capacity = capacity;
        }

        public Pair<IZone[], float[]> ProduceResult(ITripChain data)
        {
            if(GetTripsFirst(data, out ITrip first, out ITrip second))
            {
                if(first == null | second == null) return null;
                TimePeriod firstTimePeriod = GetTimePeriod(first);
                TimePeriod secondTimePeriod = GetTimePeriod(second);
                if(firstTimePeriod == null | secondTimePeriod == null) return null;
                var zoneArray = Root.ZoneSystem.ZoneArray;
                float[] utilities = new float[AccessZoneIndexes.Length];
                var firstOrigin = zoneArray.GetFlatIndex(first.OriginalZone.ZoneNumber) * AccessZoneIndexes.Length;
                var firstDestination = zoneArray.GetFlatIndex(first.DestinationZone.ZoneNumber) * AccessZoneIndexes.Length;
                var secondOrigin = zoneArray.GetFlatIndex(second.OriginalZone.ZoneNumber) * AccessZoneIndexes.Length;
                var secondDestination = zoneArray.GetFlatIndex(second.DestinationZone.ZoneNumber) * AccessZoneIndexes.Length;
                for(int i = 0; i < utilities.Length; i++)
                {
                    utilities[i] = firstTimePeriod.AccessFromOrigin[firstOrigin + i] * firstTimePeriod.AccessToDestination[secondDestination + i]
                                   * secondTimePeriod.EgressToDestination[secondOrigin + i] * secondTimePeriod.EgressFromOrigin[secondDestination + i];
                    // this will get rid of NaN's as well
                    if(!(utilities[i] >= MinimumStationUtility))
                    {
                        utilities[i] = 0.0f;
                    }
                }
                return new Pair<IZone[], float[]>(AccessZones, utilities);
            }
            return null;
        }

        private TimePeriod GetTimePeriod(ITrip first)
        {
            var time = first.TripStartTime;
            for(int i = 0; i < TimePeriods.Length; i++)
            {
                if(time >= TimePeriods[i].StartTime && time <= TimePeriods[i].EndTime)
                {
                    return TimePeriods[i];
                }
            }
            return null;
        }

        private bool GetTripsFirst(ITripChain tc, out ITrip trip1, out ITrip trip2)
        {
            var list = tc.Trips;
            int i = 0;
            trip1 = null;
            trip2 = null;
            for(; i < list.Count; i++)
            {
                if(list[i].Mode == OurMode)
                {
                    trip1 = list[i++];
                    break;
                }
            }
            for(; i < list.Count; i++)
            {
                if(list[i].Mode == OurMode)
                {
                    trip2 = list[i++];
                    break;
                }
            }
            // if we get in here and find another trip, then we have more than 2 and is thus invalid
            for(; i < list.Count; i++)
            {
                if(list[i].Mode == OurMode)
                {
                    return false;
                }
            }
            return trip1 != null & trip2 != null;
        }

        internal static int[] GetStationZones(RangeSet stationRanges, float[] capacity, IZone[] zones)
        {
            List<int> validStationIndexes = new List<int>();
            for(int i = 0; i < zones.Length; i++)
            {
                if(capacity[i] > 0 && stationRanges.Contains(zones[i].ZoneNumber))
                {
                    validStationIndexes.Add(i);
                }
            }
            return validStationIndexes.ToArray();
        }

        public void Unload()
        {

        }


        public string Name
        {
            get;
            set;
        }

        private Func<float> _Progress = () => 0f;


        public float Progress
        {
            get { return _Progress(); }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
