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

    public class AirportDistributionFitnessFunction : IPostIteration
    {

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        private float[] ObservedDistribution;

        private float[] TotalTrips;

        [RootModule]
        public IEstimationClientModelSystem Root;

        public void Execute(int iterationNumber, int totalIterations)
        {
            LoadInitialData();
            // after all of the truth data has been loaded in we can then compare the two models.
            Root.RetrieveValue = () => ComputeFitness();
        }

        [SubModelInformation(Required = true, Description = "The distribution probabilities from the model.")]
        public IResource DistributionProbabilites;

        private float ComputeFitness()
        {
            var fitness = 0.0;
            var distributionProbability = DistributionProbabilites.AcquireResource<SparseArray<float>>().GetFlatData();
            // we only need the auto probabilities since auto + transit = 1 always.
            for(int i = 0; i < ObservedDistribution.Length; i++)
            {
                var trips = TotalTrips[i];
                if(trips > 0)
                {
                    double cellError;
                    var pModel = distributionProbability[i];
                    var pTruth = ObservedDistribution[i];
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
                    fitness += trips * cellError;
                }
            }
            return (float)fitness;
        }

        private void LoadInitialData()
        {
            // do a quick test to make sure that we haven't been loaded before
            if(ObservedDistribution == null)
            {
                // if we are here then we haven't loaded before
                var travelDemandRoot = (ITravelDemandModel)Root.MainClient;
                var zoneSystem = travelDemandRoot.ZoneSystem.ZoneArray;
                LoadProbabilities(zoneSystem);
            }
        }

        [SubModelInformation(Required = true, Description = "The location of the observed distribution data.")]
        public FileLocation ObservedDistributionFile;

        private void LoadProbabilities(SparseArray<IZone> zoneSystem)
        {
            var zones = zoneSystem.GetFlatData();
            ObservedDistribution = new float[zones.Length];
            TotalTrips = new float[zones.Length];
            using (CsvReader reader = new CsvReader(ObservedDistributionFile))
            {
                // burn header
                reader.LoadLine();
                // read in the rest of the data
                int columns;
                while(reader.LoadLine(out columns))
                {
                    if(columns >= 2)
                    {
                        int zone;
                        reader.Get(out zone, 0);
                        zone = zoneSystem.GetFlatIndex(zone);
                        if(zone >= 0)
                        {
                            float probability, totalTrips;
                            reader.Get(out probability, 1);
                            reader.Get(out totalTrips, 2);
                            ObservedDistribution[zone] = probability;
                            TotalTrips[zone] = totalTrips;
                        }
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
