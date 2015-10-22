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
using System.Linq;
using System.Text;
using XTMF;
using TMG;
using Tasha.Common;
using TMG.Estimation;
using System.Threading;
using TMG.Input;
using Datastructure;
using System.IO;
using TMG.Functions;

namespace Tasha.Estimation.AccessStation
{
    public class AccessStationChoiceEstimationModelSystemTemplate : ITashaRuntime
    {
        [RootModule]
        public IEstimationClientModelSystem Root;

        [RunParameter("Input Directory", "../../Input", "The directory that input will be read from.")]
        public string InputBaseDirectory { get; set; }

        public string Name { get; set; }

        [SubModelInformation(Required = false, Description = "The network level data for the model.")]
        public IList<INetworkData> NetworkData { get; set; }

        [SubModelInformation(Required = true, Description = "Loads the tours for estimation.")]
        public IDataLoader<ITripChain> TourLoader;

        [SubModelInformation(Required = false, Description = "Set this to produce a confusion matrix.")]
        public FileLocation ConfusionMatrix;

        public string OutputBaseDirectory { get; set; }

        Func<float> _Progress = () => 0f;
        public float Progress
        {
            get
            {
                return _Progress();
            }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get
            {
                return new Tuple<byte, byte, byte>(50, 150, 50);
            }
        }

        [SubModelInformation(Required = true, Description = "The zone system the model is in.")]
        public IZoneSystem ZoneSystem { get; set; }

        public List<ITashaMode> AllModes { get; set; }

        [DoNotAutomate]
        public ITashaMode AutoMode { get; set; }

        [DoNotAutomate]
        public IVehicleType AutoType { get; set; }
        public Time EndOfDay { get; set; }

        [DoNotAutomate]
        public IDataLoader<ITashaHousehold> HouseholdLoader { get; set; }

        public int TotalIterations { get; set; }

        [DoNotAutomate]
        public ITashaModeChoice ModeChoice { get; set; }

        [DoNotAutomate]
        public List<ITashaMode> NonSharedModes { get; set; }

        [DoNotAutomate]
        public List<ITashaMode> OtherModes { get; set; }

        public bool Parallel { get; set; }

        [DoNotAutomate]
        public List<IPostHousehold> PostHousehold { get; set; }

        [DoNotAutomate]
        public List<IPostIteration> PostIteration { get; set; }

        [DoNotAutomate]
        public List<ISelfContainedModule> PostRun { get; set; }

        [DoNotAutomate]
        public List<IPostScheduler> PostScheduler { get; set; }

        [DoNotAutomate]
        public List<IPreIteration> PreIteration { get; set; }

        [DoNotAutomate]
        public List<ISelfContainedModule> PreRun { get; set; }

        public int RandomSeed { get; set; }

        public List<ISharedMode> SharedModes { get; set; }

        public Time StartOfDay { get; set; }

        [DoNotAutomate]
        public List<IVehicleType> VehicleTypes { get; set; }

        public List<IResource> Resources { get; set; }

        public bool ExitRequest()
        {
            return false;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private int Completed;

        private bool FirstLoad = true;

        public void Start()
        {
            if (FirstLoad)
            {
                this.ZoneSystem.LoadData();
                foreach (var network in this.NetworkData)
                {
                    network.LoadData();
                }
                this.TourLoader.LoadData();
                this.FirstLoad = false;
            }
            // the model needs to be loaded every time
            this.AccessStationChoiceModel.Load();
            var tours = this.TourLoader.ToArray();

            float result = this.ComputeError(tours);
            this.Completed = 0;
            this._Progress = () => Completed / tours.Length;
            this.Root.RetrieveValue = () =>
                {
                    return result;
                };
            if (ConfusionMatrix != null)
            {
                ProduceConfusionMatrix(tours);
            }
            this._Progress = () => 1.0f;
        }

        private float ComputeError(ITripChain[] tours)
        {
            return (from tour in tours.AsParallel()
                    select ComputeError(tour)).Sum();
        }

        [Parameter("Access Station Tag", "AccessStation", "The name of the tag to attach to the trip chains that contains the access station zone.")]
        public string AccessStationTag;

        [SubModelInformation(Required = true, Description = "The access station choice model to estimate.")]
        public IAccessStationChoiceModel AccessStationChoiceModel;

        private void ProduceConfusionMatrix(ITripChain[] tours)
        {
            var zoneSystem = ZoneSystem.ZoneArray;
            var zones = zoneSystem.GetFlatData();
            var results = new float[zones.Length * zones.Length];
            for (int t = 0; t < tours.Length; t++)
            {
                var tour = tours[t];
                var observedAccessStation = tour[this.AccessStationTag] as IZone;
                var result = this.AccessStationChoiceModel.ProduceResult(tour);
                var observedOffset = zoneSystem.GetFlatIndex(observedAccessStation.ZoneNumber);
                if (result != null)
                {
                    if (Normalize(result))
                    {
                        for (int i = 0; i < result.First.Length && result.First[i] != null; i++)
                        {
                            var value = result.Second[i];
                            var recallOffset = zoneSystem.GetFlatIndex(result.First[i].ZoneNumber) * zones.Length;
                            results[recallOffset + observedOffset] += value;
                        }
                    }
                }
            }
            TMG.Functions.SaveData.SaveMatrix(zones, results, ConfusionMatrix);
        }

        private bool Normalize(Pair<IZone[], float[]> result)
        {
            var utils = result.Second;
            var total = VectorHelper.IsHardwareAccelerated ? VectorHelper.Sum(utils, 0, utils.Length) : utils.Sum();
            if (total <= 0) return false;
            // convert utilities to probability
            if (VectorHelper.IsHardwareAccelerated)
            {
                VectorHelper.Multiply(utils, 0, utils, 0, 1.0f / total, utils.Length);
            }
            else
            {
                for (int i = 0; i < utils.Length; i++)
                {
                    utils[i] /= total;
                }
            }
            return true;
        }

        private float ComputeError(ITripChain tour)
        {
            var observedAccessStation = tour[this.AccessStationTag] as IZone;
            var result = this.AccessStationChoiceModel.ProduceResult(tour);
            float correct = 0.0f;
            if (result != null)
            {
                var total = 0.0f;
                int correctIndex = -1;
                for (int i = 0; i < result.First.Length; i++)
                {
                    if (result.First[i] == null) break;
                    if (result.First[i] == observedAccessStation) correctIndex = i;
                    var value = result.Second[i];
                    total += result.Second[i];
                }
                if ((total > 0) & (correctIndex >= 0))
                {
                    correct = result.Second[correctIndex] / total;
                }
            }
            // we finished another
            Interlocked.Increment(ref this.Completed);
            var error = 1.0f - correct;
            // use squared error
            return error * error;
        }

        public ITrip CreateTrip(ITripChain chain, IZone originalZone, IZone destinationZone, Activity purpose, Time startTime)
        {
            throw new NotImplementedException();
        }

        public int GetIndexOfMode(ITashaMode mode)
        {
            throw new NotImplementedException();
        }
    }
}
