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
using XTMF;

namespace Tasha.PopulationSynthesis
{
    [ModuleInformation(Description = "GTAModel V4.1.0's Driver's License Model.")]
    public class DriverLicenseModel : ICalculation<ITashaPerson, bool>
    {
        [RootModule]
        public ITravelDemandModel Root;

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50,150,50);

        public void Load()
        {
            _random = new Random(RandomSeed);
            LoadPopulationDensity();
            _zones = Root.ZoneSystem.ZoneArray;
            _distances = Root.ZoneSystem.Distances.GetFlatData();
            LoadZonalConstants();
        }

        public sealed class PDConstants : IModule
        {
            public string Name { get; set; }

            public float Progress => 0f;

            public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50,150,50);

            [RunParameter("Planning Districts", "0", typeof(RangeSet), "The planning districts to apply this constant to.")]
            public RangeSet PlanningDistricts;

            [RunParameter("Constant", 0.0f, "The constant to apply to the planning districts.")]
            public float Constant;

            internal void ApplyConstant(int[] zonePds, float[] zoneConstants)
            {
                for (int i = 0; i < zoneConstants.Length; i++)
                {
                    if(PlanningDistricts.Contains(zonePds[i]))
                    {
                        zoneConstants[i] += Constant;
                    }
                }
            }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        [SubModelInformation(Required = false, Description = "The spatial constants to apply at the planning district level")]
        public PDConstants[] Constants;
        

        private void LoadZonalConstants()
        {
            var flatZones = _zones.GetFlatData();
            _zonalConstants = new float[flatZones.Length];
            var pds = _zones.GetFlatData().Select(zone => zone.PlanningDistrict).ToArray();
            foreach(var constants in Constants)
            {
                constants.ApplyConstant(pds, _zonalConstants);
            }
        }

        private void LoadPopulationDensity()
        {
            var load = !PopulationDensity.Loaded;
            if (load)
            {
                PopulationDensity.LoadData();
            }
            _populationDensity = PopulationDensity.GiveData().GetFlatData();
            if (load)
            {
                PopulationDensity.UnloadData();
            }
        }

        [SubModelInformation(Required = true, Description = "The population density in pop/m^2")]
        public IDataSource<SparseArray<float>> PopulationDensity;
        private float[] _populationDensity;
        private float[][] _distances;

        [RunParameter("Transit Network Name", "Transit", "The name of the transit network to get the perceived travel times from.")]
        public string TransitNetworkName;

        private ITripComponentData TransitNetwork;

        [RunParameter("Constant", -0.7586f, "The model's constant.")]
        public float Constant;
        [RunParameter("Female", -0.7775f, "Applied if the person is female.")]
        public float Female;

        [RunParameter("Age16To17", -0.3646f, "A term to add if the person is within the age range.")]
        public float Age16To17;
        [RunParameter("Age18To20", 0.9070f, "A term to add if the person is within the age range.")]
        public float Age18To20;
        [RunParameter("Age21To25", 1.3652f, "A term to add if the person is within the age range.")]
        public float Age21To25;
        [RunParameter("Age26To35", 1.7433f, "A term to add if the person is within the age range.")]
        public float Age26To35;
        [RunParameter("Age36To45", 2.1094f, "A term to add if the person is within the age range.")]
        public float Age36To45;
        [RunParameter("Age46To55", 2.0226f, "A term to add if the person is within the age range.")]
        public float Age46To55;
        [RunParameter("Age56To65", 1.9770f, "A term to add if the person is within the age range.")]
        public float Age56To65;
        [RunParameter("Age66To75", 1.9461f, "A term to add if the person is within the age range.")]
        public float Age66To75;
        [RunParameter("Age76To85", 1.2269f, "A term to add if the person is within the age range.")]
        public float Age76To85;

        [RunParameter("Occupation General", 0.5737f, "A term to add if the person works in the occupation category.")]
        public float OccGeneral;
        [RunParameter("Occupation Manufacturing", 0.7314f, "A term to add if the person works in the occupation category.")]
        public float OccManufacturing;
        [RunParameter("Occupation Professional", 1.2349f, "A term to add if the person works in the occupation category.")]
        public float OccProfessional;
        [RunParameter("Occupation Sales", 0.4507f, "A term to add if the person works in the occupation category.")]
        public float OccSales;

        [RunParameter("Part Time", -0.3366f, "A term to add if the person works part time outside of the home.")]
        public float PartTime;

        [RunParameter("Income Category 2", 0.4493f, "A term to add if the person belongs to a household within the TTS income category 2.")]
        public float Income2;
        [RunParameter("Income Category 3", 0.7008f, "A term to add if the person belongs to a household within the TTS income category 3.")]
        public float Income3;
        [RunParameter("Income Category 4", 0.8689f, "A term to add if the person belongs to a household within the TTS income category 4.")]
        public float Income4;
        [RunParameter("Income Category 5", 1.0242f, "A term to add if the person belongs to a household within the TTS income category 5.")]
        public float Income5;
        [RunParameter("Income Category 6", 1.3385f, "A term to add if the person belongs to a household within the TTS income category 6.")]
        public float Income6;

        [RunParameter("Population Density", -29.0526f, "Applied against the population density for the home zone. (pop/m^2)")]
        public float PopulationDensityBeta;
        [RunParameter("Transit Perceived Time", 0.0018f, "Applied against the perceived travel time to work and school. (minutes)")]
        public float TransitPerceivedTravelTime;
        [RunParameter("Distance to Work and School", 0.0200f, "Applied against the distance to work and school. (km)")]
        public float DistanceToWorkSchoolBeta;

        private Random _random;

        [RunParameter("Random Seed", 4564616, "The fixed seed to start the pseudo-random number generator with.")]
        public int RandomSeed;

        [RunParameter("Time To Use", "7:00", typeof(Time), "The time of day to use for computing travel times.")]
        public Time TimeToUse;

        private SparseArray<IZone> _zones;
        private float[] _zonalConstants;


        public bool ProduceResult(ITashaPerson data)
        {
            var household = data.Household;
            var flatHhldZone = _zones.GetFlatIndex(household.HomeZone.ZoneNumber);
            var age = data.Age;
            // you have to be older than 16 to drive
            if(age < 16)
            {
                return false;
            }
            var ageClass = (age - 16) / 5;
            var v = Constant + _zonalConstants[flatHhldZone];
            if(data.Female)
            {
                v += Female;
            }
            switch(ageClass)
            {
                case 0:
                    v += age < 18 ? Age16To17 : Age18To20;
                    break;
                case 1:
                    v += Age21To25;
                    break;
                case 2:
                case 3:
                    v += Age26To35;
                    break;
                case 4:
                case 5:
                    v += Age36To45;
                    break;
                case 6:
                case 7:
                    v += Age46To55;
                    break;
                case 8:
                case 9:
                    v += Age56To65;
                    break;
                case 10:
                case 11:
                    v += Age66To75;
                    break;
                case 12:
                case 13:
                    v += Age76To85;
                    break;
                // Do nothing for age classes for 86+
                default:
                    break;
            }
            switch(data.Occupation)
            {
                case Occupation.Office:
                    v += OccGeneral;
                    break;
                case Occupation.Manufacturing:
                    v += OccManufacturing;
                    break;
                case Occupation.Professional:
                    v += OccProfessional;
                    break;
                case Occupation.Retail:
                    v += OccSales;
                    break;
                    // Do nothing for the rest
                default:
                    break;
                        
            }
            if(data.EmploymentStatus == TTSEmploymentStatus.PartTime)
            {
                v += PartTime;
            }
            switch(household.IncomeClass)
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
                    // Income class 1 is our base
                default:
                    break;
            }

            v += PopulationDensityBeta * _populationDensity[flatHhldZone];

            // Add the combined travel times for home to work and home to school if they exist.
            v += TransitPerceivedTravelTime * (
                 TravelTimeIfExists(TransitNetwork, flatHhldZone, data.EmploymentZone, _zones, TimeToUse)
                + TravelTimeIfExists(TransitNetwork, flatHhldZone, data.SchoolZone, _zones, TimeToUse)
                );
            var distanceRow = _distances[flatHhldZone];
            v += DistanceToWorkSchoolBeta * (
                 DistanceIfExists(distanceRow, data.EmploymentZone, _zones)
                + DistanceIfExists(distanceRow, data.SchoolZone, _zones)
                );
            // Binary logit
            var eToV = Math.Exp(v);
            return _random.NextDouble() < (eToV / (1.0f + eToV));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TravelTimeIfExists(ITripComponentData network, int flatOrigin, IZone zone, SparseArray<IZone> zones, Time time)
        {
            if (zone == null)
            {
                return 0f;
            }
            int index = zones.GetFlatIndex(zone.ZoneNumber);
            if(index < 0)
            {
                return 0f;
            }
            return network.BoardingTime(flatOrigin, index, time).ToMinutes();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DistanceIfExists(float[] distanceRow, IZone zone, SparseArray<IZone> zones)
        {
            if (zone == null)
            {
                return 0f;
            }
            int index = zones.GetFlatIndex(zone.ZoneNumber);
            if(index < 0)
            {
                return 0f;
            }
            // convert to KM
            return distanceRow[index] / 1000f;
        }

        public bool RuntimeValidation(ref string error)
        {
            TransitNetwork = Root.NetworkData.FirstOrDefault(net => net.NetworkType == TransitNetworkName) as ITripComponentData;
            if(TransitNetwork == null)
            {
                error = (Root.NetworkData.FirstOrDefault(net => net.NetworkType == TransitNetworkName) != null) ?
                    $"The network specified {TransitNetworkName} is not a valid transit network!":
                    $"There was no transit network with the name {TransitNetworkName} found!";
                return false;
            }
            return true;
        }

        public void Unload()
        {
            _populationDensity = null;
            _random = null;
        }
    }
}
