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
using System.Threading;
using XTMF;

namespace TMG.Estimation.Utilities
{

    public class ReportBestParameterValue : IModelSystemTemplate
    {
        public string InputBaseDirectory
        {
            get; set;
        }

        public string Name { get; set; }

        public string OutputBaseDirectory
        {
            get; set;
        }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        public bool ExitRequest()
        {
            return false;
        }

        bool Attached;

        [RootModule]
        public LocalEstimationHost Root;

        public bool RuntimeValidation(ref string error)
        {
            if(!Attached)
            {
                Root.FitnessFunctionEvaluated += Root_FitnessFunctionEvaluated;
                Response.Root.RetrieveValue = () => GetBestFitness();
                Attached = true;
            }
            return true;
        }

        private float GetBestFitness()
        {
            bool taken = false;
            FitnessLock.Enter(ref taken);
            var ret = BestFitness;
            FitnessLock.Exit(true);
            return ret;
        }

        private void Root_FitnessFunctionEvaluated(Job job, int gen, float fitness)
        {
            bool taken = false;
            if(Maximize)
            {
                FitnessLock.Enter(ref taken);
                if(BestFitness < fitness)
                {
                    BestFitness = fitness;
                }
                FitnessLock.Exit(true);
            }
            else
            {
                FitnessLock.Enter(ref taken);
                if(BestFitness > fitness)
                {
                    BestFitness = fitness;
                }
                FitnessLock.Exit(true);
            }
        }

        [RunParameter("Maximize", false, "Find the max or min value.")]
        public bool Maximize;

        private SpinLock FitnessLock = new SpinLock(false);
        private float BestFitness;

        public void Start()
        {
            if(Maximize)
            {
                BestFitness = float.MinValue;
            }
            else
            {
                BestFitness = float.MaxValue;
            }
        }

        [SubModelInformation(Required = true, Description = "Used to respond to the host.")]
        public RespondToHost Response;

        public class RespondToHost : IModule
        {
            [RootModule]
            public IEstimationClientModelSystem Root;

            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

    }

}
