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

            public SparseTwinIndex<float> TravelTimes;

            public void Load()
            {
                var times = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
                var network = Parent.AutoNetwork;
                var data = times.GetFlatData();
                Parallel.For(0, data.Length, (int i) =>
                {
                    var row = data[i];
                    var time = StartTime;
                    for(int j = 0; j < row.Length; j++)
                    {
                        row[j] = network.TravelTime(i, j, time).ToMinutes();
                    }
                });
                TravelTimes = times;
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

            protected SparseTwinIndex<float>[] To;
            protected SparseTwinIndex<float>[] From;

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
            [RunParameter("Transit TravelTime", "0.0", typeof(float), "The weight applied for the travel time from origin to zone to final destination.")]
            public float TransitTime;
            [RunParameter("Transit Cost", "0.0", typeof(float), "The weight applied for the transit cost from origin to zone to final destination.")]
            public float TransitCost;
            [RunParameter("Same PD", 0.0f, "The constant applied if the zone of interest is the same as both the previous and next planning districts.")]
            public float SamePD;

            public SpatialRegion[] PDConstant;
            public ODConstant[] ODConstants;
            private float expSamePD;
            private SparseTriIndex<int> PDCube;

            private double GetTransitUtility(ITripComponentData network, int i, int j, Time time)
            {
                float ivtt = 0.0f, walk = 0.0f, wait = 0.0f, cost = 0.0f, boarding = 0.0f;
                if(!network.GetAllData(i, j, time, out ivtt, out walk, out wait, out boarding, out cost))
                {
                    return 0f;
                }
                return Math.Exp(TransitTime * (ivtt + walk + wait)
                    + TransitCost * cost);
            }

            protected float GetTravelLogsum(INetworkData autoNetwork, ITripComponentData transitNetwork, int i, int j, Time time)
            {
                return (float)(GetTransitUtility(transitNetwork, i, j, time)
                    + Math.Exp(autoNetwork.TravelTime(i,j, time).ToMinutes() * AutoTime));
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
            int[] FlatZoneToPDLookup;

            internal void Load()
            {
                var timePeriods = Parent.TimePeriods;
                zoneSystem = Root.ZoneSystem.ZoneArray;
                zones = zoneSystem.GetFlatData();
                if(To == null)
                {
                    To = new SparseTwinIndex<float>[timePeriods.Length];
                    From = new SparseTwinIndex<float>[timePeriods.Length];
                    for(int i = 0; i < timePeriods.Length; i++)
                    {
                        To[i] = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
                        From[i] = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
                    }
                }
                expSamePD = (float)Math.Exp(SamePD);
                // raise the constants to e^constant to save CPU time during the main phase
                for(int i = 0; i < ODConstants.Length; i++)
                {
                    ODConstants[i].ExpConstant = (float)Math.Exp(ODConstants[i].Constant);
                }
                BuildPDCube();
                FlatZoneToPDLookup = zones.Select(zone => PDCube.GetFlatIndex(zone.PlanningDistrict)).ToArray();
                // now that we are done we can calculate our utilities
                CalculateUtilities();
            }

            private void BuildPDCube()
            {
                var pds = TMG.Functions.ZoneSystemHelper.CreatePDArray<float>(Root.ZoneSystem.ZoneArray);
                var pdIndex = pds.ValidIndexArray();
                PDCube = SparseTriIndex<int>.CreateSimilarArray(pds, pds, pds);
                var data = PDCube.GetFlatData();
                for(int i = 0; i < data.Length; i++)
                {
                    for(int j = 0; j < data[i].Length; j++)
                    {
                        for(int k = 0; k < data[i][j].Length; k++)
                        {
                            data[i][j][k] = GetODIndex(pdIndex[i], pdIndex[j], pdIndex[k]);
                        }
                    }
                }
            }

            protected abstract void CalculateUtilities();

            internal IZone GetLocation(IZone previousZone, IEpisode ep, IZone nextZone, Time startTime, Time availableTime, float[] calculationSpace, Random random)
            {
                var p = zoneSystem.GetFlatIndex(previousZone.ZoneNumber);
                var n = zoneSystem.GetFlatIndex(nextZone.ZoneNumber);
                int index = GetTimePeriod(startTime);
                var times = Parent.TimePeriods[index].TravelTimes.GetFlatData();
                var from = From[index].GetFlatData();
                var available = availableTime.ToMinutes();
                var timeRow = times[p];
                var toRow = To[index].GetFlatData()[p];
                var pIndex = FlatZoneToPDLookup[p];
                var nIndex = FlatZoneToPDLookup[n];
                var data = PDCube.GetFlatData()[pIndex];
                for(int i = 0; i < timeRow.Length; i++)
                {
                    if(timeRow[i] + times[i][n] <= available)
                    {
                        calculationSpace[i] = toRow[i] * from[i][n] * GetODUtility(data, pIndex, FlatZoneToPDLookup[i], nIndex);
                    }
                    else
                    {
                        calculationSpace[i] = 0;
                    }
                }
                float total = 0.0f;
                for(int i = 0; i < calculationSpace.Length; i++)
                {
                    total += calculationSpace[i];
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

            private float GetODUtility(int[][] indexMap, int flatPPD, int flatIPD, int flatNPD)
            {
                var index = indexMap[flatIPD][flatNPD];
                if(index >= 0)
                {
                    return (flatPPD == flatIPD & flatNPD == flatPPD) ? ODConstants[index].ExpConstant * expSamePD : ODConstants[index].ExpConstant;
                }
                return (flatPPD == flatIPD & flatNPD == flatPPD) ? expSamePD : 1.0f;
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
                var to = To.Select(d => d.GetFlatData()).ToArray();
                var from = From.Select(d => d.GetFlatData()).ToArray();
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
                            to[time][i][j] = nonTimeUtil * travelUtility;
                            // compute from
                            from[time][i][j] = travelUtility;
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
                var to = To.Select(d => d.GetFlatData()).ToArray();
                var from = From.Select(d => d.GetFlatData()).ToArray();
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
                            to[time][i][j] = nonTimeUtil * travelUtility;
                            // compute from
                            from[time][i][j] = travelUtility;
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
                var to = To.Select(d => d.GetFlatData()).ToArray();
                var from = From.Select(d => d.GetFlatData()).ToArray();
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
                            to[time][i][j] = nonTimeUtil * travelUtility;
                            // compute from
                            from[time][i][j] = travelUtility;
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
