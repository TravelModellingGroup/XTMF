/*
    Copyright 2014 James Vaughan for integration into XTMF.

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
using Datastructure;
using XTMF;
using TMG;
using TMG.Input;
using TMG.Estimation;
using Tasha.Common;
namespace Tasha.Estimation.V4AirportModel2014
{

    public class AiportModeSplitFitnessFunction : IPostIteration
    {

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [RootModule]
        public IEstimationClientModelSystem Root;

        private float[] AutoProbabilities;

        private float[] TotalTrips;

        private float[] TransitProbabilities;

        public void Execute(int iterationNumber, int totalIterations)
        {
            LoadInitialData();
            // after all of the truth data has been loaded in we can then compare the two models.
            Root.RetrieveValue = () => ComputeFitness();
        }

        private void LoadInitialData()
        {
            // do a quick test to make sure that we haven't been loaded before
            if(AutoProbabilities == null)
            {
                // if we are here then we haven't loaded before
                var travelDemandRoot = (ITravelDemandModel)Root.MainClient;
                var zoneSystem = travelDemandRoot.ZoneSystem.ZoneArray;
                LoadProbabilities(zoneSystem);
            }
        }

        [SubModelInformation(Required = true, Description = "The auto probabilities from the model.")]
        public IResource AutoProbabilites;

        [RunParameter("Minimum Fitness", -10.0f, "The minimum fitness a zone can give.")]
        public float MinimumFitness;

        private float ComputeFitness()
        {
            var fitness = 0.0;
            var auto = AutoProbabilites.AcquireResource<SparseArray<float>>().GetFlatData();
            // we only need the auto probabilities since auto + transit = 1 always.
            for(int i = 0; i < AutoProbabilities.Length; i++)
            {
                var observedTrips = TotalTrips[i];
                var pModel = auto[i];
                var pTruth = AutoProbabilities[i];
                if(observedTrips > 0 & pTruth > 0)
                {
                    double cellError;
                    if(pModel > pTruth)
                    {
                        // y - deltaXY <=> 2y-x
                        cellError = pTruth * Math.Log(Math.Min((Math.Max((pTruth + pTruth - pModel), 0)
                            + (pTruth * 0.0015)) / (pTruth * 1.0015), 1));
                    }
                    else
                    {
                        cellError = pTruth * Math.Log(Math.Min((pModel + (pTruth * 0.0015)) / (pTruth * 1.0015), 1));
                    }
                    fitness += cellError;
                }
            }
            return (float)fitness;
        }

        [SubModelInformation(Required = true, Description = "The location to load the mode split truth data from.")]
        public FileLocation ModeSplitTruthData;

        /// <summary>
        /// Load the probabilities from file
        /// </summary>
        /// <param name="zoneSystem">The zone system the model is using</param>
        private void LoadProbabilities(SparseArray<IZone> zoneSystem)
        {
            var zones = zoneSystem.GetFlatData();
            AutoProbabilities = new float[zones.Length];
            TransitProbabilities = new float[zones.Length];
            TotalTrips = new float[zones.Length];
            using CsvReader reader = new(ModeSplitTruthData);
            // burn header
            reader.LoadLine();
            // read in the rest of the data
            while (reader.LoadLine(out int columns))
            {
                if (columns >= 3)
                {
                    reader.Get(out int zone, 0);
                    zone = zoneSystem.GetFlatIndex(zone);
                    if (zone >= 0)
                    {
                        reader.Get(out float auto, 1);
                        reader.Get(out float transit, 2);
                        reader.Get(out float totalTrips, 3);
                        AutoProbabilities[zone] = auto;
                        TransitProbabilities[zone] = transit;
                        TotalTrips[zone] = totalTrips;
                    }
                }
            }
        }

        public void Load(IConfiguration config, int totalIterations)
        {

        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }

}
