using Datastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Tasha.DataExtraction;
using Tasha.EMME;
using Tasha.Modes;
using TMG;
using TMG.Input;
using XTMF;

namespace Tasha.Airport
{
    public class V4AirportModel2020 : ISelfContainedModule
    {
        [RootModule]
        public ITravelDemandModel Root;

        [SubModelInformation(Description = "The different demand segments to use.")]
        public DemandSegments[] TimePeriods;

        [RunParameter("Auto Network Name", "Auto", "The name of the network data to use for the auto utilities.")]
        public string AutoNetworkName;

        private INetworkCompleteData _autoNetwork;

        [RunParameter("Transit Network Name", "Transit", "The name of the network data to use for the transit utilities.")]
        public string TransitNetworkName;

        private ITripComponentCompleteData _transitNetwork;

        [RunParameter("Valid Zones", "1-5999", typeof(RangeSet), "Zones that are included in this model.")]
        public RangeSet ValidZones;

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

        public bool RuntimeValidation(ref string error)
        {
            foreach (var network in Root.NetworkData)
            {
                if (network.NetworkType == AutoNetworkName)
                {
                    _autoNetwork = network as INetworkCompleteData;
                }
                else if (network.NetworkType == TransitNetworkName)
                {
                    _transitNetwork = network as ITripComponentCompleteData;
                }
            }
            if (_autoNetwork == null)
            {
                error = "In '" + Name + "' we were unable to find an auto network called '" + AutoNetworkName + "'";
                return false;
            }
            if (_transitNetwork == null)
            {
                error = "In '" + Name + "' we were unable to find a transit network called '" + TransitNetworkName + "'";
                return false;
            }
            return true;
        }

        public class DemandSegments : IModule
        {
            [RunParameter("StartTime", "6:00", "The start time of this time period, inclusive.", Index = 0)]
            public Time StartTime;

            [RunParameter("EndTime", "9:00", "The end time of this time period, exclusive.", Index = 1)]
            public Time EndTime;

            [RunParameter("ASCOther", 0.0f, "Modal constant for the Other Mode")]
            public float ASCOther;
            [RunParameter("ASCPassengerOutOfParty", 0.0f, "Modal constant for the PassengerOutOfParty Mode")]
            public float ASCPassengerOutOfParty;
            [RunParameter("ASCTransit", 0.0f, "Modal constant for the Transit Mode")]
            public float ASCTransit;
            [RunParameter("ASCRideshare", 0.0f, "Modal constant for the Rideshare Mode")]
            public float ASCRideshare;

            [RunParameter("BAutoUtil", 0.0f, "The factor applied to the travel utility from the road network for the auto mode.")]
            public float BAutoUtil;
            [RunParameter("BAutoUtil_Rideshare", 0.0f, "The factor applied to the travel utility from the road network for the rideshare mode.")]
            public float BAutoUtilRideshare;
            [RunParameter("BTransitUtil", 0.0f, "The factor applied to the travel utility from the transit network for the transit mode.")]
            public float BTransitUtil;
            [RunParameter("BDistance", 0.0f, "The factor applied to the distance (metres) from the road network for the other mode.")]
            public float BDistance;

            [RunParameter("AutoValueOfTime", 50.99, "The value of time to use for the auto mode.  This should match the road assignment.")]
            public float AutoVoT;

            [RunParameter("BLogPopulation", 0.0f, "The factor applied to the log of the population for the attraction zone")]
            public float BLogPopulation;

            [RunParameter("BProfessionalEmployment", 0.0f, "The factor applied to the log of employment multiplied by the professional employment rate for the attraction zone")]
            public float BProfessionalEmployment;

            [RunParameter("BGeneralEmployment", 0.0f, "The factor applied to the log of employment multiplied by the general employment rate for the attraction zone")]
            public float BGeneralEmployment;

            [RunParameter("BSalesEmployment", 0.0f, "The factor applied to the log of employment multiplied by the sales employment rate for the attraction zone")]
            public float BSalesEmployment;

            [RunParameter("BManufacturingEmployment", 0.0f, "The factor applied to the log of employment multiplied by the manufacturing employment rate for the attraction zone")]
            public float BManufactruingEmployment;

            [RunParameter("BPD1", 0.0f, "An alternative constant to apply to attractiveness for zones in the CBD.")]
            public float BPD1;

            [RunParameter("BLogsum", 0.0f, "The scaling factor applied to the mode choice logsum.")]
            public float BLogsum;

            [RunParameter("OriginatingPassengers", 0.0f, "The number of passengers of this type that are starting their tours.")]
            public float OriginatingPassengers;

            [RunParameter("TerminatingPassengers", 0.0f, "The number of passengers of this type that are ending their tours.")]
            public float TerminatingPassengers;

            [SubModelInformation(Required = true, Description = "The location to save the auto demand for this segment.")]
            public FileLocation AutoDemand;

            [SubModelInformation(Required = true, Description = "The location to save the transit demand for this segment.")]
            public FileLocation TransitDemand;

            [RootModule]
            public ITravelDemandModel Root;

            public string Name { get; set; }

            public float Progress => 0f;

            public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

            /// <summary>
            /// Generates the demand for the given segment.
            /// </summary>
            /// <param name="validZones"></param>
            /// <param name="autoNetwork"></param>
            /// <param name="transitNetwork"></param>
            /// <param name="distance"></param>
            /// <param name="attraction">[Occupation (0-3), population (4), pd1 (5)][(Log(emp)*Rate) per zone]</param>
            internal void GenerateDemand(bool[] validZones, INetworkCompleteData autoNetwork,
                ITripComponentCompleteData transitNetwork, float[][] distance,
                float[][] attraction, int airportIndex)
            {
                var autoNetworkData = autoNetwork.GetTimePeriodData(StartTime);
                var transitNetworkData = transitNetwork.GetTimePeriodData(StartTime);
                var distribution = new float[validZones.Length];
                var auto = new float[validZones.Length];
                var publicTransit = new float[validZones.Length];
                var passengerOutOfParty = new float[validZones.Length];
                var rideshare = new float[validZones.Length];
                var other = new float[validZones.Length];
                // Mode Choice
                ModeChoice(validZones, distance, airportIndex, autoNetworkData, transitNetworkData, distribution, auto, publicTransit, passengerOutOfParty, rideshare, other);
                float sum = ComputeDistribution(attraction, distribution);
                CreateDemandMatrices(distribution, auto, publicTransit, passengerOutOfParty, rideshare, other, sum, airportIndex);
            }

            private void ModeChoice(bool[] validZones, float[][] distance, int airportIndex, float[] autoNetworkData, float[] transitNetworkData, float[] distribution, float[] auto, float[] publicTransit, float[] passengerOutOfParty, float[] rideshare, float[] other)
            {
                for (int i = 0; i < validZones.Length; i++)
                {
                    if (validZones[i])
                    {
                        var zoneOffset = (i * validZones.Length) + airportIndex;
                        // +1 is cost, +0 is aivtt
                        var autil = (AutoVoT * autoNetworkData[(zoneOffset * 2) + 1]) + autoNetworkData[zoneOffset * 2];
                        // +4 is boarding
                        var tutil = transitNetworkData[(zoneOffset * 5) + 4];
                        auto[i] = (float)Math.Exp(BAutoUtil * autil);
                        passengerOutOfParty[i] = (float)Math.Exp(ASCPassengerOutOfParty + (BAutoUtil * autil));
                        publicTransit[i] = (float)Math.Exp(ASCTransit + (BTransitUtil * tutil));
                        rideshare[i] = (float)Math.Exp(ASCRideshare + (BAutoUtilRideshare * autil));
                        other[i] = (float)Math.Exp(ASCOther + (BDistance * distance[i][airportIndex]));
                        distribution[i] = auto[i] + passengerOutOfParty[i] + publicTransit[i] + rideshare[i] + other[i];
                    }
                }
            }

            private float ComputeDistribution(float[][] attraction, float[] distribution)
            {
                // Distribution
                var sum = 0.0f;
                var professional = attraction[0];
                var general = attraction[1];
                var sales = attraction[2];
                var manufacturing = attraction[3];
                var population = attraction[4];
                var pd1 = attraction[5];
                for (int i = 0; i < distribution.Length; i++)
                {
                    var local = (BProfessionalEmployment * professional[i])
                        + (BGeneralEmployment * general[i])
                        + (BSalesEmployment * sales[i])
                        + (BManufactruingEmployment * manufacturing[i])
                        + (BLogPopulation * population[i])
                        + (BPD1 * pd1[i]);
                    sum += (distribution[i] = (float)(Math.Exp(local) * Math.Pow(distribution[i], BLogsum)));
                }

                return sum;
            }

            private void CreateDemandMatrices(float[] distribution, float[] auto, float[] publicTransit, float[] passengerOutOfParty,
                float[] rideshare, float[] other, float sum, int airportIndex)
            {
                var zones = Root.ZoneSystem.ZoneArray;
                var autoDemandMatrix = CreateMatrix(distribution);
                var transitDemandMatrix = CreateMatrix(distribution);
                for (int i = 0; i < distribution.Length; i++)
                {
                    var denominator = auto[i] + passengerOutOfParty[i] + publicTransit[i] + rideshare[i] + other[i];
                    if (denominator > 0)
                    {
                        /*
                         * Auto Goes there or back. PassOOP Goes there and back again.
                         * Rideshare Goes there or back.
                         * Other is ignored.
                         */
                        var distributionRate = (distribution[i] / sum);
                        var direct = (auto[i] + (rideshare[i] / denominator)) * distributionRate;
                        var bothWays = (TerminatingPassengers + OriginatingPassengers)
                            * (passengerOutOfParty[i] / denominator)
                            * distributionRate;
                        autoDemandMatrix[i][airportIndex] = (direct * OriginatingPassengers) + bothWays;
                        autoDemandMatrix[airportIndex][i] = (direct * TerminatingPassengers) + bothWays;
                        transitDemandMatrix[i][airportIndex] = OriginatingPassengers * distributionRate * (publicTransit[i] / denominator);
                        transitDemandMatrix[airportIndex][i] = TerminatingPassengers * distributionRate * (publicTransit[i] / denominator);
                    }
                }
                SaveMatrix(autoDemandMatrix, zones, AutoDemand);
                SaveMatrix(transitDemandMatrix, zones, TransitDemand);
            }

            private void SaveMatrix(float[][] demandMatrix, SparseArray<IZone> zones, string fileName)
            {
                try
                {
                    new TMG.Emme.EmmeMatrix(zones, demandMatrix)
                        .Save(fileName, false);
                }
                catch (IOException e)
                {
                    throw new XTMFRuntimeException(this, e, "Error when trying to save demand matrix: " + e.Message);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SaveMatrix(float[][] demandMatrix, SparseArray<IZone> zones, FileLocation autoDemand)
            {
                SaveMatrix(demandMatrix, zones, autoDemand.GetFilePath());
            }

            private static float[][] CreateMatrix(float[] vector)
            {
                var ret = new float[vector.Length][];
                for (int i = 0; i < ret.Length; i++)
                {
                    ret[i] = new float[ret.Length];
                }
                return ret;
            }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        [SubModelInformation(Required = true, Description = "Employment for the given class.")]
        public IDataSource<SparseArray<float>> PFEmployment;
        [SubModelInformation(Required = true, Description = "Employment for the given class.")]
        public IDataSource<SparseArray<float>> PPEmployment;
        [SubModelInformation(Required = true, Description = "Employment for the given class.")]
        public IDataSource<SparseArray<float>> GFEmployment;
        [SubModelInformation(Required = true, Description = "Employment for the given class.")]
        public IDataSource<SparseArray<float>> GPEmployment;
        [SubModelInformation(Required = true, Description = "Employment for the given class.")]
        public IDataSource<SparseArray<float>> SFEmployment;
        [SubModelInformation(Required = true, Description = "Employment for the given class.")]
        public IDataSource<SparseArray<float>> SPEmployment;
        [SubModelInformation(Required = true, Description = "Employment for the given class.")]
        public IDataSource<SparseArray<float>> MFEmployment;
        [SubModelInformation(Required = true, Description = "Employment for the given class.")]
        public IDataSource<SparseArray<float>> MPEmployment;
        [SubModelInformation(Required = true, Description = "Population by zone.")]
        public IDataSource<SparseArray<float>> Population;

        [RunParameter("CBD Range", "1", typeof(RangeSet), "The range of planning districts to consider a CBD.")]
        public RangeSet CBDRange;

        [RunParameter("Airport Zone", 0, "The zone number of the airport.")]
        public int AirportZone;

        public void Start()
        {
            bool[] availableZones = Root.ZoneSystem.ZoneArray.GetFlatData()
                .Select(z => ValidZones.Contains(z.ZoneNumber)).ToArray();
            float[][] attractionTerms = new float[][]
                {
                    SumResources(PFEmployment, PPEmployment),
                    SumResources(GFEmployment, GPEmployment),
                    SumResources(SFEmployment, SPEmployment),
                    SumResources(MFEmployment, MPEmployment),
                    GetCopyOfResource(Population),
                    Root.ZoneSystem.ZoneArray.GetFlatData().Select(z => CBDRange.Contains(z.PlanningDistrict) ? 1.0f : 0.0f).ToArray()
                };
            ProcessAttractionTerms(attractionTerms);
            int pearsonZoneIndex = Root.ZoneSystem.ZoneArray.GetFlatIndex(AirportZone);
            if(pearsonZoneIndex < 0)
            {
                throw new XTMFRuntimeException(this, $"The airport zone number {AirportZone} was not found in the zone system!");
            }
            Parallel.ForEach(TimePeriods, (timePeriod) =>
            {
                timePeriod.GenerateDemand(availableZones, _autoNetwork, _transitNetwork, Root.ZoneSystem.Distances.GetFlatData(),
                    attractionTerms, pearsonZoneIndex);
            });
        }

        private static void ProcessAttractionTerms(float[][] attractionTerms)
        {
            var p = attractionTerms[0];
            var g = attractionTerms[1];
            var s = attractionTerms[2];
            var m = attractionTerms[3];
            var pop = attractionTerms[4];
            for (int i = 0; i < p.Length; i++)
            {
                var total = p[i] + g[i] + s[i] + m[i];
                if(total > 0)
                {
                    var logTotal = (float)Math.Log(total);
                    p[i] = logTotal * (p[i] / total);
                    g[i] = logTotal * (g[i] / total);
                    s[i] = logTotal * (s[i] / total);
                    m[i] = logTotal * (m[i] / total);
                }
                pop[i] = (float)Math.Log(pop[i]);
            }
        }

        private static float[] GetResource(IDataSource<SparseArray<float>> resource)
        {
            if(!resource.Loaded)
            {
                resource.LoadData();
            }
            return resource.GiveData().GetFlatData();
        }

        private static float[] GetCopyOfResource(IDataSource<SparseArray<float>> resource)
        {
            var original = GetResource(resource);
            var ret = new float[original.Length];
            Buffer.BlockCopy(original, 0, ret, 0, original.Length * sizeof(float));
            return ret;
        }

        private static float[] SumResources(IDataSource<SparseArray<float>> first, IDataSource<SparseArray<float>> second)
        {
            var f = GetResource(first);
            var s = GetResource(second);
            var ret = new float[f.Length];
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = f[i] + s[i];
            }
            return ret;
        }
    }
}
