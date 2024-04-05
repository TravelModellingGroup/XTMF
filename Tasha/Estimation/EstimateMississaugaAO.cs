using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF;
using TMG;
using TMG.Input;
using Tasha.Common;
using Tasha.PopulationSynthesis;
using TMG.Estimation;
using System.Diagnostics;
using Activity = Tasha.Common.Activity;

namespace Tasha.Estimation;

public sealed class EstimateMississaugaAO : ITashaRuntime
{
    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50,150,50);

    [DoNotAutomate]
    public List<ITashaMode> AllModes => throw new NotImplementedException();
    [DoNotAutomate]
    public ITashaMode AutoMode { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public IVehicleType AutoType { get; set; }
    public Time EndOfDay { get; set; }
    IDataLoader<ITashaHousehold> ITashaRuntime.HouseholdLoader { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public int TotalIterations { get; set; }
    [DoNotAutomate]
    public ITashaModeChoice ModeChoice { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    [DoNotAutomate]
    public List<ITashaMode> NonSharedModes { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    [DoNotAutomate]
    public List<ITashaMode> OtherModes { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public bool Parallel { get; set; }
    [DoNotAutomate]
    public List<IPostHousehold> PostHousehold { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    [DoNotAutomate]
    public List<IPostIteration> PostIteration { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    [DoNotAutomate]
    public List<ISelfContainedModule> PostRun { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    [DoNotAutomate]
    public List<IPostScheduler> PostScheduler { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    [DoNotAutomate]
    public List<IPreIteration> PreIteration { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    [DoNotAutomate]
    public List<ISelfContainedModule> PreRun { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public int RandomSeed { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    [DoNotAutomate]
    public List<ISharedMode> SharedModes { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    [DoNotAutomate]
    public Time StartOfDay { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    [DoNotAutomate]
    public List<IVehicleType> VehicleTypes { get; set; }

    public IList<INetworkData> NetworkData { get; set; }

    public IZoneSystem ZoneSystem { get; set; }

    [RunParameter("Input Directory", "../../Input", "The directory that contains the input for this model system.")]
    public string InputBaseDirectory { get; set; }
    public string OutputBaseDirectory { get; set; }

    public List<IResource> Resources { get; set; }

    [SubModelInformation(Required = true, Description = "The households to estimate against.")]
    public IDataLoader<ITashaHousehold> HouseholdLoader;

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    private ITashaHousehold[] _households;

    [SubModelInformation(Required = true, Description = "The model to estimate")]
    public IEstimableCalculation<ITashaHousehold, int> Model;

    [RootModule]
    public IEstimationClientModelSystem Root;

    private double[] _fitness;

    public void Start()
    {
        if(_households == null)
        {
            Console.WriteLine("Loading one time data");
            ZoneSystem.LoadData();
            foreach(var network in NetworkData)
            {
                network.LoadData();
            }
            HouseholdLoader.LoadData();
            _households = HouseholdLoader.ToArray();
            _fitness = new double[_households.Length];
            Console.WriteLine("Finished loading one time data");
        }
        // The model needs to be loaded every time so it can compute the household zone utilities
        Model.Load();
        System.Threading.Tasks.Parallel.For(0, _households.Length,
            (int i) =>
            {
                var probabilityCorrect = Model.Estimate(_households[i], Math.Min(_households[i].Vehicles.Length, 4));
                _fitness[i] = Math.Log(probabilityCorrect + 0.0001);
            });
        Root.RetrieveValue = () => (float)_fitness.Sum();
    }

    public ITrip CreateTrip(ITripChain chain, IZone originalZone, IZone destinationZone, Activity purpose, Time startTime)
    {
        return null;
    }

    public int GetIndexOfMode(ITashaMode mode)
    {
        return -1;
    }

    public bool ExitRequest()
    {
        return false;
    }
}
