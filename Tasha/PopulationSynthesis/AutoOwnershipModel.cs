/*
    Copyright 2019 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using Datastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Tasha.Common;
using TMG;
using TMG.Functions;
using XTMF;

namespace Tasha.PopulationSynthesis
{
    [ModuleInformation(Description = "The Auto Ownership model for GTAModel V4.1.0.")]
    public sealed class AutoOwnershipModel : IEstimableCalculation<ITashaHousehold, int>
    {
        [RootModule]
        public ITravelDemandModel Root;

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50,150,50);

        [RunParameter("Number Of Adults", 0.159f, "Applied against the number of persons over the age of 18.")]
        public float NumberOfAdults;
        [RunParameter("Number Of Kids", 0.016f, "Applied against the number of persons under the age of 16.")]
        public float NumberOfKids;
        [RunParameter("Number Of Full-Time Workers", 0.184f, "Applied against the number of persons who are full-time out of house workers.")]
        public float NumberOfFTWorkers;


        [RunParameter("Drivers License 1", 4.820f, "Applied if there is 1 license held in the household.")]
        public float DriverLicense1;
        [RunParameter("Drivers License 2", 6.957f, "Applied if there are 2 licenses held in the household.")]
        public float DriverLicense2;
        [RunParameter("Drivers License 3+", 8.704f, "Applied if there are 3 or more licenses held in the household.")]
        public float DriverLicense3Plus;

        [RunParameter("Income Category 2", 0.470f, "A term to add if the person belongs to a household within the TTS income category 2.")]
        public float Income2;
        [RunParameter("Income Category 3", 0.746f, "A term to add if the person belongs to a household within the TTS income category 3.")]
        public float Income3;
        [RunParameter("Income Category 4", 1.060f, "A term to add if the person belongs to a household within the TTS income category 4.")]
        public float Income4;
        [RunParameter("Income Category 5", 1.374f, "A term to add if the person belongs to a household within the TTS income category 5.")]
        public float Income5;
        [RunParameter("Income Category 6", 1.751f, "A term to add if the person belongs to a household within the TTS income category 6.")]
        public float Income6;

        [RunParameter("Population Density", -49.620f, "Applied against the population density for the home zone. (pop/m^2)")]
        public float PopulationDensityBeta;
        [RunParameter("Job Density", -19.492f, "Applied against the job density for the home zone. (pop/m^2)")]
        public float JobDensityBeta;

        [RunParameter("Avg Distance To Work", 0.104f, "Applied to the average distance to work.")]
        public float AverageDistanceToWork;
        [RunParameter("Avg Perceived Transit Time To Work", 0.005f, "Applied to the perceived transit travel time to work.")]
        public float AveragePerceivedTransitTimeToWork;
        [RunParameter("Avg Auto Time To Work", -0.069f, "Applied to the auto travel time to work.")]
        public float AverageAutoTravelTimeToWork;
        [RunParameter("Apartment", 0.0f, "Applied to the utility if the household is in an apartment dwelling.")]
        public float Apartment;

        [RunParameter("Threshold 1", 5.186f, "")]
        public float Threshold1;
        [RunParameter("Threshold 2", 9.395f, "")]
        public float Threshold2;
        [RunParameter("Threshold 3", 12.638f, "")]
        public float Threshold3;
        [RunParameter("Threshold 4", 14.570f, "")]
        public float Threshold4;

        [RunParameter("Sufficient Licenses", 0.0f, "This will be applied if the number of licenses in the household is equal to the number of people who can possibly have one.")]
        public float SufficientLicenses;

        private Random _random;

        [RunParameter("Random Seed", 4564616, "The fixed seed to start the pseudo-random number generator with.")]
        public int RandomSeed;

        private SparseArray<IZone> _zones;
        private float[] _thresholdOffset1;
        private float[] _thresholdOffset2;
        private float[] _thresholdOffset3;
        private float[] _thresholdOffset4;

        [SubModelInformation(Required = true, Description = "The population density in pop/m^2")]
        public IDataSource<SparseArray<float>> PopulationDensity;

        [SubModelInformation(Required = true, Description = "The job density in pop/m^2")]
        public IDataSource<SparseArray<float>> JobDensity;

        [SubModelInformation(Required = true, Description = "The aggregate number of job linkages.")]
        public IDataSource<SparseTwinIndex<float>> JobLinkages;

        [RunParameter("Auto Network", "Auto", "The auto network to use for travel times.")]
        public string AutoNetwork;

        [RunParameter("Transit Network", "Transit", "The transit network to use for travel times.")]
        public string TransitNetwork;

        private INetworkCompleteData _autoNetwork;
        private ITripComponentCompleteData _transitNetwork;

        [RunParameter("Time To Use", "7:00", typeof(Time), "The time of day to use for computing travel times.")]
        public Time TimeToUse;

        /// <summary>
        /// The pre-computed utility to apply for the household zones
        /// </summary>
        private float[] _preComputedHouseholdZoneUtilitiesGround;
        private float[] _preComputedHouseholdZoneUtilitiesApt;

        public void Load()
        {
            _random = new Random(RandomSeed);
            _zones = Root.ZoneSystem.ZoneArray;
            ComputeHouseholdZoneUtilities();
        }

        private void ComputeHouseholdZoneUtilities()
        {
            ComputeAccessibility();
            LoadThresholdOffsets();
        }

        private void ComputeAccessibility()
        {
            var distances = Root.ZoneSystem.Distances.GetFlatData();
            int numberOfZones = _zones.Count;
            _preComputedHouseholdZoneUtilitiesGround = new float[distances.Length];
            _preComputedHouseholdZoneUtilitiesApt = new float[distances.Length];
            var jobAverageAutoTime = new float[numberOfZones];
            var jobAverageDistance = new float[numberOfZones];
            var jobAverageTransitTime = new float[numberOfZones];
            LoadVector(out var populationDensity, PopulationDensity);
            LoadVector(out var jobDensity, JobDensity);
            LoadMatrix(out var jobLinkages, JobLinkages);
            var autoData = _autoNetwork.GetTimePeriodData(TimeToUse);
            var transitData = _transitNetwork.GetTimePeriodData(TimeToUse);
            for (int i = 0; i < jobLinkages.Length; i++)
            {
                var totalJobs = VectorHelper.Sum(jobLinkages[i], 0, jobLinkages[i].Length);
                // only compute the data if the zone has employment, otherwise keep it zero.
                if (totalJobs > 0)
                {
                    var distanceRow = distances[i];
                    var autoIndex = (numberOfZones * i) * 2;
                    var transitIndex = (numberOfZones * i) * 5;
                    for (int j = 0; j < jobLinkages[i].Length; j++)
                    {
                        var jobRatio = (jobLinkages[i][j] / totalJobs);
                        jobAverageAutoTime[i] += jobRatio * autoData[autoIndex];
                        // using perceived travel time
                        jobAverageTransitTime[i] += jobRatio * transitData[transitIndex + 4];
                        // this variable is in km
                        jobAverageDistance[i] += jobRatio * (distanceRow[j] / 1000f);
                        autoIndex += 2;
                        transitIndex += 5;
                    }
                }
                  
                var v = PopulationDensityBeta * populationDensity[i];
                v += JobDensityBeta * jobDensity[i];
                v += AverageAutoTravelTimeToWork * jobAverageAutoTime[i];
                v += AveragePerceivedTransitTimeToWork * jobAverageTransitTime[i];
                v += AverageDistanceToWork * jobAverageDistance[i];
                _preComputedHouseholdZoneUtilitiesGround[i] = v;
                _preComputedHouseholdZoneUtilitiesApt[i] = v;
            }
        }

        public sealed class PDConstants : IModule
        {
            public string Name { get; set; }

            public float Progress => 0f;

            public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

            [RunParameter("Planning Districts", "0", typeof(RangeSet), "The planning districts to apply this constant to.")]
            public RangeSet PlanningDistricts;

            [RunParameter("Constant", 0.0f, "The constant to apply to the planning districts.")]
            public float Constant;

            [RunParameter("Threshold1 Offset", 0.0f, "The offset for the first threshold to apply to the planning districts.")]
            public float ThresholdOffset1;

            [RunParameter("Threshold2 Offset", 0.0f, "The offset for the second threshold to apply to the planning districts.")]
            public float ThresholdOffset2;

            [RunParameter("Threshold3 Offset", 0.0f, "The offset for the third threshold to apply to the planning districts.")]
            public float ThresholdOffset3;

            [RunParameter("Threshold4 Offset", 0.0f, "The offset for the fourth threshold to apply to the planning districts.")]
            public float ThresholdOffset4;

            [RunParameter("Apartment Offset", 0.0f, "Applied to the utility if the household is in an apartment dwelling.")]
            public float ApartmentOffset;

            internal void ApplyConstant(int[] zonePds, float[] zoneConstantsGround, float[] zoneConstantsApt, float[] thresholdOffset1, float[] thresholdOffset2, float[] thresholdOffset3, float[] thresholdOffset4)
            {
                for (int i = 0; i < zoneConstantsGround.Length; i++)
                {
                    if (PlanningDistricts.Contains(zonePds[i]))
                    {
                        zoneConstantsGround[i] += Constant;
                        zoneConstantsApt[i] += Constant + ApartmentOffset;
                        thresholdOffset1[i] += ThresholdOffset1;
                        thresholdOffset2[i] += ThresholdOffset2;
                        thresholdOffset3[i] += ThresholdOffset3;
                        thresholdOffset4[i] += ThresholdOffset4;
                    }
                }
            }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        private void LoadThresholdOffsets()
        {
            var flatZones = _zones.GetFlatData();
            _thresholdOffset1 = new float[flatZones.Length];
            _thresholdOffset2 = new float[flatZones.Length];
            _thresholdOffset3 = new float[flatZones.Length];
            _thresholdOffset4 = new float[flatZones.Length];
            var pds = _zones.GetFlatData().Select(zone => zone.PlanningDistrict).ToArray();
            foreach (var constants in Constants)
            {
                constants.ApplyConstant(pds, _preComputedHouseholdZoneUtilitiesGround, _preComputedHouseholdZoneUtilitiesApt, _thresholdOffset1, _thresholdOffset2, _thresholdOffset3, _thresholdOffset4);
            }
        }

        [SubModelInformation(Required = false, Description = "The spatial constants to apply at the planning district level")]
        public PDConstants[] Constants;

        private void LoadVector(out float[] data, IDataSource<SparseArray<float>> source)
        {
            var load = !source.Loaded;
            if (load)
            {
                source.LoadData();
            }
            data = source.GiveData().GetFlatData();
            if (load)
            {
                source.UnloadData();
            }
        }

        private void LoadMatrix(out float[][] data, IDataSource<SparseTwinIndex<float>> source)
        {
            var load = !source.Loaded;
            if (load)
            {
                source.LoadData();
            }
            data = source.GiveData().GetFlatData();
            if (load)
            {
                source.UnloadData();
            }
        }

        public int ProduceResult(ITashaHousehold data)
        {
            if(data.HomeZone is null)
            {
                throw new XTMFRuntimeException(this, "A household didn't have a homezone!");
            }
            int flathomeZone = _zones.GetFlatIndex(data.HomeZone.ZoneNumber);
            float v = ComputeUtility(data, flathomeZone);
            // now that we have our utility go through them and test against the thresholds.
            var pop = _random.NextDouble();
            if (pop < LogitCDF(v, Threshold1 + _thresholdOffset1[flathomeZone]))
            {
                return 0;
            }
            if (pop < LogitCDF(v, Threshold2 + _thresholdOffset2[flathomeZone]))
            {
                return 1;
            }
            if (pop < LogitCDF(v, Threshold3 + _thresholdOffset3[flathomeZone]))
            {
                return 2;
            }
            if (pop < LogitCDF(v, Threshold4 + _thresholdOffset4[flathomeZone]))
            {
                return 3;
            }
            //TODO: Replace this with a 4+ distribution?
            return 4;
        }

        public float Estimate(ITashaHousehold input, int expectedResult)
        {
            int flathomeZone = _zones.GetFlatIndex(input.HomeZone.ZoneNumber);
            float v = ComputeUtility(input, flathomeZone);
            switch(expectedResult)
            {
                case 0:
                    return LogitCDF(v, Threshold1 + _thresholdOffset1[flathomeZone]);
                case 1:
                    return LogitCDF(v, Threshold2 + _thresholdOffset2[flathomeZone])
                        - LogitCDF(v, Threshold1 + _thresholdOffset1[flathomeZone]);
                case 2:
                    return LogitCDF(v, Threshold3 + _thresholdOffset3[flathomeZone])
                        - LogitCDF(v, Threshold2 + _thresholdOffset2[flathomeZone]);
                case 3:
                    return LogitCDF(v, Threshold4 + _thresholdOffset4[flathomeZone])
                        - LogitCDF(v, Threshold3 + _thresholdOffset3[flathomeZone]);
                default:
                    return 1.0f
                        - LogitCDF(v, Threshold4 + _thresholdOffset4[flathomeZone]);
            }
        }

        private float ComputeUtility(ITashaHousehold data, int flathomeZone)
        {
            float v;
            if(data.DwellingType == DwellingType.Apartment)
            {
                v = _preComputedHouseholdZoneUtilitiesApt[flathomeZone] + Apartment;
            }
            else
            {
                v = _preComputedHouseholdZoneUtilitiesGround[flathomeZone];
            }
            var persons = data.Persons;
            int adults = 0, kids = 0, ftWorkers = 0;
            for (int i = 0; i < persons.Length; i++)
            {
                if(persons[i].Age >= 18)
                {
                    adults++;
                }
                else if(persons[i].Age < 16)
                {
                    kids++;
                }
                if(persons[i].EmploymentStatus == TTSEmploymentStatus.FullTime)
                {
                    ftWorkers++;
                }
            }
            var licenses = persons.Count(p => p.Licence);
            // kids are under 16, so the number of persons 16+ is length - kids
            v += licenses == (persons.Length - kids) ? SufficientLicenses : 0;
            v += NumberOfAdults * adults;
            v += NumberOfKids * kids;
            v += NumberOfFTWorkers * ftWorkers;
            switch (licenses)
            {
                case 0:
                    // there is nothing to add if there is no driver's license
                    break;
                case 1:
                    v += DriverLicense1;
                    break;
                case 2:
                    v += DriverLicense2;
                    break;
                // 3+
                default:
                    v += DriverLicense3Plus;
                    break;
            }
            switch (data.IncomeClass)
            {
                case 2:
                    v += Income2;
                    break;
                case 3:
                    v += Income3;
                    break;
                case 4:
                    v += Income4;
                    break;
                case 5:
                    v += Income5;
                    break;
                case 6:
                    v += Income6;
                    break;
                // case 1 (base) or case 7 (invalid)
                default:
                    break;
            }
            return v;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float LogitCDF(float util, float threshold)
        {
            return (float)(1.0 / (1.0 + Math.Exp(-(threshold - util))));
        }

        public void Unload()
        {
            _preComputedHouseholdZoneUtilitiesGround = null;
            _preComputedHouseholdZoneUtilitiesApt = null;
        }

        public bool RuntimeValidation(ref string error)
        {
            _transitNetwork = Root.NetworkData.FirstOrDefault(net => net.NetworkType == TransitNetwork) as ITripComponentCompleteData;
            if (TransitNetwork == null)
            {
                error = (Root.NetworkData.FirstOrDefault(net => net.NetworkType == TransitNetwork) != null) ?
                    $"The network specified {TransitNetwork} is not a valid transit network!" :
                    $"There was no transit network with the name {TransitNetwork} found!";
                return false;
            }

            _autoNetwork = Root.NetworkData.FirstOrDefault(net => net.NetworkType == AutoNetwork) as INetworkCompleteData;
            if (TransitNetwork == null)
            {
                error = (Root.NetworkData.FirstOrDefault(net => net.NetworkType == AutoNetwork) != null) ?
                    $"The network specified {AutoNetwork} is not a valid auto network!" :
                    $"There was no auto network with the name {AutoNetwork} found!";
                return false;
            }
            return true;
        }
    }
}
