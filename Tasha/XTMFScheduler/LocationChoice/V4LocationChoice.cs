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
using System.Threading.Tasks;
using Datastructure;
using Tasha.Common;
using Tasha.Scheduler;
using TMG;
using XTMF;
using TMG.Functions;

namespace Tasha.XTMFScheduler.LocationChoice
{

    public sealed class V4LocationChoice : ILocationChoiceModel
    {
        [RunParameter("Valid Destination Zones", "1-6999", typeof(RangeSet), "The valid zones to use.")]
        public RangeSet ValidDestinationZones;

        [RootModule]
        public ITravelDemandModel Root;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [SubModelInformation(Required = true, Description = "")]
        public IResource ProfessionalFullTime;
        [SubModelInformation(Required = true, Description = "")]
        public IResource ProfessionalPartTime;

        [SubModelInformation(Required = true, Description = "")]
        public IResource GeneralFullTime;
        [SubModelInformation(Required = true, Description = "")]
        public IResource GeneralPartTime;

        [SubModelInformation(Required = true, Description = "")]
        public IResource RetailFullTime;
        [SubModelInformation(Required = true, Description = "")]
        public IResource RetailPartTime;

        [SubModelInformation(Required = true, Description = "")]
        public IResource ManufacturingFullTime;
        [SubModelInformation(Required = true, Description = "")]
        public IResource ManufacturingPartTime;

        [RunParameter("Auto Network Name", "Auto", "The name of the network to use for computing auto times.")]
        public string AutoNetworkName;

        [RunParameter("Transit Network Name", "Transit", "The name of the network to use for computing transit times.")]
        public string TransitNetworkName;

        public IZone GetLocation(IEpisode ep, Random random)
        {
            var episodes = ep.ContainingSchedule.Episodes;
            var startTime = ep.StartTime;
            int i = 0;
            for(; i < episodes.Length; i++)
            {
                if(episodes[i] == null) break;
                if(startTime < episodes[i].StartTime)
                {
                    return GetLocation(ep, random, (i == 0 ? null : episodes[i - 1]), episodes[i], startTime);
                }
            }
            return GetLocation(ep, random, (i > 0 ? episodes[i - 1] : null), null, startTime);
        }

        [ThreadStatic]
        private static float[] CalculationSpace;

        private System.Collections.Concurrent.ConcurrentStack<float[]> CalculationPool = new System.Collections.Concurrent.ConcurrentStack<float[]>();

        private IZone GetLocation(IEpisode ep, Random random, IEpisode previous, IEpisode next, Time startTime)
        {
            var previousZone = GetZone(previous, ep);
            var nextZone = GetZone(next, ep);
            var calculationSpace = CalculationSpace;
            if(calculationSpace == null)
            {
                CalculationSpace = calculationSpace = new float[Root.ZoneSystem.ZoneArray.Count];
            }
            Time availableTime = ComputeAvailableTime(previous, next);
            switch(ep.ActivityType)
            {
                case Activity.Market:
                case Activity.JointMarket:
                    return MarketModel.GetLocation(previousZone, ep, nextZone, startTime, availableTime, calculationSpace, random);
                case Activity.JointOther:
                case Activity.IndividualOther:
                    return OtherModel.GetLocation(previousZone, ep, nextZone, startTime, availableTime, calculationSpace, random);
                case Activity.WorkBasedBusiness:
                case Activity.SecondaryWork:
                    return WorkBasedBusinessModel.GetLocation(previousZone, ep, nextZone, startTime, availableTime, calculationSpace, random);
            }
            // if it isn't something that we understand just accept its previous zone
            return ep.Zone;
        }

        [RunParameter("Maximum Episode Duration Compression", 0.5f, "The amount that the duration is allowed to be compressed from the original duration time (0 to 1 default is 0.5).")]
        public float MaximumEpisodeDurationCompression;

        private Time ComputeAvailableTime(IEpisode previous, IEpisode next)
        {
            return (next == null ? Time.EndOfDay : (next.StartTime + next.Duration - (MaximumEpisodeDurationCompression * next.OriginalDuration)))
                - (previous == null ? Time.StartOfDay : previous.EndTime - previous.Duration - (MaximumEpisodeDurationCompression * previous.OriginalDuration));
        }

        public sealed class TimePeriod : IModule
        {
            [RootModule]
            public ITravelDemandModel Root;

            [ParentModel]
            public V4LocationChoice Parent;

            [RunParameter("Start Time", "6:00AM", typeof(Time), "The time this period starts at.")]
            public Time StartTime;

            [RunParameter("End Time", "9:00AM", typeof(Time), "The time this period ends at (exclusive).")]
            public Time EndTime;

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public float[] RowTravelTimes;
            public float[] ColumnTravelTimes;

            public void Load()
            {
                var size = Root.ZoneSystem.ZoneArray.Count;
                var rowData = RowTravelTimes == null ? new float[size * size] : RowTravelTimes;
                var columnData = ColumnTravelTimes == null ? new float[size * size] : ColumnTravelTimes;
                var network = Parent.AutoNetwork;
                Parallel.For(0, size, (int i) =>
                {
                    var time = StartTime;
                    int startingIndex = i * size;
                    for(int j = 0; j < size; j++)
                    {
                        var ijTime = network.TravelTime(i, j, time).ToMinutes();
                        rowData[startingIndex + j] = ijTime;
                        columnData[j * size + i] = ijTime;
                    }
                });
                RowTravelTimes = rowData;
                ColumnTravelTimes = columnData;
            }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }


        public abstract class LocationChoiceActivity : IModule
        {
            [RootModule]
            public ITravelDemandModel Root;

            [ParentModel]
            public V4LocationChoice Parent;

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            /// <summary>
            /// To[timePeriod][o * #zones + d]
            /// </summary>
            protected float[][] To;
            /// <summary>
            /// From[timePeriod][o * #zones + d]
            /// </summary>
            protected float[][] From;

            [RunParameter("Professional FullTime", "0.0", typeof(float), "The weight applied for the worker category.")]
            public float ProfessionalFullTime;
            [RunParameter("Professional PartTime", "0.0", typeof(float), "The weight applied for the worker category.")]
            public float ProfessionalPartTime;
            [RunParameter("General FullTime", "0.0", typeof(float), "The weight applied for the worker category.")]
            public float GeneralFullTime;
            [RunParameter("General PartTime", "0.0", typeof(float), "The weight applied for the worker category.")]
            public float GeneralPartTime;
            [RunParameter("Sales FullTime", "0.0", typeof(float), "The weight applied for the worker category.")]
            public float RetailFullTime;
            [RunParameter("Sales PartTime", "0.0", typeof(float), "The weight applied for the worker category.")]
            public float RetailPartTime;
            [RunParameter("Manufacturing FullTime", "0.0", typeof(float), "The weight applied for the worker category.")]
            public float ManufacturingFullTime;
            [RunParameter("Manufacturing PartTime", "0.0", typeof(float), "The weight applied for the worker category.")]
            public float ManufacturingPartTime;
            [RunParameter("Population", "0.0", typeof(float), "The weight applied for the log of the population in the zone.")]
            public float Population;
            [RunParameter("Auto TravelTime", "0.0", typeof(float), "The weight applied for the travel time from origin to zone to final destination.")]
            public float AutoTime;
            [RunParameter("Transit IVTT", "0.0", typeof(float), "The weight applied for the in vehicle travel time travel time from origin to zone to final destination.")]
            public float TransitTime;
            [RunParameter("Transit Walk", "0.0", typeof(float), "The weight applied for the walk time travel time from origin to zone to final destination.")]
            public float TransitWalk;
            [RunParameter("Transit Wait", "0.0", typeof(float), "The weight applied for the wait travel time travel time from origin to zone to final destination.")]
            public float TransitWait;
            [RunParameter("Transit Boarding", "0.0", typeof(float), "The weight applied for the boarding penalties from origin to zone to final destination.")]
            public float TransitBoarding;
            [RunParameter("Cost", "0.0", typeof(float), "The weight applied for the cost from origin to zone to final destination.")]
            public float Cost;
            [RunParameter("Same PD", 0.0f, "The constant applied if the zone of interest is the same as both the previous and next planning districts.")]
            public float SamePD;

            public SpatialRegion[] PDConstant;
            public ODConstant[] ODConstants;
            private float expSamePD;
            private int[][][] PDCube;

            private double GetTransitUtility(ITripComponentData network, int i, int j, Time time)
            {
                float ivtt, walk, wait, cost, boarding;
                if(!network.GetAllData(i, j, time, out ivtt, out walk, out wait, out boarding, out cost))
                {
                    return 0f;
                }
                return Math.Exp(
                      TransitTime * ivtt 
                    + TransitWalk * walk 
                    + TransitWait * wait
                    + Cost * cost);
            }

            protected float GetTravelLogsum(INetworkData autoNetwork, ITripComponentData transitNetwork, int i, int j, Time time)
            {
                float ivtt, cost;
                if(!autoNetwork.GetAllData(i, j, time, out ivtt, out cost))
                {
                    return 0.0f;
                }
                return (float)(GetTransitUtility(transitNetwork, i, j, time)
                    + Math.Exp( ivtt * AutoTime + cost * Cost));
            }

            public sealed class ODConstant : IModule
            {
                [RunParameter("Previous PD Range", "1", typeof(RangeSet), "The planning districts for the previous zone.")]
                public RangeSet Previous;

                [RunParameter("Next PD Range", "1", typeof(RangeSet), "The planning districts for the next zone.")]
                public RangeSet Next;

                [RunParameter("Interest PD Range", "1", typeof(RangeSet), "The planning districts the zone we are interested in.")]
                public RangeSet Interest;

                [RunParameter("Constant", 0.0f, "The constant applied if the spacial category is met.")]
                public float Constant;
                internal float ExpConstant;

                public string Name { get; set; }

                public float Progress { get; set; }

                public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

                public bool RuntimeValidation(ref string error)
                {
                    return true;
                }
            }

            public sealed class SpatialRegion : IModule
            {
                [RunParameter("PDRange", "1", typeof(RangeSet), "The planning districts that constitute this spatial segment.")]
                public RangeSet Range;

                [RunParameter("Constant", 0.0f, "The constant applied if the spacial category is met.")]
                public float Constant;

                public string Name { get; set; }

                public float Progress { get; set; }

                public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

                public bool RuntimeValidation(ref string error)
                {
                    return true;
                }
            }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }

            private SparseArray<IZone> zoneSystem;
            private IZone[] zones;
            private int[] FlatZoneToPDCubeLookup;

            internal void Load()
            {
                var timePeriods = Parent.TimePeriods;
                zoneSystem = Root.ZoneSystem.ZoneArray;
                zones = zoneSystem.GetFlatData();
                if(To == null)
                {
                    To = new float[timePeriods.Length][];
                    From = new float[timePeriods.Length][];
                    for(int i = 0; i < timePeriods.Length; i++)
                    {
                        To[i] = new float[zones.Length * zones.Length];
                        From[i] = new float[zones.Length * zones.Length];
                    }
                }
                expSamePD = (float)Math.Exp(SamePD);
                // raise the constants to e^constant to save CPU time during the main phase
                for(int i = 0; i < ODConstants.Length; i++)
                {
                    ODConstants[i].ExpConstant = (float)Math.Exp(ODConstants[i].Constant);
                }
                var pds = TMG.Functions.ZoneSystemHelper.CreatePDArray<float>(Root.ZoneSystem.ZoneArray);
                BuildPDCube(pds);
                if(FlatZoneToPDCubeLookup == null)
                {
                    FlatZoneToPDCubeLookup = zones.Select(zone => pds.GetFlatIndex(zone.PlanningDistrict)).ToArray();
                }
                // now that we are done we can calculate our utilities
                CalculateUtilities();
            }

            private static float[][] CreateSquare(int length)
            {
                var ret = new float[length][];
                for(int i = 0; i < ret.Length; i++)
                {
                    ret[i] = new float[length];
                }
                return ret;
            }

            private void BuildPDCube(SparseArray<float> pds)
            {
                var numberOfPds = pds.Count;
                var pdIndex = pds.ValidIndexArray();
                PDCube = new int[numberOfPds][][];
                for(int i = 0; i < PDCube.Length; i++)
                {
                    PDCube[i] = new int[numberOfPds][];
                    for(int j = 0; j < PDCube[i].Length; j++)
                    {
                        PDCube[i][j] = new int[numberOfPds];
                        for(int k = 0; k < PDCube[i][j].Length; k++)
                        {
                            PDCube[i][j][k] = GetODIndex(pdIndex[i], pdIndex[k], pdIndex[j]);
                        }
                    }
                }
            }

            protected abstract void CalculateUtilities();

            internal IZone GetLocation(IZone previousZone, IEpisode ep, IZone nextZone, Time startTime, Time availableTime, float[] calculationSpace, Random random)
            {
                var p = zoneSystem.GetFlatIndex(previousZone.ZoneNumber);
                var n = zoneSystem.GetFlatIndex(nextZone.ZoneNumber);
                var size = zones.Length;
                int index = GetTimePeriod(startTime);
                var rowTimes = Parent.TimePeriods[index].RowTravelTimes;
                var columnTimes = Parent.TimePeriods[index].ColumnTravelTimes;
                var from = From[index];
                var available = availableTime.ToMinutes();
                var to = To[index];
                var pIndex = FlatZoneToPDCubeLookup[p];
                var nIndex = FlatZoneToPDCubeLookup[n];
                var data = PDCube[pIndex][nIndex];
                int previousIndexOffset = p * size;
                int nextSizeOffset = n * size;
                float total = 0.0f;
                unsafe
                {
                    fixed (float* pRowTimes = &rowTimes[0])
                    fixed (float* pColumnTimes = &columnTimes[0])
                    fixed (float* pTo = &to[0])
                    fixed (float* pFrom = &from[0])
                    fixed (int* pData = &data[0])
                    {
                        if(nIndex == pIndex)
                        {
                            for(int i = 0; i < calculationSpace.Length; i++)
                            {
                                if(pRowTimes[previousIndexOffset + i] + pColumnTimes[nextSizeOffset + i] <= available)
                                {
                                    var odUtility = 1.0f;
                                    var pdindex = pData[FlatZoneToPDCubeLookup[i]];
                                    if(pdindex >= 0)
                                    {
                                        odUtility = (pIndex == FlatZoneToPDCubeLookup[i]) ? ODConstants[pdindex].ExpConstant * expSamePD : ODConstants[pdindex].ExpConstant;
                                    }
                                    else
                                    {
                                        odUtility = (pIndex == FlatZoneToPDCubeLookup[i]) ? expSamePD : 1.0f;
                                    }
                                    total += calculationSpace[i] = pTo[previousIndexOffset + i] * pFrom[nextSizeOffset + i] * odUtility;
                                }
                                else
                                {
                                    calculationSpace[i] = 0;
                                }
                            }
                        }
                        else
                        {
                            for(int i = 0; i < calculationSpace.Length; i++)
                            {
                                if(pRowTimes[previousIndexOffset + i] + pColumnTimes[nextSizeOffset + i] <= available)
                                {
                                    var odUtility = 1.0f;
                                    var pdindex = pData[FlatZoneToPDCubeLookup[i]];
                                    if(pdindex >= 0)
                                    {
                                        odUtility = ODConstants[pdindex].ExpConstant;
                                    }
                                    total += calculationSpace[i] = pTo[previousIndexOffset + i] * pFrom[nextSizeOffset + i] * odUtility;
                                }
                                else
                                {
                                    calculationSpace[i] = 0;
                                }
                            }
                        }
                    }
                }
                if(total <= 0)
                {
                    return null;
                }
                var pop = (float)random.NextDouble() * total;
                float current = 0.0f;
                for(int i = 0; i < calculationSpace.Length; i++)
                {
                    current += calculationSpace[i];
                    if(pop <= current)
                    {
                        return zones[i];
                    }
                }
                for(int i = 0; i < calculationSpace.Length; i++)
                {
                    if(calculationSpace[i] > 0)
                    {
                        return zones[i];
                    }
                }
                return null;
            }

            private int GetODIndex(int pPD, int iPD, int nPD)
            {
                for(int i = 0; i < ODConstants.Length; i++)
                {
                    if(ODConstants[i].Previous.Contains(pPD) && ODConstants[i].Interest.Contains(iPD) && ODConstants[i].Next.Contains(nPD))
                    {
                        return i;
                    }
                }
                return -1;
            }

            private int GetTimePeriod(Time startTime)
            {
                var periods = Parent.TimePeriods;
                int i;
                for(i = 0; i < periods.Length; i++)
                {
                    if(periods[i].StartTime <= startTime & periods[i].EndTime > startTime)
                    {
                        return i;
                    }
                }
                return (i - 1);
            }
        }

        public sealed class MarketLocationChoice : LocationChoiceActivity
        {
            protected override void CalculateUtilities()
            {
                var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
                var pf = Parent.ProfessionalFullTime.AquireResource<SparseArray<float>>().GetFlatData();
                var pp = Parent.ProfessionalPartTime.AquireResource<SparseArray<float>>().GetFlatData();
                var gf = Parent.GeneralFullTime.AquireResource<SparseArray<float>>().GetFlatData();
                var gp = Parent.GeneralPartTime.AquireResource<SparseArray<float>>().GetFlatData();
                var sf = Parent.RetailFullTime.AquireResource<SparseArray<float>>().GetFlatData();
                var sp = Parent.RetailPartTime.AquireResource<SparseArray<float>>().GetFlatData();
                var mf = Parent.ManufacturingFullTime.AquireResource<SparseArray<float>>().GetFlatData();
                var mp = Parent.ManufacturingPartTime.AquireResource<SparseArray<float>>().GetFlatData();
                if(pf.Length != zones.Length)
                {
                    throw new XTMFRuntimeException("The professional full-time employment data is not of the same size as the number of zones!");
                }
                Parallel.For(0, zones.Length, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, (int j) =>
                {
                    var network = Parent.AutoNetwork;
                    var transitNetwork = Parent.TransitNetwork;
                    var times = Parent.TimePeriods;
                    var jPD = zones[j].PlanningDistrict;
                    if(!Parent.ValidDestinationZones.Contains(zones[j].ZoneNumber)) return;
                    var jUtil = (float)(Math.Log(1 + pf[j]) * ProfessionalFullTime
                        + Math.Log(1 + pp[j]) * ProfessionalPartTime
                        + Math.Log(1 + gf[j]) * GeneralFullTime
                        + Math.Log(1 + gp[j]) * GeneralPartTime
                        + Math.Log(1 + sf[j]) * RetailFullTime
                        + Math.Log(1 + sp[j]) * RetailPartTime
                        + Math.Log(1 + mf[j]) * ManufacturingFullTime
                        + Math.Log(1 + mp[j]) * ManufacturingPartTime
                        + Math.Log(1 + zones[j].Population) * Population);

                    for(int i = 0; i < zones.Length; i++)
                    {
                        if(!Parent.ValidDestinationZones.Contains(zones[i].ZoneNumber)) continue;
                        var iPD = zones[i].PlanningDistrict;
                        var nonTimeUtil = jUtil;
                        for(int seg = 0; seg < PDConstant.Length; seg++)
                        {
                            if(PDConstant[seg].Range.Contains(jPD))
                            {
                                nonTimeUtil += PDConstant[seg].Constant;
                                break;
                            }
                        }
                        nonTimeUtil = (float)Math.Exp(nonTimeUtil);
                        for(int time = 0; time < times.Length; time++)
                        {
                            Time timeOfDay = times[time].StartTime;
                            var travelUtility = GetTravelLogsum(network, transitNetwork, i, j, timeOfDay);
                            // compute to
                            To[time][i * zones.Length + j] = nonTimeUtil * travelUtility;
                            // compute from
                            From[time][j * zones.Length + i] = travelUtility;
                        }
                    }
                });
            }
        }

        public sealed class OtherLocationChoice : LocationChoiceActivity
        {
            protected override void CalculateUtilities()
            {
                var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
                var pf = Parent.ProfessionalFullTime.AquireResource<SparseArray<float>>().GetFlatData();
                var pp = Parent.ProfessionalPartTime.AquireResource<SparseArray<float>>().GetFlatData();
                var gf = Parent.GeneralFullTime.AquireResource<SparseArray<float>>().GetFlatData();
                var gp = Parent.GeneralPartTime.AquireResource<SparseArray<float>>().GetFlatData();
                var sf = Parent.RetailFullTime.AquireResource<SparseArray<float>>().GetFlatData();
                var sp = Parent.RetailPartTime.AquireResource<SparseArray<float>>().GetFlatData();
                var mf = Parent.ManufacturingFullTime.AquireResource<SparseArray<float>>().GetFlatData();
                var mp = Parent.ManufacturingPartTime.AquireResource<SparseArray<float>>().GetFlatData();
                Parallel.For(0, zones.Length, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, (int j) =>
                {
                    var network = Parent.AutoNetwork;
                    var transitNetwork = Parent.TransitNetwork;
                    var times = Parent.TimePeriods;
                    var jPD = zones[j].PlanningDistrict;
                    var jUtil = (float)(Math.Log(1 + pf[j]) * ProfessionalFullTime
                        + Math.Log(1 + pp[j]) * ProfessionalPartTime
                        + Math.Log(1 + gf[j]) * GeneralFullTime
                        + Math.Log(1 + gp[j]) * GeneralPartTime
                        + Math.Log(1 + sf[j]) * RetailFullTime
                        + Math.Log(1 + sp[j]) * RetailPartTime
                        + Math.Log(1 + mf[j]) * ManufacturingFullTime
                        + Math.Log(1 + mp[j]) * ManufacturingPartTime
                        + Math.Log(1 + zones[j].Population) * Population);

                    for(int i = 0; i < zones.Length; i++)
                    {
                        var iRegion = zones[i].RegionNumber;
                        var iPD = zones[i].PlanningDistrict;
                        var nonTimeUtil = jUtil;
                        for(int seg = 0; seg < PDConstant.Length; seg++)
                        {
                            if(PDConstant[seg].Range.Contains(jPD))
                            {
                                nonTimeUtil += PDConstant[seg].Constant;
                                break;
                            }
                        }
                        nonTimeUtil = (float)Math.Exp(nonTimeUtil);
                        for(int time = 0; time < times.Length; time++)
                        {
                            Time timeOfDay = times[time].StartTime;
                            var travelUtility = GetTravelLogsum(network, transitNetwork, i, j, timeOfDay);
                            // compute to
                            To[time][i * zones.Length + j] = nonTimeUtil * travelUtility;
                            // compute from
                            From[time][j * zones.Length + i] = travelUtility;
                        }
                    }
                });
            }
        }

        public sealed class WorkBasedBusinessocationChoice : LocationChoiceActivity
        {
            protected override void CalculateUtilities()
            {
                var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
                var pf = Parent.ProfessionalFullTime.AquireResource<SparseArray<float>>().GetFlatData();
                var pp = Parent.ProfessionalPartTime.AquireResource<SparseArray<float>>().GetFlatData();
                var gf = Parent.GeneralFullTime.AquireResource<SparseArray<float>>().GetFlatData();
                var gp = Parent.GeneralPartTime.AquireResource<SparseArray<float>>().GetFlatData();
                var sf = Parent.RetailFullTime.AquireResource<SparseArray<float>>().GetFlatData();
                var sp = Parent.RetailPartTime.AquireResource<SparseArray<float>>().GetFlatData();
                var mf = Parent.ManufacturingFullTime.AquireResource<SparseArray<float>>().GetFlatData();
                var mp = Parent.ManufacturingPartTime.AquireResource<SparseArray<float>>().GetFlatData();
                Parallel.For(0, zones.Length, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, (int j) =>
                {
                    var network = Parent.AutoNetwork;
                    var transitNetwork = Parent.TransitNetwork;
                    var times = Parent.TimePeriods;
                    var jPD = zones[j].PlanningDistrict;
                    var jUtil = (float)(Math.Log(1 + pf[j]) * ProfessionalFullTime
                        + Math.Log(1 + pp[j]) * ProfessionalPartTime
                        + Math.Log(1 + gf[j]) * GeneralFullTime
                        + Math.Log(1 + gp[j]) * GeneralPartTime
                        + Math.Log(1 + sf[j]) * RetailFullTime
                        + Math.Log(1 + sp[j]) * RetailPartTime
                        + Math.Log(1 + mf[j]) * ManufacturingFullTime
                        + Math.Log(1 + mp[j]) * ManufacturingPartTime
                        + Math.Log(1 + zones[j].Population) * Population);

                    for(int i = 0; i < zones.Length; i++)
                    {
                        var iPD = zones[i].PlanningDistrict;
                        var nonTimeUtil = jUtil;
                        for(int seg = 0; seg < PDConstant.Length; seg++)
                        {
                            if(PDConstant[seg].Range.Contains(jPD))
                            {
                                nonTimeUtil += PDConstant[seg].Constant;
                                break;
                            }
                        }
                        nonTimeUtil = (float)Math.Exp(nonTimeUtil);
                        for(int time = 0; time < times.Length; time++)
                        {
                            Time timeOfDay = times[time].StartTime;
                            var travelUtility = GetTravelLogsum(network, transitNetwork, i, j, timeOfDay);
                            // compute to
                            To[time][i * zones.Length + j] = nonTimeUtil * travelUtility;
                            // compute from
                            From[time][j * zones.Length + i] = travelUtility;
                        }
                    }
                });
            }
        }

        [SubModelInformation(Required = true)]
        public MarketLocationChoice MarketModel;

        [SubModelInformation(Required = true)]
        public MarketLocationChoice OtherModel;

        [SubModelInformation(Required = true)]
        public MarketLocationChoice WorkBasedBusinessModel;

        [SubModelInformation(Description = "The different time periods supported")]
        public TimePeriod[] TimePeriods;

        private INetworkData AutoNetwork;
        private ITripComponentData TransitNetwork;

        private static IZone GetZone(IEpisode otherEpisode, IEpisode inserting)
        {
            return otherEpisode == null ? inserting.Owner.Household.HomeZone : otherEpisode.Zone;
        }

        public IZone GetLocationHomeBased(Activity activity, IZone zone, Random random)
        {
            throw new NotImplementedException("This method is no longer supported for V4.0+");
        }

        public IZone GetLocationHomeBased(IEpisode episode, ITashaPerson person, Random random)
        {
            throw new NotImplementedException("This method is no longer supported for V4.0+");
        }

        public IZone GetLocationWorkBased(IZone primaryWorkZone, ITashaPerson person, Random random)
        {
            throw new NotImplementedException("This method is no longer supported for V4.0+");
        }

        public void LoadLocationChoiceCache()
        {
            Console.WriteLine("Loading Location Choice...");
            for(int i = 0; i < TimePeriods.Length; i++)
            {
                TimePeriods[i].Load();
            }
            Console.WriteLine("Loading Market...");
            MarketModel.Load();
            Console.WriteLine("Loading Other...");
            OtherModel.Load();
            Console.WriteLine("Loading Work Based Business...");
            WorkBasedBusinessModel.Load();
            Console.WriteLine("Finished Loading Location Choice");
        }

        public bool RuntimeValidation(ref string error)
        {
            if(!ProfessionalFullTime.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the sub module Professional Full Time was not of type SparseArray<float>!";
                return false;
            }
            if(!ProfessionalPartTime.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the sub module Professional Part Time was not of type SparseArray<float>!";
                return false;
            }
            if(!ManufacturingFullTime.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the sub module Manufacturing Full Time was not of type SparseArray<float>!";
                return false;
            }
            if(!ManufacturingPartTime.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the sub module Manufacturing Part Time was not of type SparseArray<float>!";
                return false;
            }
            if(!GeneralFullTime.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the sub module General Full Time was not of type SparseArray<float>!";
                return false;
            }
            if(!GeneralPartTime.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the sub module General Part Time was not of type SparseArray<float>!";
                return false;
            }
            if(!RetailFullTime.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the sub module Retail Full Time was not of type SparseArray<float>!";
                return false;
            }
            if(!RetailPartTime.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the sub module Retail Part Time was not of type SparseArray<float>!";
                return false;
            }
            foreach(var network in Root.NetworkData)
            {
                if(network.NetworkType == AutoNetworkName)
                {
                    AutoNetwork = network;
                    break;
                }
            }
            if(AutoNetwork == null)
            {
                error = "In '" + Name + "' we were unable to find a network called '" + AutoNetworkName + "'";
            }

            foreach(var network in Root.NetworkData)
            {
                if(network.NetworkType == TransitNetworkName)
                {
                    TransitNetwork = network as ITripComponentData;
                    break;
                }
            }
            if(TransitNetwork == null)
            {
                error = "In '" + Name + "' we were unable to find a network called '" + AutoNetworkName + "'";
            }
            return true;
        }
    }

}
