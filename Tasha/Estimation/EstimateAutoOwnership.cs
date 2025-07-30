/*
    Copyright 2025 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using XTMF;
using TMG;
using Tasha.Common;
using TMG.Estimation;
using Activity = Tasha.Common.Activity;
using System.Numerics;
using TMG.Functions;

namespace Tasha.Estimation;

[RedirectModule("Tasha.Estimation.EstimateMississaugaAO, Tasha, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null")]
public sealed class EstimateAutoOwnership : ITashaRuntime
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

    [RunParameter("Maximum Number of Vehicles", 4, "The maximum number of vehicles that will be selected.")]
    public int MaximumNumberOfVehicles;

    private float[] _fitness;

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
            _households = [.. HouseholdLoader];
            _fitness = new float[_households.Length];
            Console.WriteLine("Finished loading one time data");
        }
        // The model needs to be loaded every time so it can compute the household zone utilities
        Model.Load();
        System.Threading.Tasks.Parallel.For(0, _households.Length,
            i =>
            {
                var probabilityCorrect = Model.Estimate(_households[i], Math.Min(_households[i].Vehicles.Length, MaximumNumberOfVehicles));
                _fitness[i] = probabilityCorrect + 0.0001f;
            });
        VectorHelper.Log(_fitness, 0, _fitness, 0, _fitness.Length);
        VectorHelper.Multiply(_fitness, 0, _fitness, 0, 1.0f / _households.Length, _fitness.Length);
        Root.RetrieveValue = () => _fitness.Sum();
        Model.Unload();
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
