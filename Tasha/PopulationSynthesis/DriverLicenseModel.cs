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

        [RunParameter("Constant", 0.183f, "The model's constant.")]
        public float Constant;
        [RunParameter("Female", -0.746f, "Applied if the person is female.")]
        public float Female;

        [RunParameter("Age16To20", -0.692f, "A term to add if the person is within the age range.")]
        public float Age16To20;
        [RunParameter("Age21To25", 0.395f, "A term to add if the person is within the age range.")]
        public float Age21To25;
        [RunParameter("Age26To35", 0.781f, "A term to add if the person is within the age range.")]
        public float Age26To35;
        [RunParameter("Age36To45", 1.145f, "A term to add if the person is within the age range.")]
        public float Age36To45;
        [RunParameter("Age46To55", 1.061f, "A term to add if the person is within the age range.")]
        public float Age46To55;
        [RunParameter("Age56To65", 1.026f, "A term to add if the person is within the age range.")]
        public float Age56To65;
        [RunParameter("Age66To75", 1.010f, "A term to add if the person is within the age range.")]
        public float Age66To75;

        [RunParameter("Occupation General", 0.611f, "A term to add if the person works in the occupation category.")]
        public float OccGeneral;
        [RunParameter("Occupation Manufacturing", 0.793f, "A term to add if the person works in the occupation category.")]
        public float OccManufacturing;
        [RunParameter("Occupation Professional", 1.268f, "A term to add if the person works in the occupation category.")]
        public float OccProfessional;
        [RunParameter("Occupation Sales", 0.492f, "A term to add if the person works in the occupation category.")]
        public float OccSales;

        [RunParameter("Part Time", -0.324f, "A term to add if the person works part time outside of the home.")]
        public float PartTime;

        [RunParameter("Income Category 2", 0.421f, "A term to add if the person belongs to a household within the TTS income category 2.")]
        public float Income2;
        [RunParameter("Income Category 3", 0.687f, "A term to add if the person belongs to a household within the TTS income category 2.")]
        public float Income3;
        [RunParameter("Income Category 4", 0.821f, "A term to add if the person belongs to a household within the TTS income category 2.")]
        public float Income4;
        [RunParameter("Income Category 5", 0.964f, "A term to add if the person belongs to a household within the TTS income category 2.")]
        public float Income5;
        [RunParameter("Income Category 6", 1.274f, "A term to add if the person belongs to a household within the TTS income category 2.")]
        public float Income6;

        [RunParameter("Population Density", -28.814f, "Applied against the population density for the home zone. (pop/m^2)")]
        public float PopulationDensityBeta;
        [RunParameter("Transit Perceived Time", 0.002f, "Applied against the perceived travel time to work and school. (minutes)")]
        public float TransitPerceivedTravelTime;
        [RunParameter("Distance to Work and School", 0.020f, "Applied against the distance to work and school. (km)")]
        public float DistanceToWorkSchoolBeta;

        private Random _random;

        [RunParameter("Random Seed", 4564616, "The fixed seed to start the pseudo-random number generator with.")]
        public int RandomSeed;

        [RunParameter("Time To Use", "7:00", typeof(Time), "The time of day to use for computing travel times.")]
        public Time TimeToUse;

        private SparseArray<IZone> _zones;


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
            var v = Constant;
            if(data.Female)
            {
                v += Female;
            }
            switch(ageClass)
            {
                case 0:
                    v += Age16To20;
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
                // Do nothing for age classes for 76+
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
            return true;
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
                    $"There was not transit network with the name {TransitNetworkName} found!";
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
