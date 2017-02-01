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
using System.Threading.Tasks;
using Datastructure;
using TMG.Functions;
using XTMF;

namespace TMG.GTAModel
{
    [ModuleInformation(Description = "<b>For test purposes only!</b><p>This code is designed to test the GPU gravity model.</p>")]
    public class DemographicDistribution : IDemographicDistribution
    {
        [RunParameter("Beta", 1f, "The correlation between the different options.")]
        public float Beta;

        [RunParameter("Accuracy Epsilon", 0.2f, "The epsilon value used for the gravity distribution.")]
        public float Epsilon;

        [RunParameter("Max Iterations", 300, "The maximum number of iterations for computing the Work Location.")]
        public int MaxIterations;

        [RootModule]
        public I4StepModel Root;

        [RunParameter("Simulation Time", "7:00", typeof(Time), "The time of day the simulation will be for.")]
        public Time SimulationTime;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public IEnumerable<SparseTwinIndex<float>> Distribute(IEnumerable<SparseArray<float>> productions, IEnumerable<SparseArray<float>> attractions, IEnumerable<IDemographicCategory> cat)
        {
            var productionEnum = productions.GetEnumerator();
            var attractionEnum = attractions.GetEnumerator();
            var catEnum = cat.GetEnumerator();
            var numberOfZones = Root.ZoneSystem.ZoneArray.GetFlatData().Length;
            var zoneArray = Root.ZoneSystem.ZoneArray;
            var sparseFriction = zoneArray.CreateSquareTwinArray<float>();
            float[][] friction = sparseFriction.GetFlatData();
            var validZones = zoneArray.ValidIndexArray();
            while (productionEnum.MoveNext() && attractionEnum.MoveNext() && catEnum.MoveNext())
            {
                friction = ComputeFriction(zoneArray.GetFlatData(), catEnum.Current, friction);
                yield return new GravityModel(sparseFriction, (p => Progress = p), Epsilon, MaxIterations).ProcessFlow(productionEnum.Current, attractionEnum.Current, validZones);
            }
            friction = null;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private float[][] ComputeFriction(IZone[] zones, IDemographicCategory cat, float[][] friction)
        {
            var numberOfZones = zones.Length;
            var rootModes = Root.Modes;
            var numberOfModes = rootModes.Count;
            var minFrictionInc = (float)Math.Exp(-10);
            // initialize the category so we can compute the friction
            cat.InitializeDemographicCategory();
            Parallel.For(0, numberOfZones, delegate (int i)
           {
               var origin = zones[i];
               int vIndex = i * numberOfZones * numberOfModes;
               for (int j = 0; j < numberOfZones; j++)
               {
                   double logsum = 0f;
                   var destination = zones[j];
                   int feasibleModes = 0;
                   for (int mIndex = 0; mIndex < numberOfModes; mIndex++)
                   {
                       var mode = rootModes[mIndex];
                       if (!mode.Feasible(origin, zones[j], SimulationTime))
                       {
                           vIndex++;
                           continue;
                       }
                       feasibleModes++;
                       var inc = mode.CalculateV(origin, zones[j], SimulationTime);
                       if (float.IsNaN(inc))
                       {
                           continue;
                       }
                       logsum += Math.Exp(inc);
                   }
                   friction[i][j] = (float)Math.Pow(logsum, Beta);
               }
           });
            // Use the Log-Sum from the V's as the impedance function
            return friction;
        }
    }
}