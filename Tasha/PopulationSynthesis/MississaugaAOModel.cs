using Datastructure;
using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Tasha.Common;
using TMG;
using XTMF;
using TMG.Functions;

namespace Tasha.PopulationSynthesis;

public class MississaugaAOModel : IEstimableCalculation<ITashaHousehold, int>
{
    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50,150,50);

    [RunParameter("Zones To Process", "", typeof(RangeSet), "The zones to modify the auto ownership for.  Leave blank to apply to all zones.")]
    public RangeSet ZonesToProcess;

    [RunParameter("Auto Network", "Auto", "The name of the auto network to use.")]
    public string AutoNetworkName;

    [RunParameter("Transit Network", "Transit", "The name of the transit network to use.")]
    public string TransitNetworkName;

    private INetworkCompleteData _autoNetwork;
    private ITripComponentCompleteData _transitNetwork;

    [RootModule]
    public ITravelDemandModel Root;

    [SubModelInformation(Required = true, Description = "Population by zone")]
    public IDataSource<SparseArray<float>> Population;

    [SubModelInformation(Required = true, Description = "Employment by zone")]
    public IDataSource<SparseArray<float>> Employment;

    [RunParameter("Time", "7:00", typeof(Time), "The time to use for computing the accessibility terms.")]
    public Time Time;

    [RunParameter("Threshold 1", 0f, "The utility where we start selecting 1 vehicle.")]
    public float Threshold1;
    [RunParameter("Threshold 2", 0f, "The amount past Threshold 1 until we start selecting 2 vehicles.")]
    public float Threshold2;
    [RunParameter("Threshold 3", 0f, "The amount past Threshold 2 until we start selecting 3 vehicles.")]
    public float Threshold3;
    [RunParameter("Threshold 4", 0f, "The amount past Threshold 3 until we start selecting 4 vehicles.")]
    public float Threshold4;

    [RunParameter("BEmploymentIntraZonal", 0.0f, "")]
    public float BEmploymentIntraZonal;
    [RunParameter("BAutoEmployment5", 0.0f, "")]
    public float BAutoEmployment5;
    [RunParameter("BAutoEmployment10", 0.0f, "")]
    public float BAutoEmployment10;
    [RunParameter("BAutoEmployment15", 0.0f, "")]
    public float BAutoEmployment15;
    [RunParameter("BAutoEmployment30", 0.0f, "")]
    public float BAutoEmployment30;
    [RunParameter("BAutoEmployment45", 0.0f, "")]
    public float BAutoEmployment45;

    [RunParameter("BTransitEmployment5", 0.0f, "")]
    public float BTransitEmployment5;
    [RunParameter("BTransitEmployment10", 0.0f, "")]
    public float BTransitEmployment10;
    [RunParameter("BTransitEmployment15", 0.0f, "")]
    public float BTransitEmployment15;
    [RunParameter("BTransitEmployment30", 0.0f, "")]
    public float BTransitEmployment30;
    [RunParameter("BTransitEmployment45", 0.0f, "")]
    public float BTransitEmployment45;

    [RunParameter("BPopulationIntraZonal", 0.0f, "")]
    public float BPopulationIntraZonal;

    [RunParameter("BAutoPopulation5", 0.0f, "")]
    public float BAutoPopulation5;
    [RunParameter("BAutoPopulation10", 0.0f, "")]
    public float BAutoPopulation10;
    [RunParameter("BAutoPopulation15", 0.0f, "")]
    public float BAutoPopulation15;
    [RunParameter("BAutoPopulation30", 0.0f, "")]
    public float BAutoPopulation30;
    [RunParameter("BAutoPopulation45", 0.0f, "")]
    public float BAutoPopulation45;

    [RunParameter("BTransitPopulation5", 0.0f, "")]
    public float BTransitPopulation5;
    [RunParameter("BTransitPopulation10", 0.0f, "")]
    public float BTransitPopulation10;
    [RunParameter("BTransitPopulation15", 0.0f, "")]
    public float BTransitPopulation15;
    [RunParameter("BTransitPopulation30", 0.0f, "")]
    public float BTransitPopulation30;
    [RunParameter("BTransitPopulation45", 0.0f, "")]
    public float BTransitPopulation45;

    [RunParameter("BIntraZonalDistance", 0.0f, "")]
    public float BIntraZonalDistance;

    [RunParameter("BDensity", 0.0f, "")]
    public float BDensity;

    [RunParameter("BLicenses", 0.0f, "")]
    public float BLicenses;

    [RunParameter("BChildren", 0.0f, "")]
    public float BChildren;

    /// <summary>
    /// Holds all of the land-use utilities pre-computed
    /// </summary>
    private float[] _preComputedUtilities;
    private float[] _thresholdOffset1;
    private float[] _thresholdOffset2;
    private float[] _thresholdOffset3;
    private float[] _thresholdOffset4;

    private SparseArray<IZone> _zoneSystem;

    [RunParameter("Random Seed", 12345, "A seed to fix the random number generation.")]
    public int Seed;

    [RunParameter("Estimation Mode", false, "Set this to true to cache the land-use features.")]
    public bool EstimationMode;

    private float[][] _features;

    public void Load()
    {
        _random = new Random(Seed);
        var zoneSystem = Root.ZoneSystem;
        _zoneSystem = zoneSystem.ZoneArray;
        var features = LoadFeatures(zoneSystem,
            GetAutoTime(_zoneSystem),
            GetTransitTime(_zoneSystem));
        ComputeHouseholdZoneUtilities(features);
        ComputeThresholds();
    }

    public sealed class Offset : IModule
    {
        [RunParameter("PDs", "0", typeof(RangeSet), "Range of planning districts to assign to.")]
        public RangeSet PDs;

        [RunParameter("Threshold Offset 1", 0.0f, "")]
        public float ThresholdOffset1;
        [RunParameter("Threshold Offset 2", 0.0f, "")]
        public float ThresholdOffset2;
        [RunParameter("Threshold Offset 3", 0.0f, "")]
        public float ThresholdOffset3;
        [RunParameter("Threshold Offset 4", 0.0f, "")]
        public float ThresholdOffset4;

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new(50,150,50);

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

    [SubModelInformation(Required = false, Description = "Offsets for thresholds for given planning districts")]
    public Offset[] Offsets;

    private void ComputeThresholds()
    {
        _thresholdOffset1 = new float[_zoneSystem.Count];
        _thresholdOffset2 = new float[_thresholdOffset1.Length];
        _thresholdOffset3 = new float[_thresholdOffset1.Length];
        _thresholdOffset4 = new float[_thresholdOffset1.Length];
        var pds = _zoneSystem.GetFlatData().Select(z => z.PlanningDistrict).ToArray();

        foreach (var offset in Offsets)
        {
            for (int i = 0; i < pds.Length; i++)
            {
                if(offset.PDs.Contains(pds[i]))
                {
                    _thresholdOffset1[i] += offset.ThresholdOffset1;
                    _thresholdOffset2[i] += offset.ThresholdOffset2;
                    _thresholdOffset3[i] += offset.ThresholdOffset3;
                    _thresholdOffset4[i] += offset.ThresholdOffset4;
                }
            }
        }
    }

    private void ComputeHouseholdZoneUtilities(float[][] features)
    {
        _preComputedUtilities = new float[_zoneSystem.Count];
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[0], BEmploymentIntraZonal, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[1], BPopulationIntraZonal, _preComputedUtilities);

        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[2], BAutoEmployment5, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[3], BAutoPopulation5, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[4], BAutoEmployment10, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[5], BAutoPopulation10, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[6], BAutoEmployment15, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[7], BAutoPopulation15, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[8], BAutoEmployment30, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[9], BAutoPopulation30, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[10], BAutoEmployment45, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[11], BAutoPopulation45, _preComputedUtilities);

        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[12], BTransitEmployment5, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[13], BTransitPopulation5, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[14], BTransitEmployment10, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[15], BTransitPopulation10, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[16], BTransitEmployment15, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[17], BTransitPopulation15, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[18], BTransitEmployment30, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[19], BTransitPopulation30, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[20], BTransitEmployment45, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[21], BTransitPopulation45, _preComputedUtilities);

        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[22], BIntraZonalDistance, _preComputedUtilities);
        VectorHelper.FusedMultiplyAdd(_preComputedUtilities, features[23], BDensity, _preComputedUtilities);
    }

    private Random _random;

    public int ProduceResult(ITashaHousehold data)
    {
        var zoneNumber = data.HomeZone.ZoneNumber;
        if(ZonesToProcess.Count > 0 && !ZonesToProcess.Contains(zoneNumber))
        {
            return -1;
        }
        var flatHomeZone = _zoneSystem.GetFlatIndex(zoneNumber);
        if (flatHomeZone < 0)
        {
            return 0;
        }
        float v = ComputeUtility(data, flatHomeZone);
        var pop = (float)_random.NextDouble();
        var acc = Threshold1 + _thresholdOffset1[flatHomeZone];
        if (pop < LogitCDF(v, acc))
        {
            return 0;
        }
        acc += Threshold2 + _thresholdOffset2[flatHomeZone];
        if (pop < LogitCDF(v, acc))
        {
            return 1;
        }
        acc += Threshold3 + _thresholdOffset3[flatHomeZone];
        if (pop < LogitCDF(v, acc))
        {
            return 2;
        }
        acc += Threshold4 + _thresholdOffset4[flatHomeZone];
        if (pop < LogitCDF(v, acc))
        {
            return 3;
        }
        return 4;
    }

    private float ComputeUtility(ITashaHousehold data, int flatHomeZone)
    {
        var v = _preComputedUtilities[flatHomeZone];
        var persons = data.Persons;
        int licenses = 0;
        int children = 0;
        for (int i = 0; i < persons.Length; i++)
        {
            if (persons[i].Licence) licenses++;
            if (persons[i].Age < 18) children++;
        }
        v += BLicenses * Math.Min(licenses, 4);
        v += BChildren * Math.Min(children, 4);
        return v;
    }

    /// <summary>
    /// Return the amount of space that is correct
    /// </summary>
    /// <param name="data"></param>
    /// <param name="correct"></param>
    /// <returns></returns>
    public float Estimate(ITashaHousehold data, int correct)
    {
        var flatHomeZone = _zoneSystem.GetFlatIndex(data.HomeZone.ZoneNumber);
        var v = ComputeUtility(data, flatHomeZone);
        switch(correct)
        {
            case 0:
                return LogitCDF(v, Threshold1 + _thresholdOffset1[flatHomeZone]);
            case 1:
                return LogitCDF(v, Threshold1 + _thresholdOffset1[flatHomeZone]
                    + Threshold2 + _thresholdOffset2[flatHomeZone]) - 
                    LogitCDF(v, Threshold1 + _thresholdOffset1[flatHomeZone]);
            case 2:
                return LogitCDF(v, Threshold1 + _thresholdOffset1[flatHomeZone]
                    + Threshold2 + _thresholdOffset2[flatHomeZone]
                    + Threshold3 + _thresholdOffset3[flatHomeZone]) -
                    LogitCDF(v, Threshold1 + _thresholdOffset1[flatHomeZone]
                    + Threshold2 + _thresholdOffset2[flatHomeZone]);
            case 3:
                return LogitCDF(v, Threshold1 + _thresholdOffset1[flatHomeZone]
                    + Threshold2 + _thresholdOffset2[flatHomeZone]
                    + Threshold3 + _thresholdOffset3[flatHomeZone]
                    + Threshold4 + _thresholdOffset4[flatHomeZone]) -
                    LogitCDF(v, Threshold1 + _thresholdOffset1[flatHomeZone]
                    + Threshold2 + _thresholdOffset2[flatHomeZone]
                    + Threshold3 + _thresholdOffset3[flatHomeZone]);
            default:
                return 1.0f - LogitCDF(v, Threshold1 + _thresholdOffset1[flatHomeZone]
                    + Threshold2 + _thresholdOffset2[flatHomeZone]
                    + Threshold3 + _thresholdOffset3[flatHomeZone]
                    + Threshold4 + _thresholdOffset4[flatHomeZone]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float LogitCDF(float util, float threshold)
    {
        return (float)(1.0 / (1.0 + Math.Exp(-(threshold - util))));
    }

    public bool RuntimeValidation(ref string error)
    {
        _transitNetwork = Root.NetworkData.FirstOrDefault(net => net.NetworkType == TransitNetworkName) as ITripComponentCompleteData;
        if (_transitNetwork == null)
        {
            error = (Root.NetworkData.Any(net => net.NetworkType == TransitNetworkName)) ?
                $"The network specified {TransitNetworkName} is not a valid transit network!" :
                $"There was no transit network with the name {TransitNetworkName} found!";
            return false;
        }

        _autoNetwork = Root.NetworkData.FirstOrDefault(net => net.NetworkType == AutoNetworkName) as INetworkCompleteData;
        if (_autoNetwork == null)
        {
            error = (Root.NetworkData.Any(net => net.NetworkType == AutoNetworkName)) ?
                $"The network specified {AutoNetworkName} is not a valid auto network!" :
                $"There was no auto network with the name {AutoNetworkName} found!";
            return false;
        }
        return true;
    }

    public void Unload()
    {
        _preComputedUtilities = null;
        _zoneSystem = null;
    }

    private float[][] LoadFeatures(IZoneSystem zoneSystem, SparseTwinIndex<float> autoTime, SparseTwinIndex<float> transitTime)
    {
        if(EstimationMode && _features != null)
        {
            return _features;
        }

        if (!Employment.Loaded)
        {
            Employment.LoadData();
        }
        if(!Population.Loaded)
        {
            Population.LoadData();
        }
        if(!zoneSystem.Loaded)
        {
            zoneSystem.LoadData();
        }
        var distances = zoneSystem.Distances.GetFlatData();
        var employment = Employment.GiveData().GetFlatData();
        var population = Population.GiveData().GetFlatData();
        var intraZonalDistance = new float[employment.Length];
        for (int i = 0; i < intraZonalDistance.Length; i++)
        {
            intraZonalDistance[i] = distances[i][i];
        }
        var autoEmp45 = new float[employment.Length];
        var autoEmp30 = new float[employment.Length];
        var autoEmp15 = new float[employment.Length];
        var autoEmp10 = new float[employment.Length];
        var autoEmp5 = new float[employment.Length];
        var autoPop45 = new float[employment.Length];
        var autoPop30 = new float[employment.Length];
        var autoPop15 = new float[employment.Length];
        var autoPop10 = new float[employment.Length];
        var autoPop5 = new float[employment.Length];
        var transitEmp45 = new float[employment.Length];
        var transitEmp30 = new float[employment.Length];
        var transitEmp15 = new float[employment.Length];
        var transitEmp10 = new float[employment.Length];
        var transitEmp5 = new float[employment.Length];
        var transitPop45 = new float[employment.Length];
        var transitPop30 = new float[employment.Length];
        var transitPop15 = new float[employment.Length];
        var transitPop10 = new float[employment.Length];
        var transitPop5 = new float[employment.Length];

        var zones = zoneSystem.ZoneArray.GetFlatData();
        var autoTimes = autoTime.GetFlatData();
        var transitTimes = transitTime.GetFlatData();
        for (int o = 0; o < zones.Length; o++)
        {
            for (int d = 0; d < zones.Length; d++)
            {
                if (autoTimes[o][d] <= 45)
                {
                    autoEmp45[o] += employment[d];
                    autoPop45[o] += population[d];
                    if (autoTimes[o][d] <= 30)
                    {
                        autoEmp30[o] += employment[d];
                        autoPop30[o] += population[d];
                        if (autoTimes[o][d] <= 15)
                        {
                            autoEmp15[o] += employment[d];
                            autoPop15[o] += population[d];
                            if (autoTimes[o][d] <= 10)
                            {
                                autoEmp10[o] += employment[d];
                                autoPop10[o] += population[d];
                                if (autoTimes[o][d] <= 5)
                                {
                                    autoEmp5[o] += employment[d];
                                    autoPop5[o] += population[d];
                                }
                            }
                        }
                    }
                }
                if (transitTimes[o][d] <= 45)
                {
                    transitEmp45[o] += employment[d];
                    transitPop45[o] += population[d];
                    if (transitTimes[o][d] <= 30)
                    {
                        transitEmp30[o] += employment[d];
                        transitPop30[o] += population[d];
                        if (transitTimes[o][d] <= 15)
                        {
                            transitEmp15[o] += employment[d];
                            transitPop15[o] += population[d];
                            if (autoTimes[o][d] <= 10)
                            {
                                transitEmp10[o] += employment[d];
                                transitPop10[o] += population[d];
                                if (transitTimes[o][d] <= 5)
                                {
                                    transitEmp5[o] += employment[d];
                                    transitPop5[o] += population[d];
                                }
                            }
                        }
                    }
                }
            }
        }
        var ret = new float[][]
        {
           TakeLogP1(employment),
           TakeLogP1(population),
           TakeLogP1(autoEmp5),
           TakeLogP1(autoPop5),
           TakeLogP1(autoEmp10),
           TakeLogP1(autoPop10),
           TakeLogP1(autoEmp15),
           TakeLogP1(autoPop15),
           TakeLogP1(autoEmp30),
           TakeLogP1(autoPop30),
           TakeLogP1(autoEmp45),
           TakeLogP1(autoPop45),
           TakeLogP1(transitEmp5),
           TakeLogP1(transitPop5),
           TakeLogP1(transitEmp10),
           TakeLogP1(transitPop10),
           TakeLogP1(transitEmp15),
           TakeLogP1(transitPop15),
           TakeLogP1(transitEmp30),
           TakeLogP1(transitPop30),
           TakeLogP1(transitEmp45),
           TakeLogP1(transitPop45),
           intraZonalDistance,
           TakeLogP1(Divide(Add(employment,population), intraZonalDistance))
        };
        if(EstimationMode)
        {
            _features = ret;
        }
        return ret;
    }

    private static float[] Add(float[] first, float[] second)
    {
        var ret = new float[first.Length];
        for (int i = 0; i < first.Length; i++)
        {
            ret[i] = first[i] + second[i];
        }
        return ret;
    }

    private static float[] Divide(float[] lhs, float[] rhs)
    {
        var ret = new float[lhs.Length];
        for (int i = 0; i < lhs.Length; i++)
        {
            ret[i] = lhs[i] / rhs[i];
        }
        return ret;
    }

    private static float[] TakeLogP1(float[] array)
    {
        var ret = new float[array.Length];
        for (int i = 0; i < array.Length; i++)
        {
            ret[i] = (float)Math.Log(array[i] + 1);
        }
        return ret;
    }

    private SparseTwinIndex<float> GetAutoTime(SparseArray<IZone> zones)
    {
        var data = _autoNetwork.GetTimePeriodData(Time);
        var ret = zones.CreateSquareTwinArray<float>();
        var flat = ret.GetFlatData();
        for (int i = 0; i < flat.Length; i++)
        {
            var flatRow = flat[i];
            var rowOffset = (i * flat.Length);
            for (int j = 0; j < flat[i].Length; j++)
            {
                flatRow[j] = data[(rowOffset + j) * 2];
            }
        }
        return ret;
    }

    private SparseTwinIndex<float> GetTransitTime(SparseArray<IZone> zones)
    {
        var data = _transitNetwork.GetTimePeriodData(Time);
        var ret = zones.CreateSquareTwinArray<float>();
        var flat = ret.GetFlatData();
        for (int i = 0; i < flat.Length; i++)
        {
            var flatRow = flat[i];
            var rowOffset = (i * flat.Length);
            for (int j = 0; j < flat[i].Length; j++)
            {
                flatRow[j] = data[(rowOffset + j) * 5]
                    + data[(rowOffset + j) * 5 + 1]
                    + data[(rowOffset + j) * 5 + 2];
            }
        }
        return ret;
    }
}
