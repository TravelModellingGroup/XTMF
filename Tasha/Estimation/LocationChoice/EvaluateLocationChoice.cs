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
using System.Threading;
using Tasha.Common;
using XTMF;
using TMG.Estimation;
using Tasha.Scheduler;
using TMG.Input;
using Datastructure;
using TMG;

namespace Tasha.Estimation.LocationChoice;

public class EvaluateLocationChoice : IPostHousehold
{
    [SubModelInformation(Required = true, Description = "The location choice model to evaluate.")]
    public ILocationChoiceModel LocationChoice;

    [RootModule]
    public IEstimationClientModelSystem Root;

    [SubModelInformation(Required = false, Description = "The location to save a confusion matrix for validation.")]
    public FileLocation ConfusionMatrix;

    [SubModelInformation(Required = false, Description = "The location to optionally save the extracted data")]
    public FileLocation ModelWorkOD;

    [SubModelInformation(Required = false, Description = "The location to optionally save the extracted data")]
    public FileLocation ModelMarketOD;

    [SubModelInformation(Required = false, Description = "The location to optionally save the extracted data")]
    public FileLocation ModelOtherOD;

    [SubModelInformation(Required = false, Description = "The location to optionally save the extracted data")]
    public FileLocation ObservedWorkOD;

    [SubModelInformation(Required = false, Description = "The location to optionally save the extracted data")]
    public FileLocation ObservedMarketOD;

    [SubModelInformation(Required = false, Description = "The location to optionally save the extracted data")]
    public FileLocation ObservedOtherOD;

    SparseTwinIndex<float> Choices;

    SpinLock ChoicesLock = new(false);

    public string Name
    {
        get; set;
    }

    public float Progress
    {
        get
        {
            return 0f;
        }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get
        {
            return null;
        }
    }

    SpinLock FitnessLock = new(false);
    private float Fitness;

    private SparseArray<IZone> ZoneSystem;

    public void Execute(ITashaHousehold household, int iteration)
    {
        var localFitness = 0.0f;
        var persons = household.Persons;
        bool taken;
        if (ConfusionMatrix == null)
        {
            for (int personIndex = 0; personIndex < persons.Length; personIndex++)
            {
                var tripChains = persons[personIndex].TripChains;
                for (int tcIndex = 0; tcIndex < tripChains.Count; tcIndex++)
                {
                    var trips = tripChains[tcIndex].Trips;
                    IEpisode[] episodes = BuildScheduleFromTrips(trips);
                    for (int tripIndex = 0; tripIndex < trips.Count - 1; tripIndex++)
                    {
                        var activtiyType = episodes[tripIndex].ActivityType;
                        int revieldChoice = ZoneSystem.GetFlatIndex(trips[tripIndex].DestinationZone.ZoneNumber);
                        switch (activtiyType)
                        {
                            case Activity.WorkBasedBusiness:
                            case Activity.Market:
                            case Activity.IndividualOther:
                            case Activity.JointMarket:
                            case Activity.JointOther:
                                {
                                    var choices = LocationChoice.GetLocationProbabilities(episodes[tripIndex]);
                                    var correct = Math.Min(choices[revieldChoice] + 1e-20f, 1.0f);
                                    localFitness += (float)Math.Log(correct);
                                    break;
                                }
                        }
                    }
                }
            }
        }
        else
        {
            var flatChoices = Choices.GetFlatData();
            for (int personIndex = 0; personIndex < persons.Length; personIndex++)
            {
                var tripChains = persons[personIndex].TripChains;
                var expansionFactor = persons[personIndex].ExpansionFactor;
                for (int tcIndex = 0; tcIndex < tripChains.Count; tcIndex++)
                {
                    var trips = tripChains[tcIndex].Trips;
                    IEpisode[] episodes = BuildScheduleFromTrips(trips);
                    for (int tripIndex = 0; tripIndex < trips.Count - 1; tripIndex++)
                    {
                        var activtiyType = episodes[tripIndex].ActivityType;
                        int origin = ZoneSystem.GetFlatIndex(trips[tripIndex].OriginalZone.ZoneNumber);
                        int revieldChoice = ZoneSystem.GetFlatIndex(trips[tripIndex].DestinationZone.ZoneNumber);
                        switch (activtiyType)
                        {
                            case Activity.WorkBasedBusiness:
                            case Activity.Market:
                            case Activity.IndividualOther:
                            case Activity.JointMarket:
                            case Activity.JointOther:
                                {
                                    var choices = LocationChoice.GetLocationProbabilities(episodes[tripIndex]);
                                    var correct = Math.Min(choices[revieldChoice] + 0.001f, 1.0f);
                                    taken = false;
                                    ChoicesLock.Enter(ref taken);
                                    for (int i = 0; i < choices.Length; i++)
                                    {
                                        flatChoices[i][revieldChoice] += choices[i];
                                        switch (activtiyType)
                                        {
                                            case Activity.WorkBasedBusiness:
                                                if (ModelWork != null)
                                                {
                                                    ModelWork[i][revieldChoice] += choices[i];
                                                }
                                                break;
                                            case Activity.Market:
                                            case Activity.JointMarket:
                                                if (ModelMarket != null)
                                                {
                                                    ModelMarket[i][revieldChoice] += choices[i];
                                                }
                                                break;
                                            case Activity.IndividualOther:
                                            case Activity.JointOther:
                                                if (ModelOther != null)
                                                {
                                                    ModelOther[i][revieldChoice] += choices[i];
                                                }
                                                break;
                                        }
                                    }
                                    switch (activtiyType)
                                    {
                                        case Activity.WorkBasedBusiness:
                                            AddIfExists(ObservedWork, origin, revieldChoice);
                                            break;
                                        case Activity.Market:
                                        case Activity.JointMarket:
                                            AddIfExists(ObservedMarket, origin, revieldChoice);
                                            break;
                                        case Activity.IndividualOther:
                                        case Activity.JointOther:
                                            AddIfExists(ObservedOther, origin, revieldChoice);
                                            break;
                                    }
                                    if (taken) ChoicesLock.Exit(false);
                                    localFitness += (float)Math.Log(correct);
                                    break;
                                }
                        }
                    }
                }
            }
        }
        taken = false;
        // evaluate the household
        FitnessLock.Enter(ref taken);
        Fitness += localFitness;
        if (taken) FitnessLock.Exit(true);
    }

    private void AddIfExists(float[][] observedWork, int origin, int revieldChoice)
    {
        if (observedWork != null)
        {
            observedWork[origin][revieldChoice]++;
        }
    }

    private IEpisode[] BuildScheduleFromTrips(List<ITrip> trips)
    {
        ITashaPerson owner = trips[0].TripChain.Person;
        PersonSchedule schedule = new(owner);
        // we don't do the last trip since it is always to home.
        for (int i = 0; i < trips.Count - 1; i++)
        {
            Episode ep = CreateEpisode(trips[i], trips[i + 1], owner);
            ep.Zone = trips[i].DestinationZone;
            schedule.InsertAt(ep, i);
        }
        return schedule.Episodes;
    }

    private Episode CreateEpisode(ITrip trip, ITrip nextTrip, ITashaPerson owner)
    {
        return new ActivityEpisode(new TimeWindow(trip.ActivityStartTime, nextTrip.ActivityStartTime), trip.Purpose, owner);
    }

    public void IterationFinished(int iteration)
    {
        Root.RetrieveValue = () => Fitness;

        if (ConfusionMatrix != null)
        {
            TMG.Functions.SaveData.SaveMatrix(Choices, ConfusionMatrix);
        }
        SaveIfExists(ObservedWork, ObservedWorkOD);
        SaveIfExists(ObservedMarket, ObservedMarketOD);
        SaveIfExists(ObservedOther, ObservedOtherOD);

        SaveIfExists(ModelWork, ModelWorkOD);
        SaveIfExists(ModelMarket, ModelMarketOD);
        SaveIfExists(ModelOther, ModelOtherOD);
    }

    private void SaveIfExists(float[][] observedWork, FileLocation observedWorkOD)
    {
        if (observedWork != null)
        {
            TMG.Functions.SaveData.SaveMatrix(RealRoot.ZoneSystem.ZoneArray.GetFlatData()
                , observedWork, observedWorkOD);
        }
    }

    private float[][] ObservedWork;
    private float[][] ObservedMarket;
    private float[][] ObservedOther;

    private float[][] ModelWork;
    private float[][] ModelMarket;
    private float[][] ModelOther;


    public void IterationStarting(int iteration)
    {
        Fitness = 0.0f;
        if (ConfusionMatrix != null)
        {
            Choices = ((ITravelDemandModel)Root.MainClient).ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
        }
        // reload all of the probabilities
        LocationChoice.LoadLocationChoiceCache();
        ZoneSystem = RealRoot.ZoneSystem.ZoneArray;
        CreateIfObserved(out ObservedWork, ObservedWorkOD);
        CreateIfObserved(out ObservedMarket, ObservedMarketOD);
        CreateIfObserved(out ObservedOther, ObservedOtherOD);

        CreateIfObserved(out ModelWork, ObservedWorkOD);
        CreateIfObserved(out ModelMarket, ObservedMarketOD);
        CreateIfObserved(out ModelOther, ObservedOtherOD);
    }

    private void CreateIfObserved(out float[][] observedDataSet, FileLocation observedDatasetOutputLocation)
    {
        if (observedDatasetOutputLocation == null)
        {
            observedDataSet = null;
        }
        else
        {
            var zones = RealRoot.ZoneSystem.ZoneArray.GetFlatData().Length;
            observedDataSet = new float[zones][];
            for (int i = 0; i < observedDataSet.Length; i++)
            {
                observedDataSet[i] = new float[zones];
            }
        }
    }

    public void Load(int maxIterations)
    {

    }

    private ITravelDemandModel RealRoot;

    public bool RuntimeValidation(ref string error)
    {
        var realRoot = Root.MainClient as ITravelDemandModel;
        if (realRoot == null)
        {
            error = "'" + Name + "' is unable to run because the model system that is being estimated is not based on an ITravelDemandModel!";
            return false;
        }
        RealRoot = realRoot;
        return true;
    }
}
