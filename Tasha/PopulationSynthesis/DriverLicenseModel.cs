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
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Tasha.PopulationSynthesis
{
    [ModuleInformation(Description = "GTAModel V4.1.0's Driver's License Model.")]
    public class DriverLicenseModel : ICalculation<ITashaPerson, bool>
    {
        [RootModule]
        public ITravelDemandModel Root;

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

        public void Load()
        {
            _random = new Random(RandomSeed);
            LoadPopulationDensity();
            _zones = Root.ZoneSystem.ZoneArray;
            _distances = Root.ZoneSystem.Distances.GetFlatData();
            LoadZonalConstants();
            _additionalIncomeCategories = AdditionalIncomeCategories.Select(x => x.Category).ToArray();
            _additionalIncomeValues = AdditionalIncomeCategories.Select(x => x.Value).ToArray();
        }

        [ModuleInformation(Description = "Provides the ability to apply a dummy variable for the given planning districts.")]
        public sealed class PDConstants : IModule
        {
            public string Name { get; set; }

            public float Progress => 0f;

            public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

            [RunParameter("Planning Districts", "0", typeof(RangeSet), "The planning districts to apply this constant to.")]
            public RangeSet PlanningDistricts;

            [RunParameter("Constant", 0.0f, "The constant to apply to the planning districts.")]
            public float Constant;

            internal void ApplyConstant(int[] zonePds, float[] zoneConstants)
            {
                for (int i = 0; i < zoneConstants.Length; i++)
                {
                    if (PlanningDistricts.Contains(zonePds[i]))
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

        [SubModelInformation(Required = false, Description = "Constants to apply at the TAZ level.")]
        public IDataSource<SparseArray<float>> ZonalConstants;

        private void LoadZonalConstants()
        {
            var flatZones = _zones.GetFlatData();
            _zonalConstants = ZonalConstants switch
            {
                null => new float[flatZones.Length],
                _ => LoadResource(ZonalConstants),
            };
            var pds = _zones.GetFlatData().Select(zone => zone.PlanningDistrict).ToArray();
            foreach (var constants in Constants)
            {
                constants.ApplyConstant(pds, _zonalConstants);
            }
        }

        private static T[] LoadResource<T>(IDataSource<SparseArray<T>> zonalConstants)
        {
            var loaded = zonalConstants.Loaded;
            if (!loaded)
            {
                zonalConstants.LoadData();
            }
            var ret = zonalConstants.GiveData();
            if (!loaded)
            {
                zonalConstants.UnloadData();
            }
            return ret.GetFlatData();
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

        [RunParameter("Full Time", 0.0f, "A constant applied if the person is a full-time worker.")]
        public float FullTime;

        [RunParameter("Part Time", -0.3366f, "A term to add if the person works part time outside of the home.")]
        public float PartTime;

        [RunParameter("Student", 0.0f, "A constant applied if the person is a student.")]
        public float Student;

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

        [RunParameter("Max Transit Perceived Time", float.PositiveInfinity, "The maximum perceived time to use when computing the utilities.")]
        public float MaxTransitPerceivedTime;

        [RunParameter("Minimum Age", 16, "The minimum age for a person to have a driver license.")]
        public int MinimumAge;

        [RunParameter("School Plus Work", true, "Should we add the distance of work and school or just use work for workers and school for students?")]
        public bool SchoolPlusWork;

        private SparseArray<IZone> _zones;
        private float[] _zonalConstants;


        public bool ProduceResult(ITashaPerson data)
        {
            var household = data.Household;
            var flatHhldZone = _zones.GetFlatIndex(household.HomeZone.ZoneNumber);
            var age = data.Age;
            // you have to be older than 16 to drive
            if (age < MinimumAge)
            {
                return false;
            }
            var ageClass = (age - 16) / 5;
            var distanceRow = _distances[flatHhldZone];
            var v = Constant
                + _zonalConstants[flatHhldZone]
                + (data.Female ? Female : 0f)
                + ageClass switch
                {
                    0 => age < 18 ? Age16To17 : Age18To20,
                    1 => Age21To25,
                    2 or 3 => Age26To35,
                    4 or 5 => Age36To45,
                    6 or 7 => Age46To55,
                    8 or 9 => Age56To65,
                    10 or 11 => Age66To75,
                    12 or 13 => Age76To85,
                    _ => 0f,
                }

                + data.Occupation switch
                {
                    Occupation.Office => OccGeneral,
                    Occupation.Manufacturing => OccManufacturing,
                    Occupation.Retail => OccSales,
                    Occupation.Professional => OccProfessional,
                    _ => 0f,
                }
                + data.StudentStatus switch
                {
                    StudentStatus.FullTime => Student,
                    _ => 0f
                }
                + data.EmploymentStatus switch
                {
                    TTSEmploymentStatus.FullTime => FullTime,
                    TTSEmploymentStatus.PartTime => PartTime,
                    _ => 0f,
                }
                + household.IncomeClass switch
                {
                    2 => Income2,
                    3 => Income3,
                    4 => Income4,
                    5 => Income5,
                    6 => Income6,
                    var otherIncomeClass => AdditionalIncomes(otherIncomeClass),
                }
                + PopulationDensityBeta * _populationDensity[flatHhldZone]

                // Add the combined travel times for home to work and home to school if they exist.
                + GetWorkSchoolUtility(distanceRow, data, flatHhldZone)

            ;
            // check for a non-sensible result.
            if (float.IsInfinity(v) | float.IsNaN(v))
            {
                Debug(flatHhldZone, data);
            }
            // Binary logit
            var eToV = MathF.Exp(v);
            return _random.NextDouble() < (eToV / (1.0f + eToV));
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        private float GetWorkSchoolUtility(float[] distanceRow, ITashaPerson data, int flatHhldZone)
        {
            var employmentZone = data.EmploymentZone;
            var schoolZone = data.SchoolZone;
            if (SchoolPlusWork)
            {
                var distanceUtil = DistanceToWorkSchoolBeta * (DistanceIfExists(distanceRow, employmentZone, _zones)
                + DistanceIfExists(distanceRow, schoolZone, _zones));
                var timeUtil = TransitPerceivedTravelTime * (TravelTimeIfExists(TransitNetwork, flatHhldZone, schoolZone, _zones, TimeToUse)
                    + TravelTimeIfExists(TransitNetwork, flatHhldZone, employmentZone, _zones, TimeToUse));
                return distanceUtil + timeUtil;
            }
            else
            {
                return ((data.EmploymentStatus, data.StudentStatus) switch
                {
                    (TTSEmploymentStatus.FullTime or TTSEmploymentStatus.WorkAtHome_FullTime, _) =>
                        DistanceToWorkSchoolBeta * DistanceIfExists(distanceRow, employmentZone, _zones)
                        + TransitPerceivedTravelTime * TravelTimeIfExists(TransitNetwork, flatHhldZone, employmentZone, _zones, TimeToUse),
                    (_, StudentStatus.FullTime) =>
                        DistanceToWorkSchoolBeta * DistanceIfExists(distanceRow, schoolZone, _zones)
                        + TransitPerceivedTravelTime * TravelTimeIfExists(TransitNetwork, flatHhldZone, schoolZone, _zones, TimeToUse),
                    (TTSEmploymentStatus.PartTime or TTSEmploymentStatus.WorkAtHome_PartTime, _) =>
                        DistanceToWorkSchoolBeta * DistanceIfExists(distanceRow, employmentZone, _zones)
                        + TransitPerceivedTravelTime * TravelTimeIfExists(TransitNetwork, flatHhldZone, employmentZone, _zones, TimeToUse),
                    (_, StudentStatus.PartTime) =>
                        DistanceToWorkSchoolBeta * DistanceIfExists(distanceRow, schoolZone, _zones)
                        + TransitPerceivedTravelTime * TravelTimeIfExists(TransitNetwork, flatHhldZone, schoolZone, _zones, TimeToUse),
                    _ => 0f,
                });
            }
        }

        [ModuleInformation(Description = "Provides a general way of giving a constant for a particular number of something coming from the household.")]
        public sealed class IncomeCategory : IModule
        {
            [RunParameter("Category", 0, "The income category for this value.", Index = 0)]
            public int Category;

            [RunParameter("Value", 0.0f, "The value this option represents.", Index = 1)]
            public float Value;

            public string Name { get; set; }

            public float Progress => 0f;

            public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        [SubModelInformation(Required = false, Description = "Additional income category constants.")]
        public IncomeCategory[] AdditionalIncomeCategories;

        private int[] _additionalIncomeCategories;
        private float[] _additionalIncomeValues;

        private float AdditionalIncomes(int incomeClass)
        {
            for (int i = 0; i < _additionalIncomeCategories.Length; i++)
            {
                if (incomeClass == _additionalIncomeCategories[i])
                {
                    return _additionalIncomeValues[i];
                }
            }
            return 0f;
        }

        private void Debug(int flatHhldZone, ITashaPerson person)
        {
            if (float.IsInfinity(_populationDensity[flatHhldZone]) | float.IsNaN(_populationDensity[flatHhldZone]))
            {
                throw new XTMFRuntimeException(this, $"The population density for the zone {Root.ZoneSystem.ZoneArray.GetFlatData()[flatHhldZone].ZoneNumber} was NaN." +
                    $" Please check to make sure that the distance matrix is Non-Zero for intra-zonals.");
            }
            else
            {
                throw new XTMFRuntimeException(this, $"An unknown error has caused the driver license model to produce an invalid utility for a household in the zone {Root.ZoneSystem.ZoneArray.GetFlatData()[flatHhldZone].ZoneNumber} was NaN." +
                    $" HouseholdID:{person.Household.HouseholdId}, PersonID:{Array.IndexOf(person.Household.Persons, person) + 1}");
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float TravelTimeIfExists(ITripComponentData network, int flatOrigin, IZone zone, SparseArray<IZone> zones, Time time)
        {
            if (zone == null)
            {
                return 0f;
            }
            int index = zones.GetFlatIndex(zone.ZoneNumber);
            if (index < 0)
            {
                return 0f;
            }
            var ret = network.BoardingTime(flatOrigin, index, time).ToMinutes();
            return MathF.Min(ret, MaxTransitPerceivedTime);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DistanceIfExists(float[] distanceRow, IZone zone, SparseArray<IZone> zones)
        {
            if (zone == null)
            {
                return 0f;
            }
            int index = zones.GetFlatIndex(zone.ZoneNumber);
            if (index < 0)
            {
                return 0f;
            }
            // convert to KM
            return distanceRow[index] / 1000f;
        }

        public bool RuntimeValidation(ref string error)
        {
            TransitNetwork = Root.NetworkData.FirstOrDefault(net => net.NetworkType == TransitNetworkName) as ITripComponentData;
            if (TransitNetwork == null)
            {
                error = (Root.NetworkData.FirstOrDefault(net => net.NetworkType == TransitNetworkName) != null) ?
                    $"The network specified {TransitNetworkName} is not a valid transit network!" :
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
