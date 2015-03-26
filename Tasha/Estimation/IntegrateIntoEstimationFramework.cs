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
using Tasha.Common;
using TMG;
using TMG.Estimation;
using Tasha.Internal;
using Tasha.XTMFModeChoice;
using System.Threading;

namespace Tasha.Estimation
{
    public class IntegrateIntoEstimationFramework : IPostHousehold
    {
        [RunParameter("Observed Mode Tag", "ObservedMode", "The name of the data to lookup to get the observed mode.")]
        public string ObservedMode;

        [RunParameter("Household Iterations", 1, "The number of iterations done for each household.  This will need to"
            + " be the same as in mode choice!")]
        public int HouseholdIterations;

        [RootModule]
        public IEstimationClientModelSystem Root;

        private ITashaRuntime OurTasha;

        private float Fitness;

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        private SpinLock FitnessUpdateLock = new SpinLock(false);

        public void Execute(ITashaHousehold household, int iteration)
        {
            var householdFitness = (float)EvaluateHousehold(household);
            bool taken = false;
            FitnessUpdateLock.Enter(ref taken);
            Thread.MemoryBarrier();
            Fitness += householdFitness;
            if(taken) FitnessUpdateLock.Exit(true);
        }

        public void IterationFinished(int iteration)
        {

        }

        public void Load(int maxIterations)
        {
            Root.RetrieveValue = () => Fitness;
        }

        public bool RuntimeValidation(ref string error)
        {
            OurTasha = Root.MainClient as ITashaRuntime;
            if(OurTasha == null)
            {
                error = "In '" + Name + "' the estimation's client model system is not an ITashaRuntime!";
                return false;
            }
            return true;
        }

        public void IterationStarting(int iteration)
        {
            Fitness = 0f;
        }

        private double EvaluateHousehold(ITashaHousehold household)
        {
            double fitness = 0;
            foreach(var p in household.Persons)
            {
                foreach(var chain in p.TripChains)
                {
                    foreach(var trip in chain.Trips)
                    {
                        var value = Math.Log((EvaluateTrip(trip) + 1.0) / (HouseholdIterations + 1.0));
                        Array.Clear(trip.ModesChosen, 0, trip.ModesChosen.Length);
                        fitness += value;
                    }
                    chain.Release();
                }
                p.Release();
            }
            household.Release();
            return fitness;
        }

        private double EvaluateTrip(ITrip trip)
        {
            int correct = 0;
            var observedMode = trip[ObservedMode];
            foreach(var choice in trip.ModesChosen)
            {
                if(choice == observedMode)
                {
                    correct++;
                }
            }
            return correct;
        }
    }
}
