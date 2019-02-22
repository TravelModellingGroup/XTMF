using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF;
using TMG;
using TMG.Functions;
using Tasha.Common;
using Datastructure;
using TMG.Input;

namespace Tasha.StationAccess
{
    [ModuleInformation(Description = "Designed to be used with Passenger Access / Egress Transit for GTAModelV4.1")]
    public sealed class PerceivedTimePassengerStationAccessChoice : ICalculation<ITrip, Pair<IZone[], float[]>>
    {
        [RootModule]
        public ITravelDemandModel Root;

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50,150,50);

        private SparseArray<IZone> _zones;

        [RunParameter("Auto Access", true, "Should the auto component of the trip be for the access (true) or egress (false).")]
        public bool AutoAccess;

        [RunParameter("Auto Network", "Auto", "The name of the auto network data to use.")]
        public string AutoNetwork;

        internal INetworkCompleteData _autoNetwork;

        [RunParameter("Transit Network", "Transit", "The name of the transit network data to use.")]
        public string TransitNetwork;

        [RunParameter("Station Zone Ranges", "8000-9999", typeof(RangeSet),
"A set of ranges that describe which zones represent the stations to have drive access for.")]
        public RangeSet StationZoneRanges;

        [RunParameter("Spatial Zones", "1-5999", typeof(RangeSet),
    "The zone numbers for physical zones that we wish to compute.")]
        public RangeSet SpatialZones;

        [SubModelInformation(Required = true, Description = "Describes the station data.(Origin = Station, Data = capacity)")]
        public IReadODData<float> StationCapacity;

        internal ITripComponentCompleteData _transitNetwork;

        // The time periods will reference these
        private int[] _stationIndexes;
        private float[] _logStationCapacity;
        private IZone[] _stationZones;
        private int[] _closestStation;

        internal static int[] GetStationZones(RangeSet stationRanges, float[] capacity, IZone[] zones)
        {
            List<int> validStationIndexes = new List<int>();
            for (int i = 0; i < zones.Length; i++)
            {
                if (capacity[i] > 0 && stationRanges.Contains(zones[i].ZoneNumber))
                {
                    validStationIndexes.Add(i);
                }
            }
            return validStationIndexes.ToArray();
        }

        public void Load()
        {
            _zones = Root.ZoneSystem.ZoneArray;
            var flatZones = _zones.GetFlatData();
            LoadStationCapacity();
            _stationIndexes = GetStationZones(StationZoneRanges, _logStationCapacity, _zones.GetFlatData());
            _stationZones = _stationIndexes.Select(index => flatZones[index]).ToArray();
            foreach (var timePeriod in TimePeriods)
            {
                timePeriod.Load();
            }
        }

        private static double Distance(IZone origin, IZone accessZone)
        {
            double originX = origin.X, originY = origin.Y;
            double accessX = accessZone.X, accessY = accessZone.Y;
            return Math.Sqrt((originX - accessX) * (originX - accessX)
                            + (originY - accessY) * (originY - accessY));
        }

        private void LoadStationCapacity()
        {
            SparseArray<float> capacity = Root.ZoneSystem.ZoneArray.CreateSimilarArray<float>();
            foreach (var point in StationCapacity.Read())
            {
                if (!capacity.ContainsIndex(point.O))
                {
                    throw new XTMFRuntimeException(this, "In '" + Name + "' we found an invalid zone '" + point.O + "' while reading in the station capacities!");
                }
                // use the log of capacity
                capacity[point.O] = (float)Math.Log(point.Data + 1.0f);
            }
            _logStationCapacity = capacity.GetFlatData();
        }

        private void AssignClosestStations()
        {
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var temp = new int[zones.Length];
            Parallel.For(0, temp.Length, i =>
            {
                var origin = zones[i];
                int bestIndex = 0;
                double bestDistance = Distance(origin, _stationZones[0]);
                for (int j = 1; j < _stationIndexes.Length; j++)
                {
                    double dist;
                    if ((dist = Distance(origin, _stationZones[j])) < bestDistance)
                    {
                        bestIndex = j;
                        bestDistance = dist;
                    }
                }
                temp[i] = _stationIndexes[bestIndex];
            });
            _closestStation = temp;
        }

        public sealed class TimePeriod : IModule
        {
            [RootModule]
            public ITravelDemandModel Root;

            [ParentModel]
            public PerceivedTimePassengerStationAccessChoice Parent;

            public string Name { get; set; }

            public float Progress => 0f;

            public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

            private int[] _stationIndexes;
            private float[] _logStationCapacity;
            private IZone[] _stationZones;

            /// <summary>
            /// [O,Station]
            /// </summary>
            private float[][] AccessUtil;

            /// <summary>
            /// [D, Station]
            /// </summary>
            private float[][] EgressUtil;

            [RunParameter("AutoTime", 0f, "The scale to apply to the auto time")]
            public float BAutoTime;

            [RunParameter("Transit Perceived Time", 0f, "The scale to apply to the transit perceived time.")]
            public float BTransitPerceivedTime;

            [RunParameter("Cost", 0f, "The scale to apply to each segment's cost.")]
            public float BCost;

            [RunParameter("Capacity", 0f, "The scale to apply to the log of the station's parking capacity.")]
            public float BCapacity;

            [RunParameter("Closest Station", 0f, "The scale to apply if the station is the closest to the access.")]
            public float BClosestStation;

            public Time StartTime;

            public Time EndTime;

            public void Load()
            {
                var auto = Parent._autoNetwork.GetTimePeriodData(StartTime);
                var transit = Parent._transitNetwork.GetTimePeriodData(StartTime);
                var zones = Root.ZoneSystem.ZoneArray;
                _stationZones = Parent._stationZones;
                _logStationCapacity = Parent._logStationCapacity;
                _stationIndexes = Parent._stationIndexes;
                var numberOfZones = Parent._zones.Count;
                AccessUtil = new float[numberOfZones][];
                EgressUtil = new float[numberOfZones][];
                for (int i = 0; i < numberOfZones; i++)
                {
                    AccessUtil[i] = new float[_stationZones.Length];
                    EgressUtil[i] = new float[_stationZones.Length];
                }
                for (int s = 0; s < _stationIndexes.Length; s++)
                {
                    var stn = _stationIndexes[s];
                    if (Parent.AutoAccess)
                    {
                        for (int o = 0; o < AccessUtil.Length; o++)
                        {
                            var i = GetAutoDataIndex(o, stn, numberOfZones);
                            AccessUtil[o][s] = (float)Math.Exp(BAutoTime * auto[i]
                                                + BCost * auto[i + 1]
                                                + BCapacity * _logStationCapacity[stn]);
                        }
                        for (int d = 0; d < EgressUtil.Length; d++)
                        {
                            var i = GetTransitDataIndex(stn, d, numberOfZones);
                            EgressUtil[d][s] = (float)Math.Exp(BTransitPerceivedTime * transit[i]
                                               + BCost * transit[i + 4]);
                        }
                    }
                    else
                    {
                        for (int o = 0; o < AccessUtil.Length; o++)
                        {
                            var i = GetAutoDataIndex(o, stn, numberOfZones);
                            AccessUtil[o][s] = (float)Math.Exp(BTransitPerceivedTime * transit[i]
                                               + BCost * transit[i + 4]
                                               + BCapacity * _logStationCapacity[stn]);
                        }
                        for (int d = 0; d < EgressUtil.Length; d++)
                        {
                            var i = GetTransitDataIndex(stn, d, numberOfZones);
                            EgressUtil[d][s] = (float)Math.Exp(BAutoTime * auto[i]
                                                + BCost * auto[i + 1]);
                        }
                    }
                }
            }

            private static int GetAutoDataIndex(int origin, int destination, int numberOfZones)
            {
                return ((origin * numberOfZones + destination) * numberOfZones) * 2;
            }

            private static int GetTransitDataIndex(int origin, int destination, int numberOfZones)
            {
                return ((origin * numberOfZones + destination) * numberOfZones) * 5;
            }

            internal Pair<IZone[], float[]> ProduceResult(Time time, int origin, int destination)
            {
                // return null if it is outside of our time period.
                if(time < StartTime | time >= EndTime)
                {
                    return null;
                }
                var probs = new float[_stationIndexes.Length];
                // get the e raised to the systematic utilities of each component
                VectorHelper.Multiply(probs, 0, AccessUtil[origin], 0, EgressUtil[destination], 0, probs.Length);
                // and normalize them to get the logit probability for each choice
                VectorHelper.Multiply(probs, probs, 1f / VectorHelper.Sum(probs, 0, probs.Length));
                return new Pair<IZone[], float[]>(_stationZones, probs);
            }

            public void Unload()
            {
                AccessUtil = null;
                EgressUtil = null;
            }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        [SubModelInformation(Required = false, Description = "The time periods to model.")]
        public TimePeriod[] TimePeriods;

        public Pair<IZone[], float[]> ProduceResult(ITrip data)
        {
            var o = _zones.GetFlatIndex(data.OriginalZone.ZoneNumber);
            var d = _zones.GetFlatIndex(data.DestinationZone.ZoneNumber);
            var time = data.ActivityStartTime;
            foreach(var period in TimePeriods)
            {
                Pair<IZone[], float[]> ret;
                if ((ret = period.ProduceResult(time, o, d)) != null)
                {
                    return ret;
                }
            }
            return null;
        }

        public bool RuntimeValidation(ref string error)
        {
            if((_autoNetwork = Root.NetworkData.FirstOrDefault(n => n.NetworkType == AutoNetwork) as INetworkCompleteData) == null)
            {
                error = $"Unable to find an auto network with the name {AutoNetwork}!";
                return false;
            }
            if ((_transitNetwork = Root.NetworkData.FirstOrDefault(n => n.NetworkType == TransitNetwork) as ITripComponentCompleteData) == null)
            {
                error = $"Unable to find a transit network with the name {TransitNetwork}!";
                return false;
            }
            return true;
        }

        public void Unload()
        {
            _stationIndexes = null;
            _logStationCapacity = null;
            _closestStation = null;
            _stationZones = null;
            foreach(var timePeriod in TimePeriods)
            {
                timePeriod.Unload();
            }
        }
    }
}
