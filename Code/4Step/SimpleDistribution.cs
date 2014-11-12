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
using TMG;
using XTMF;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Datastructure;
using TMG.Functions;

namespace James.UTDM
{
    public class SimpleDistribution : IDistribution
    {
        [RootModule]
        public ITravelDemandModel Parent;

        [RunParameter("LinearDistance", -2.4f, "The co-efficient for linear distance for the gravity model.")]
        public float LinearDistanceCoefficient;

        [RunParameter("Convergence Criteria", 0.8f, "The convergence requirement for balancing the flows.")]
        public float Epsilon;

        [RunParameter("Max Balance Iterations", 300, "The max times that balancing will be done for the flows.")]
        public int MaxBalanceIterations;

        [RunParameter("Use GPU", true, "Should we use the GPU to accellerate the calculate? (x64 systems only)")]
        public bool UseGPU;

        SparseArray<float> ZoneX;
        SparseArray<float> ZoneY;

        public SimpleDistribution()
        {

        }

        public SparseTwinIndex<float> Distribute(SparseArray<float> productions, SparseArray<float> attractions)
        {
            var zoneSystem = this.Parent.ZoneSystem;
            var zoneArray = zoneSystem.ZoneArray;
            var flatZones = zoneArray.GetFlatData();
            ZoneX = zoneArray.CreateSimilarArray<float>();
            var flatX = ZoneX.GetFlatData();
            ZoneY = zoneArray.CreateSimilarArray<float>();
            var flatY = ZoneY.GetFlatData();
            var numberOfZones = flatZones.Length;
            for (int i = 0; i < numberOfZones; i++)
            {
                var zone = flatZones[i];
                flatX[i] = zone.X;
                flatY[i] = zone.Y;
            }
            SparseTwinIndex<float> data;
            if (this.UseGPU)
            {
                float[] friction = this.ComputeFriction(flatX, flatY, flatZones);
                data = new GPUGravityModel( friction, UpdateProgress, this.Epsilon, this.MaxBalanceIterations ).ProcessFlow( productions, attractions );
            }
            else
            {
                var validZones = zoneArray.ValidIndexies().ToArray();
                data = new GravityModel(FrictionFunction, UpdateProgress, this.Epsilon, this.MaxBalanceIterations).ProcessFlow(productions, attractions, validZones);
            }
            this.Progress = 1;
            return data;
        }

        private float[] ComputeFriction(float[] x, float[] y, IZone[] zones)
        {
            var numberOfZones = zones.Length;
            float[] ret = new float[numberOfZones * numberOfZones];
            int index = 0;
            Parallel.For( 0, numberOfZones, delegate(int i)
            {
                for ( int j = 0; j < numberOfZones; j++ )
                {
                    var blockDistance = Math.Abs( x[i] - x[j] ) + Math.Abs( y[i] - y[j] );
                    ret[index++] = (float)Math.Exp( this.LinearDistanceCoefficient * ( blockDistance / 1000 ) );
                }
            } );
            return ret;
        }

        private double FrictionFunction(int i, int j)
        {
            double blockDistance = Math.Abs((ZoneX[i] - ZoneX[j])) + Math.Abs(ZoneY[i] - ZoneY[j]);
            // convert the distance into km's before hitting the exponential
            return (float)Math.Exp(this.LinearDistanceCoefficient * (blockDistance / 1000));
        }

        private void UpdateProgress(float progress)
        {
            this.Progress = progress;
        }


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

        public bool RuntimeValidation(ref string error)
        {
            if (Epsilon <= 0)
            {
                error = "The Convergence Criteria must be greater than 0!";
                return false;
            }
            else if (this.MaxBalanceIterations <= 0)
            {
                error = "The max number of balance iterations must be greater than zero.";
                return false;
            }
            else if (this.LinearDistanceCoefficient >= 0)
            {
                error = "Linear Distance co-efficient must be less than zero!";
                return false;
            }
            return true;
        }
    }
}
