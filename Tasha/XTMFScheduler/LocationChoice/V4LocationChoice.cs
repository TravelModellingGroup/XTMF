/*
    Copyright 2014-2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.Threading.Tasks;
using Datastructure;
using Tasha.Common;
using TMG;
using XTMF;
using TMG.Functions;
using System.Numerics;
using System.Collections.Concurrent;
using Tasha.EMME;

namespace Tasha.XTMFScheduler.LocationChoice;

public sealed class V4LocationChoice : ILocationChoiceModel
{
    [RunParameter("Valid Destination Zones", "1-6999", typeof(RangeSet), "The valid zones to use.")]
    public RangeSet ValidDestinationZones;

    private bool[] ValidDestinations;

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

    [RunParameter("Estimation Mode", false, "Enable this to improve performance when estimating a model.")]
    public bool EstimationMode;

    public IZone GetLocation(IEpisode ep, Random random)
    {
        var episodes = ep.ContainingSchedule.Episodes;
        var startTime = ep.StartTime;
        int i = 0;
        for (; i < episodes.Length; i++)
        {
            if (episodes[i] == null) break;
            if (startTime < episodes[i].StartTime)
            {
                return GetLocation(ep, random, (i == 0 ? null : episodes[i - 1]), episodes[i], startTime);
            }
        }
        return GetLocation(ep, random, (i > 0 ? episodes[i - 1] : null), null, startTime);
    }

    public float[] GetLocationProbabilities(IEpisode ep)
    {
        var episodes = ep.ContainingSchedule.Episodes;
        var startTime = ep.StartTime;
        int i = 0;
        for (; i < episodes.Length; i++)
        {
            if (episodes[i] == null) break;
            if (startTime < episodes[i].StartTime)
            {
                return GetLocationProbabilities(ep, (i == 0 ? null : episodes[i - 1]), episodes[i], startTime);
            }
        }
        return GetLocationProbabilities(ep, (i > 0 ? episodes[i - 1] : null), null, startTime);
    }

    private ConcurrentQueue<float[]> CalculationPool;
    private readonly int Cores = Environment.ProcessorCount;

    private IZone GetLocation(IEpisode ep, Random random, IEpisode previous, IEpisode next, Time startTime)
    {
        var previousZone = GetZone(previous, ep);
        var nextZone = GetZone(next, ep);
        if (!CalculationPool.TryDequeue(out float[] calculationSpace))
        {
            calculationSpace = new float[Root.ZoneSystem.ZoneArray.Count];
        }
        Time availableTime = ComputeAvailableTime(previous, next);
        var result = ep.Zone;
        switch (ep.ActivityType)
        {
            case Activity.Market:
            case Activity.JointMarket:
                result = MarketModel.GetLocation(previousZone, ep, nextZone, startTime, availableTime, calculationSpace, random);
                break;
            case Activity.JointOther:
            case Activity.IndividualOther:
                result = OtherModel.GetLocation(previousZone, ep, nextZone, startTime, availableTime, calculationSpace, random);
                break;
            case Activity.WorkBasedBusiness:
            case Activity.SecondaryWork:
                result = WorkBasedBusinessModel.GetLocation(previousZone, ep, nextZone, startTime, availableTime, calculationSpace, random);
                break;
        }
        CalculationPool.Enqueue(calculationSpace);
        return result;
    }

    private float[] GetLocationProbabilities(IEpisode ep, IEpisode previous, IEpisode next, Time startTime)
    {
        var previousZone = GetZone(previous, ep);
        var nextZone = GetZone(next, ep);
        float[] calculationSpace = new float[Root.ZoneSystem.ZoneArray.Count];
        Time availableTime = ComputeAvailableTime(previous, next);
        switch (ep.ActivityType)
        {
            case Activity.Market:
            case Activity.JointMarket:
                return MarketModel.GetLocationProbabilities(previousZone, ep, nextZone, startTime, availableTime, calculationSpace);
            case Activity.JointOther:
            case Activity.IndividualOther:
                return OtherModel.GetLocationProbabilities(previousZone, ep, nextZone, startTime, availableTime, calculationSpace);
            case Activity.WorkBasedBusiness:
            case Activity.SecondaryWork:
                return WorkBasedBusinessModel.GetLocationProbabilities(previousZone, ep, nextZone, startTime, availableTime, calculationSpace);
        }
        // if it isn't something that we understand just accept its previous zone
        return calculationSpace;
    }

    [RunParameter("Maximum Episode Duration Compression", 0.5f, "The amount that the duration is allowed to be compressed from the original duration time (0 to 1 default is 0.5).")]
    public float MaximumEpisodeDurationCompression;

    private Time ComputeAvailableTime(IEpisode previous, IEpisode next)
    {
        return (next == null ? Time.EndOfDay : (next.StartTime + next.Duration - (MaximumEpisodeDurationCompression * next.OriginalDuration)))
            - (previous == null ? Time.StartOfDay : previous.EndTime - previous.Duration - (MaximumEpisodeDurationCompression * previous.OriginalDuration));
    }

    public sealed class SpatialRegion : IModule
    {
        [RunParameter("PDRange", "1", typeof(RangeSet), "The planning districts that constitute this spatial segment.")]
        public RangeSet Range;

        [RunParameter("Constant", 0.0f, "The constant applied if the spatial category is met.")]
        public float Constant;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

    public sealed class ODConstant : IModule
    {
        [RunParameter("Previous PD Range", "1", typeof(RangeSet), "The planning districts for the previous zone.")]
        public RangeSet Previous;

        [RunParameter("Next PD Range", "1", typeof(RangeSet), "The planning districts for the next zone.")]
        public RangeSet Next;

        [RunParameter("Interest PD Range", "1", typeof(RangeSet), "The planning districts the zone we are interested in.")]
        public RangeSet Interest;

        [RunParameter("Constant", 0.0f, "The constant applied if the spatial category is met.")]
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
        internal float[] EstimationAIVTT;
        internal float[] EstimationACOST;
        internal float[] EstimationTIVTT;
        internal float[] EstimationTWALK;
        internal float[] EstimationTWAIT;
        internal float[] EstimationTBOARDING;
        internal float[] EstimationTFARE;
        internal float[] EstimationDistance;

        internal float[] EstimationTempSpace;
        internal float[] EstimationTempSpace2;
        internal float[] EstimationTempSpace3;

        public void Load()
        {
            var size = Root.ZoneSystem.ZoneArray.Count;
            var autoNetwork = Parent.AutoNetwork;
            var transitNetwork = Parent.TransitNetwork;
            // we only need to load in this data if we are
            if (Parent.EstimationMode)
            {
                if (EstimationAIVTT == null)
                {
                    var odPairs = size * size;
                    EstimationAIVTT = new float[odPairs];
                    EstimationACOST = new float[odPairs];
                    EstimationTIVTT = new float[odPairs];
                    EstimationTWALK = new float[odPairs];
                    EstimationTWAIT = new float[odPairs];
                    EstimationTBOARDING = new float[odPairs];
                    EstimationTFARE = new float[odPairs];
                    EstimationDistance = new float[odPairs];
                    var distances = Root.ZoneSystem.Distances.GetFlatData();
                    Parallel.For(0, size, i =>
                    {
                        var time = StartTime;
                        int baseIndex = i * size;
                        for (int j = 0; j < size; j++)
                        {
                            autoNetwork.GetAllData(i, j, time, out EstimationAIVTT[baseIndex + j], out EstimationACOST[baseIndex + j]);
                            transitNetwork.GetAllData(i, j, time,
                                out EstimationTIVTT[baseIndex + j],
                                out EstimationTWALK[baseIndex + j],
                                out EstimationTWAIT[baseIndex + j],
                                out EstimationTBOARDING[baseIndex + j],
                                out EstimationTFARE[baseIndex + j]
                                );
                            EstimationDistance[baseIndex + j] = distances[i][j];
                        }
                    });
                }
                if (RowTravelTimes != null)
                {
                    return;
                }
            }
            var rowData = (RowTravelTimes == null || RowTravelTimes.Length != size * size) ? new float[size * size] : RowTravelTimes;
            var columnData = (ColumnTravelTimes == null || ColumnTravelTimes.Length != size * size) ? new float[size * size] : ColumnTravelTimes;
            Parallel.For(0, size, i =>
            {
                var time = StartTime;
                int startingIndex = i * size;
                for (int j = 0; j < size; j++)
                {
                    var ijTime = autoNetwork.TravelTime(i, j, time).ToMinutes();
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


        public class TimePeriodParameters : IModule
        {
            [SubModelInformation(Description = "The PD constants for this time period.")]
            public SpatialRegion[] PDConstant;

            [SubModelInformation(Description = "The constants to apply when traveling between given places")]
            public ODConstant[] ODConstants;

            [RunParameter("Same PD", 0.0f, "The constant applied if the zone of interest is the same as both the previous and next planning districts.")]
            public float SamePD;

            [RunParameter("Travel Logsum Scale", 1.0f, "The scale term to apply to the logsum coming from the travel times.")]
            public float TravelLogsumScale;

            [RunParameter("Travel Logsum Denominator", 1.0f, "The scale term to apply to the logsum coming from the travel times.")]
            public float TravelLogsumDenominator;

            [SubModelInformation(Required = false, Description = "Custom utility to apply")]
            public IDataSource<SparseTwinIndex<float>> CustomUtility;

            internal float ExpSamePD;

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        [SubModelInformation(Description = "The parameters for this model by time period. There must be the same number of time periods as in the location choice model.")]
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public TimePeriodParameters[] TimePeriod;

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
        [RunParameter("Transit Constant", "0.0", typeof(float), "The alternative specific constant for transit.")]
        public float TransitConstant;
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
        [RunParameter("Active Constant", float.NegativeInfinity, "The alternative specific constant for active.")]
        public float ActiveConstant;
        [RunParameter("Active Distance", float.NegativeInfinity, "The weight applied for the distance from origin to destination.")]
        public float ActiveDistance;
        [RunParameter("Intra Zonal", 0.0f, "The constant to apply if the trip is within the same zone.")]
        public float IntraZonal
        {
            get
            {
                return _IntraZonal;
            }
            set
            {
                _IntraZonal = value;
                ExpIntraZonal = (float)Math.Exp(value);
            }
        }
        private float _IntraZonal;

        private float ExpIntraZonal;

        [RunParameter("Use Employment Ratios", false, "Instead of taking the log for each occemp, it will take the ratio of the log of the total emp.")]
        public bool UseEmploymentRatios;

        private int[][][][] PDCube;

        private double GetTransitUtility(ITripComponentData network, int i, int j, Time time,
            float scaleFactor)
        {
            if (!network.GetAllData(i, j, time, out float ivtt, out float walk, out float wait, out float boarding, out float cost))
            {
                return 0f;
            }
            return Math.Exp((
                  TransitConstant
                + TransitTime * ivtt
                + TransitWalk * walk
                + TransitWait * wait
                + TransitBoarding * boarding
                + Cost * cost) / scaleFactor);
        }

        protected float GetTravelLogsum(INetworkData autoNetwork, ITripComponentData transitNetwork, float[][] distances, int i, int j, Time time,
            float scaleFactor)
        {
            if (!autoNetwork.GetAllData(i, j, time, out float ivtt, out float cost))
            {
                return 0.0f;
            }
            var active = Math.Exp((ActiveConstant + ActiveDistance * distances[i][j]) / scaleFactor);
            // this is needed for backwards compatibility
            if (double.IsNaN(active) | double.IsInfinity(active))
            {
                active = 0.0;
            }
            var ret = (float)(GetTransitUtility(transitNetwork, i, j, time, scaleFactor)
                + Math.Exp((ivtt * AutoTime + cost * Cost) / scaleFactor)
                + active);
            return ret;
        }

        internal float[] GenerateEstimationLogsums(TimePeriod timePeriod, IZone[] zones, TimePeriodParameters timePeriodParameters)
        {
            var zones2 = zones.Length * zones.Length;
            float[] autoSpace = timePeriod.EstimationTempSpace;
            float[] transitSpace = timePeriod.EstimationTempSpace2;
            float[] activeSpace = timePeriod.EstimationTempSpace3;

            if (autoSpace == null)
            {
                timePeriod.EstimationTempSpace = autoSpace = new float[zones2];
                timePeriod.EstimationTempSpace2 = transitSpace = new float[zones2];
                timePeriod.EstimationTempSpace3 = activeSpace = new float[zones2];
            }
            Parallel.For(0, zones.Length, i =>
            {
                var start = i * zones.Length;
                var end = start + zones.Length;
                Vector<float> vCost = new(Cost);
                Vector<float> vAutoTime = new(AutoTime);
                Vector<float> vTransitConstant = new(TransitConstant);
                Vector<float> vTransitTime = new(TransitTime);
                Vector<float> vTransitWalk = new(TransitWalk);
                Vector<float> vTransitWait = new(TransitWait);
                Vector<float> vTransitBoarding = new(TransitBoarding);
                Vector<float> vActiveConstant = new(ActiveConstant);
                Vector<float> vActiveDistance = new(ActiveDistance);
                Vector<float> vNegativeInfinity = new(float.NegativeInfinity);
                int index = start;
                // copy everything we can do inside of a vector
                for (; index <= end - Vector<float>.Count; index += Vector<float>.Count)
                {
                    // compute auto utility
                    var aivtt = new Vector<float>(timePeriod.EstimationAIVTT, index);
                    var acost = new Vector<float>(timePeriod.EstimationACOST, index);
                    (
                          aivtt * vAutoTime
                        + acost * vCost
                    ).CopyTo(autoSpace, index);
                    // compute transit utility
                    var tivtt = new Vector<float>(timePeriod.EstimationTIVTT, index);
                    var twalk = new Vector<float>(timePeriod.EstimationTWALK, index);
                    var twait = new Vector<float>(timePeriod.EstimationTWAIT, index);
                    var tboarding = new Vector<float>(timePeriod.EstimationTBOARDING, index);
                    var tFare = new Vector<float>(timePeriod.EstimationTFARE, index);
                    Vector.ConditionalSelect(Vector.GreaterThan(twalk, Vector<float>.Zero), (
                         vTransitConstant
                        + tivtt * vTransitTime
                        + twalk * vTransitWalk
                        + twait * vTransitWait
                        + tboarding * vTransitBoarding
                        + tFare * vCost), vNegativeInfinity).CopyTo(transitSpace, index);
                    // compute active utility
                    (vActiveConstant + vActiveDistance * new Vector<float>(timePeriod.EstimationDistance, index)).CopyTo(activeSpace, index);
                }
                // copy the remainder
                for (; index < end; index++)
                {
                    autoSpace[index] =
                          timePeriod.EstimationAIVTT[index] * AutoTime
                        + timePeriod.EstimationACOST[index] * Cost;
                    activeSpace[index] = ActiveConstant + timePeriod.EstimationDistance[index] * ActiveDistance;
                    if (timePeriod.EstimationTWALK[index] > 0)
                    {
                        transitSpace[index] =
                                  TransitConstant
                                + timePeriod.EstimationTIVTT[index] * TransitTime
                                + timePeriod.EstimationTWALK[index] * TransitWalk
                                + timePeriod.EstimationTWAIT[index] * TransitWait
                                + timePeriod.EstimationTBOARDING[index] * TransitBoarding
                                + timePeriod.EstimationTFARE[index] * Cost;
                    }
                    else
                    {
                        transitSpace[index] = float.NegativeInfinity;
                    }
                }
            });
            Parallel.For(0, zones2, index =>
            {
                autoSpace[index] = (float)Math.Pow(Math.Exp(autoSpace[index] / timePeriodParameters.TravelLogsumDenominator) + Math.Exp(transitSpace[index] / timePeriodParameters.TravelLogsumDenominator)
                    + Math.Exp(activeSpace[index] / timePeriodParameters.TravelLogsumDenominator), timePeriodParameters.TravelLogsumScale);
            });
            return autoSpace;
        }

        public bool RuntimeValidation(ref string error)
        {
            var parentTimePeriods = Parent.TimePeriods;
            var ourTimePeriods = TimePeriod;
            if (parentTimePeriods.Length != ourTimePeriods.Length)
            {
                error = "In '" + Name + "' the number of time periods contained in the module is '" + TimePeriod.Length
                    + "', the parent has '" + ourTimePeriods.Length + "'.  These must be the same to continue.";
                return false;
            }
            return true;
        }

        private SparseArray<IZone> ZoneSystem;
        private IZone[] Zones;
        private int[] FlatZoneToPDCubeLookup;

        internal void Load()
        {
            ZoneSystem = Root.ZoneSystem.ZoneArray;
            Zones = ZoneSystem.GetFlatData();
            if (To == null || (To.Length > 0 && To[0].Length != Zones.Length))
            {
                To = new float[TimePeriod.Length][];
                From = new float[TimePeriod.Length][];
                for (int i = 0; i < TimePeriod.Length; i++)
                {
                    To[i] = new float[Zones.Length * Zones.Length];
                    From[i] = new float[Zones.Length * Zones.Length];
                }
            }
            foreach (var timePeriod in TimePeriod)
            {
                timePeriod.ExpSamePD = (float)Math.Exp(timePeriod.SamePD);
            }
            // raise the constants to e^constant to save CPU time during the main phase
            foreach (var timePeriod in TimePeriod)
            {
                for (int i = 0; i < timePeriod.ODConstants.Length; i++)
                {
                    timePeriod.ODConstants[i].ExpConstant = (float)Math.Exp(timePeriod.ODConstants[i].Constant);
                }
            }
            if (!Parent.EstimationMode || PDCube == null)
            {
                var pds = ZoneSystemHelper.CreatePdArray<float>(Root.ZoneSystem.ZoneArray);
                BuildPDCube(pds);
                FlatZoneToPDCubeLookup ??= Zones.Select(zone => pds.GetFlatIndex(zone.PlanningDistrict)).ToArray();
            }
            // now that we are done we can calculate our utilities
            CalculateUtilities();
        }

        private void BuildPDCube(SparseArray<float> pds)
        {
            var numberOfPds = pds.Count;
            var pdIndex = pds.ValidIndexArray();
            PDCube = new int[TimePeriod.Length][][][];
            for (int timePeriod = 0; timePeriod < PDCube.Length; timePeriod++)
            {
                PDCube[timePeriod] = new int[numberOfPds][][];
                for (int i = 0; i < PDCube[timePeriod].Length; i++)
                {
                    PDCube[timePeriod][i] = new int[numberOfPds][];
                    for (int j = 0; j < PDCube[timePeriod][i].Length; j++)
                    {
                        PDCube[timePeriod][i][j] = new int[numberOfPds];
                        for (int k = 0; k < PDCube[timePeriod][i][j].Length; k++)
                        {
                            PDCube[timePeriod][i][j][k] = GetODIndex(timePeriod, pdIndex[i], pdIndex[k], pdIndex[j]);
                        }
                    }
                }
            }
        }

        protected void CalculateUtilities()
        {
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            var pf = Parent.ProfessionalFullTime.AcquireResource<SparseArray<float>>().GetFlatData();
            var pp = Parent.ProfessionalPartTime.AcquireResource<SparseArray<float>>().GetFlatData();
            var gf = Parent.GeneralFullTime.AcquireResource<SparseArray<float>>().GetFlatData();
            var gp = Parent.GeneralPartTime.AcquireResource<SparseArray<float>>().GetFlatData();
            var sf = Parent.RetailFullTime.AcquireResource<SparseArray<float>>().GetFlatData();
            var sp = Parent.RetailPartTime.AcquireResource<SparseArray<float>>().GetFlatData();
            var mf = Parent.ManufacturingFullTime.AcquireResource<SparseArray<float>>().GetFlatData();
            var mp = Parent.ManufacturingPartTime.AcquireResource<SparseArray<float>>().GetFlatData();
            if (pf.Length != zones.Length)
            {
                throw new XTMFRuntimeException(this, "The professional full-time employment data is not of the same size as the number of zones!");
            }
            float[][] jSum = new float[TimePeriod.Length][];
            var expCustomUtilities = new float[TimePeriod.Length][][];
            for (int i = 0; i < TimePeriod.Length; i++)
            {
                jSum[i] = new float[zones.Length];
                var customUtilities = GetData(TimePeriod[i].CustomUtility)?.GetFlatData();
                expCustomUtilities[i] = customUtilities;
                Parallel.For(0, jSum[i].Length, j =>
                {
                    // Start by exponentiating the custom utilities
                    // if there are any for this time period
                    if (customUtilities is not null)
                    {
                        VectorHelper.Exp(customUtilities[j], customUtilities[j]);
                    }
                    var jPD = zones[j].PlanningDistrict;
                    if (Parent.ValidDestinations[j])
                    {
                        var nonExpPDConstant = 0.0f;
                        for (int seg = 0; seg < TimePeriod[i].PDConstant.Length; seg++)
                        {
                            if (TimePeriod[i].PDConstant[seg].Range.Contains(jPD))
                            {
                                nonExpPDConstant += TimePeriod[i].PDConstant[seg].Constant;
                                break;
                            }
                        }
                        double empTerm;
                        if (UseEmploymentRatios)
                        {
                            var totalEmp = ((pf[i] + pp[i]) + (gf[i] + gp[i])) + ((sf[i] + sp[i]) + (mf[i] + mp[i]));
                            var logOfEmp = Math.Log(totalEmp + 1);
                            empTerm = Math.Exp(
                                (ProfessionalFullTime * pf[i] +
                                ProfessionalPartTime * pp[i] +
                                GeneralFullTime * gf[i] +
                                GeneralPartTime * gp[i] +
                                RetailFullTime * sf[i] +
                                RetailPartTime * sp[i] +
                                ManufacturingPartTime * mf[i] +
                                ProfessionalFullTime * mp[i]) * logOfEmp / Math.Max(totalEmp, 1) +
                                Math.Log(1 + zones[j].Population) * Population
                                );

                        }
                        else
                        {
                            empTerm = Math.Exp((Math.Log(1 + pf[j]) * ProfessionalFullTime
                                          + Math.Log(1 + pp[j]) * ProfessionalPartTime
                                          + Math.Log(1 + gf[j]) * GeneralFullTime
                                          + Math.Log(1 + gp[j]) * GeneralPartTime
                                          + Math.Log(1 + sf[j]) * RetailFullTime
                                          + Math.Log(1 + sp[j]) * RetailPartTime
                                          + Math.Log(1 + mf[j]) * ManufacturingFullTime
                                          + Math.Log(1 + mp[j]) * ManufacturingPartTime
                                          + Math.Log(1 + zones[j].Population) * Population));
                        }
                        jSum[i][j] = (float)(empTerm * Math.Exp(nonExpPDConstant));
                    }
                    else
                    {
                        jSum[i][j] = 0.0f;
                    }
                });
            }
            if (Parent.EstimationMode)
            {
                for (int i = 0; i < Parent.TimePeriods.Length; i++)
                {
                    GenerateEstimationLogsums(Parent.TimePeriods[i], zones, TimePeriod[i]);
                }
            }
            var itterRoot = (Root as IIterativeModel);
            int currentIteration = itterRoot != null ? itterRoot.CurrentIteration : 0;
            Parallel.For(0, zones.Length, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
            {
                var network = Parent.AutoNetwork;
                var transitNetwork = Parent.TransitNetwork;
                var times = Parent.TimePeriods;
                var distances = Root.ZoneSystem.Distances.GetFlatData();
                for (int time = 0; time < times.Length; time++)
                {
                    var customUtilities = expCustomUtilities[time];
                    var timeParameters = TimePeriod[time];
                    Time timeOfDay = times[time].StartTime;
                    if (Parent.EstimationMode)
                    {
                        unsafe
                        {
                            fixed (float* to = To[time])
                            fixed (float* from = From[time])
                            fixed (float* logsumSpace = times[time].EstimationTempSpace)
                            {
                                if (customUtilities is null)
                                {
                                    for (int j = 0; j < zones.Length; j++)
                                    {
                                        var nonExpPDConstant = jSum[time][j] * (i == j ? ExpIntraZonal : 1.0f);
                                        var travelUtility = logsumSpace[i * zones.Length + j];
                                        // compute to
                                        to[i * zones.Length + j] = nonExpPDConstant * travelUtility;
                                        // compute from
                                        from[j * zones.Length + i] = travelUtility;
                                    }
                                }
                                else
                                {
                                    for (int j = 0; j < zones.Length; j++)
                                    {
                                        var nonExpPDConstant = jSum[time][j] * (i == j ? ExpIntraZonal : 1.0f);
                                        var travelUtility = logsumSpace[i * zones.Length + j];
                                        // compute to
                                        to[i * zones.Length + j] = nonExpPDConstant * travelUtility * customUtilities[i][j];
                                        // compute from
                                        from[j * zones.Length + i] = travelUtility * customUtilities[j][i];
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // if we are on anything besides the first iteration do a blended assignment for the utility to help converge.
                        if (currentIteration == 0)
                        {
                            if (customUtilities is null)
                            {
                                for (int j = 0; j < zones.Length; j++)
                                {
                                    var nonExpPDConstant = jSum[time][j] * (i == j ? ExpIntraZonal : 1.0f);
                                    var travelUtility = MathF.Pow(GetTravelLogsum(network, transitNetwork, distances, i, j, timeOfDay, timeParameters.TravelLogsumDenominator), timeParameters.TravelLogsumScale);
                                    // compute to
                                    To[time][i * zones.Length + j] = nonExpPDConstant * travelUtility;
                                    // compute from
                                    From[time][j * zones.Length + i] = travelUtility;
                                }
                            }
                            else
                            {
                                for (int j = 0; j < zones.Length; j++)
                                {
                                    var nonExpPDConstant = jSum[time][j] * (i == j ? ExpIntraZonal : 1.0f);
                                    var travelUtility = MathF.Pow(GetTravelLogsum(network, transitNetwork, distances, i, j, timeOfDay, timeParameters.TravelLogsumDenominator), timeParameters.TravelLogsumScale);
                                    // compute to
                                    To[time][i * zones.Length + j] = nonExpPDConstant * travelUtility * customUtilities[i][j];
                                    // compute from
                                    From[time][j * zones.Length + i] = travelUtility * customUtilities[j][i];
                                }
                            }
                        }
                        else
                        {
                            if (customUtilities is null)
                            {
                                for (int j = 0; j < zones.Length; j++)
                                {
                                    var nonExpPDConstant = jSum[time][j] * (i == j ? ExpIntraZonal : 1.0f);
                                    var travelUtility = MathF.Pow(GetTravelLogsum(network, transitNetwork, distances, i, j, timeOfDay, timeParameters.TravelLogsumDenominator), timeParameters.TravelLogsumScale);
                                    // compute to
                                    To[time][i * zones.Length + j] = ((nonExpPDConstant * travelUtility) + To[time][i * zones.Length + j]) * 0.5f;
                                    // compute from
                                    From[time][j * zones.Length + i] = (travelUtility + From[time][j * zones.Length + i]) * 0.5f;
                                }
                            }
                            else
                            {
                                for (int j = 0; j < zones.Length; j++)
                                {
                                    var nonExpPDConstant = jSum[time][j] * (i == j ? ExpIntraZonal : 1.0f);
                                    var travelUtility = MathF.Pow(GetTravelLogsum(network, transitNetwork, distances, i, j, timeOfDay, timeParameters.TravelLogsumDenominator), timeParameters.TravelLogsumScale);
                                    // compute to
                                    To[time][i * zones.Length + j] = ((nonExpPDConstant * travelUtility * customUtilities[i][j]) + To[time][i * zones.Length + j]) * 0.5f;
                                    // compute from
                                    From[time][j * zones.Length + i] = (travelUtility * customUtilities[j][i] + From[time][j * zones.Length + i]) * 0.5f;
                                }
                            }
                        }
                    }
                }
            });
        }

        private static SparseTwinIndex<float> GetData(IDataSource<SparseTwinIndex<float>> customUtility)
        {
            if (customUtility is null)
            {
                return null;
            }
            customUtility.LoadData();
            var ret = customUtility.GiveData();
            customUtility.UnloadData();
            return ret;
        }


        internal float[] GetLocationProbabilities(IZone previousZone, IEpisode ep, IZone nextZone, Time startTime, Time availableTime, float[] calculationSpace)
        {
            var total = CalculateLocationProbabilities(previousZone, nextZone, startTime, availableTime, calculationSpace);
            if (total <= 0.0f)
            {
                return calculationSpace;
            }
            VectorHelper.Divide(calculationSpace, 0, calculationSpace, 0, total, calculationSpace.Length);
            return calculationSpace;
        }

        /// <summary>
        /// </summary>
        /// <param name="previousZone"></param>
        /// <param name="nextZone"></param>
        /// <param name="startTime"></param>
        /// <param name="availableTime"></param>
        /// <param name="calculationSpace"></param>
        /// <returns>The sum of the calculation space</returns>
        private float CalculateLocationProbabilities(IZone previousZone, IZone nextZone, Time startTime, Time availableTime, float[] calculationSpace)
        {
            var p = ZoneSystem.GetFlatIndex(previousZone.ZoneNumber);
            var n = ZoneSystem.GetFlatIndex(nextZone.ZoneNumber);
            var size = Zones.Length;
            int index = GetTimePeriod(startTime);
            var rowTimes = Parent.TimePeriods[index].RowTravelTimes;
            var columnTimes = Parent.TimePeriods[index].ColumnTravelTimes;
            var from = From[index];
            var available = availableTime.ToMinutes();
            var to = To[index];
            var pIndex = FlatZoneToPDCubeLookup[p];
            var nIndex = FlatZoneToPDCubeLookup[n];
            var data = PDCube[index][pIndex][nIndex];
            int previousIndexOffset = p * size;
            int nextIndexOffset = n * size;
            float total = 0.0f;
            if (Vector.IsHardwareAccelerated)
            {
                Vector<float> availableTimeV = new(available);
                Vector<float> totalV = Vector<float>.Zero;
                int i;
                if (nIndex == pIndex)
                {
                    for (i = 0; i < calculationSpace.Length; i++)
                    {
                        float odUtility;
                        var pdindex = data[FlatZoneToPDCubeLookup[i]];
                        if (pdindex >= 0)
                        {
                            odUtility = (pIndex == FlatZoneToPDCubeLookup[i]) ? TimePeriod[index].ODConstants[pdindex].ExpConstant * TimePeriod[index].ExpSamePD
                                : TimePeriod[index].ODConstants[pdindex].ExpConstant;
                        }
                        else
                        {
                            odUtility = (pIndex == FlatZoneToPDCubeLookup[i]) ? TimePeriod[index].ExpSamePD : 1.0f;
                        }
                        calculationSpace[i] = odUtility;
                    }
                }
                else
                {
                    for (i = 0; i < calculationSpace.Length; i++)
                    {
                        var pdindex = data[FlatZoneToPDCubeLookup[i]];
                        calculationSpace[i] = pdindex >= 0 ? TimePeriod[index].ODConstants[pdindex].ExpConstant : 1f;
                    }
                }

                for (i = 0; i <= calculationSpace.Length - Vector<float>.Count; i += Vector<float>.Count)
                {
                    var timeTo = new Vector<float>(rowTimes, previousIndexOffset + i);
                    var timeFrom = new Vector<float>(columnTimes, nextIndexOffset + i);
                    var utilityTo = new Vector<float>(to, previousIndexOffset + i);
                    var utilityFrom = new Vector<float>(from, nextIndexOffset + i);
                    Vector<float> calcV = new(calculationSpace, i);
                    Vector<int> zeroMask = Vector.LessThanOrEqual(timeTo + timeFrom, availableTimeV);
                    calcV = Vector.AsVectorSingle(Vector.BitwiseAnd(Vector.AsVectorInt32(calcV), zeroMask))
                        * utilityTo * utilityFrom;
                    calcV.CopyTo(calculationSpace, i);
                    totalV += calcV;
                }
                float remainderTotal = 0.0f;
                for (; i < calculationSpace.Length; i++)
                {
                    if (rowTimes[previousIndexOffset + i] + columnTimes[nextIndexOffset + i] <= available)
                    {
                        remainderTotal += (calculationSpace[i] = to[previousIndexOffset + i] * from[nextIndexOffset + i] * calculationSpace[i]);
                    }
                    else
                    {
                        calculationSpace[i] = 0;
                    }
                }
                total += remainderTotal + Vector.Sum(totalV);
            }
            else
            {
                unsafe
                {
                    fixed (float* pRowTimes = &rowTimes[0])
                    fixed (float* pColumnTimes = &columnTimes[0])
                    fixed (float* pTo = &to[0])
                    fixed (float* pFrom = &from[0])
                    fixed (int* pData = &data[0])
                    {
                        if (nIndex == pIndex)
                        {
                            for (int i = 0; i < calculationSpace.Length; i++)
                            {
                                if (pRowTimes[previousIndexOffset + i] + pColumnTimes[nextIndexOffset + i] <= available)
                                {
                                    float odUtility;
                                    var pdindex = pData[FlatZoneToPDCubeLookup[i]];
                                    if (pdindex >= 0)
                                    {
                                        odUtility = (pIndex == FlatZoneToPDCubeLookup[i]) ?
                                            TimePeriod[index].ODConstants[pdindex].ExpConstant * TimePeriod[index].ExpSamePD
                                            : TimePeriod[index].ODConstants[pdindex].ExpConstant;
                                    }
                                    else
                                    {
                                        odUtility = (pIndex == FlatZoneToPDCubeLookup[i]) ? TimePeriod[index].ExpSamePD : 1.0f;
                                    }
                                    total += calculationSpace[i] = pTo[previousIndexOffset + i] * pFrom[nextIndexOffset + i] * odUtility;
                                }
                                else
                                {
                                    calculationSpace[i] = 0;
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < calculationSpace.Length; i++)
                            {
                                if (pRowTimes[previousIndexOffset + i] + pColumnTimes[nextIndexOffset + i] <= available)
                                {
                                    var odUtility = 1.0f;
                                    var pdindex = pData[FlatZoneToPDCubeLookup[i]];
                                    if (pdindex >= 0)
                                    {
                                        odUtility = TimePeriod[index].ODConstants[pdindex].ExpConstant;
                                    }
                                    total += calculationSpace[i] = pTo[previousIndexOffset + i] * pFrom[nextIndexOffset + i] * odUtility;
                                }
                                else
                                {
                                    calculationSpace[i] = 0;
                                }
                            }
                        }
                    }
                }
            }
            return total;
        }

        internal IZone GetLocation(IZone previousZone, IEpisode ep, IZone nextZone, Time startTime, Time availableTime, float[] calculationSpace, Random random)
        {
            var total = CalculateLocationProbabilities(previousZone, nextZone, startTime, availableTime, calculationSpace);
            if (total <= 0)
            {
                return null;
            }
            var pop = (float)random.NextDouble() * total;
            float current = 0.0f;
            for (int i = 0; i < calculationSpace.Length; i++)
            {
                current += calculationSpace[i];
                if (pop <= current)
                {
                    return Zones[i];
                }
            }
            for (int i = 0; i < calculationSpace.Length; i++)
            {
                if (calculationSpace[i] > 0)
                {
                    return Zones[i];
                }
            }
            return null;
        }

        private int GetODIndex(int timePeriod, int pPD, int iPD, int nPD)
        {
            for (int i = 0; i < TimePeriod[timePeriod].ODConstants.Length; i++)
            {
                if (TimePeriod[timePeriod].ODConstants[i].Previous.Contains(pPD)
                    && TimePeriod[timePeriod].ODConstants[i].Interest.Contains(iPD)
                    && TimePeriod[timePeriod].ODConstants[i].Next.Contains(nPD))
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
            for (i = 0; i < periods.Length; i++)
            {
                if (periods[i].StartTime <= startTime & periods[i].EndTime > startTime)
                {
                    return i;
                }
            }
            return (i - 1);
        }
    }

    public sealed class MarketLocationChoice : LocationChoiceActivity
    {

    }

    public sealed class OtherLocationChoice : LocationChoiceActivity
    {

    }

    public sealed class WorkBasedBusinessocationChoice : LocationChoiceActivity
    {

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
        CalculationPool = new ConcurrentQueue<float[]>();
        for (int i = 0; i < TimePeriods.Length; i++)
        {
            TimePeriods[i].Load();
        }
        if (!EstimationMode || ValidDestinations == null)
        {
            ValidDestinations = Root.ZoneSystem.ZoneArray.GetFlatData().Select(zone => ValidDestinationZones.Contains(zone.ZoneNumber)).ToArray();
        }
        // We can load all of the location choice models in parallel.
        Parallel.Invoke(
            () => MarketModel.Load(),
            () => OtherModel.Load(),
            () => WorkBasedBusinessModel.Load()
            );
    }

    public bool RuntimeValidation(ref string error)
    {
        if (!ProfessionalFullTime.CheckResourceType<SparseArray<float>>())
        {
            error = "In '" + Name + "' the sub module Professional Full Time was not of type SparseArray<float>!";
            return false;
        }
        if (!ProfessionalPartTime.CheckResourceType<SparseArray<float>>())
        {
            error = "In '" + Name + "' the sub module Professional Part Time was not of type SparseArray<float>!";
            return false;
        }
        if (!ManufacturingFullTime.CheckResourceType<SparseArray<float>>())
        {
            error = "In '" + Name + "' the sub module Manufacturing Full Time was not of type SparseArray<float>!";
            return false;
        }
        if (!ManufacturingPartTime.CheckResourceType<SparseArray<float>>())
        {
            error = "In '" + Name + "' the sub module Manufacturing Part Time was not of type SparseArray<float>!";
            return false;
        }
        if (!GeneralFullTime.CheckResourceType<SparseArray<float>>())
        {
            error = "In '" + Name + "' the sub module General Full Time was not of type SparseArray<float>!";
            return false;
        }
        if (!GeneralPartTime.CheckResourceType<SparseArray<float>>())
        {
            error = "In '" + Name + "' the sub module General Part Time was not of type SparseArray<float>!";
            return false;
        }
        if (!RetailFullTime.CheckResourceType<SparseArray<float>>())
        {
            error = "In '" + Name + "' the sub module Retail Full Time was not of type SparseArray<float>!";
            return false;
        }
        if (!RetailPartTime.CheckResourceType<SparseArray<float>>())
        {
            error = "In '" + Name + "' the sub module Retail Part Time was not of type SparseArray<float>!";
            return false;
        }
        foreach (var network in Root.NetworkData)
        {
            if (network.NetworkType == AutoNetworkName)
            {
                AutoNetwork = network;
                break;
            }
        }
        if (AutoNetwork == null)
        {
            error = "In '" + Name + "' we were unable to find a network called '" + AutoNetworkName + "'";
        }

        foreach (var network in Root.NetworkData)
        {
            if (network.NetworkType == TransitNetworkName)
            {
                TransitNetwork = network as ITripComponentData;
                break;
            }
        }
        if (TransitNetwork == null)
        {
            error = "In '" + Name + "' we were unable to find a network called '" + AutoNetworkName + "'";
        }
        return true;
    }
}
