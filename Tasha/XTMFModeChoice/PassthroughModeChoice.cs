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
using System.Threading.Tasks;
using Tasha.Common;
using XTMF;

namespace Tasha.XTMFModeChoice;

[ModuleInformation(Description = "A mode choice model that simply passes through the chosen modes without modification.")]
public sealed class PassthroughModeChoice : ITashaModeChoice
{

    [RunParameter("Parallel Household Iteration", true, "Should we run all of the household iteration modules in parallel?")]
    public bool ParallelHouseholdIteration;

    [RunParameter("Household Iterations", 10, "The number of times that the mode choice should process a household.")]
    public int HouseholdIterations;

    [SubModelInformation(Required = false, Description = "The modules used for processing in between household iterations.")]
    public IPostHouseholdIteration[] PostHouseholdIteration;

    public void LoadOneTimeLocalData()
    {

    }

    public void IterationStarted(int iteration, int totalIterations)
    {
        if (ParallelHouseholdIteration)
        {
            Parallel.ForEach(PostHouseholdIteration, module =>
            {
                module.IterationStarting(iteration, totalIterations);
            });
        }
        else
        {
            foreach (var module in PostHouseholdIteration)
            {
                module.IterationStarting(iteration, totalIterations);
            }
        }
    }

    public bool Run(ITashaHousehold household)
    {
        foreach (var module in PostHouseholdIteration)
        {
            module.HouseholdStart(household, HouseholdIterations);
        }

        for (int i = 0; i < HouseholdIterations; i++)
        {
            // Setup the household for the iteration
            foreach (var person in household.Persons)
            {
                foreach (var tripChain in person.TripChains)
                {
                    foreach (var trip in tripChain.Trips)
                    {
                        var mode = trip.ModesChosen[i]; ;
                        if(mode is null)
                        {
                            throw new Exception($"Trip {trip.TripNumber} for person {person.Id} in household {household.HouseholdId} does not have a chosen mode for iteration {i}.");
                        }
                        trip.Mode = mode;
                    }
                }
            }
            foreach (var module in PostHouseholdIteration)
            {
                module.HouseholdIterationComplete(household, i, HouseholdIterations);
            }
        }

        foreach (var module in PostHouseholdIteration)
        {
            module.HouseholdComplete(household, true);
        }
        return true;
    }

    public void IterationFinished(int iteration, int totalIterations)
    {
        if (ParallelHouseholdIteration)
        {
            Parallel.ForEach(PostHouseholdIteration, module =>
            {
                module.IterationFinished(iteration, totalIterations);
            });
        }
        else
        {
            foreach (var module in PostHouseholdIteration)
            {
                module.IterationFinished(iteration, totalIterations);
            }
        }
    }

    public string Name { get; set; }

    public float Progress => 0f;

    public Tuple<byte, byte, byte> ProgressColour => new(50, 150, 50);

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
