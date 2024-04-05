/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections.Concurrent;
using System.IO;
using TMG.Input;
using Tasha.Common;
using XTMF;

namespace Tasha.Validation.PerformanceMeasures;

public class TripChainMeasure : IPostHousehold
{
    [RootModule]        
    public ITashaRuntime Root;

    [RunParameter("Expanded Trips?", true, "Did you want to look at expanded trips (false = number of non-expanded trips")]
    public bool ExpandedTrips;

    [SubModelInformation(Required = true, Description = "Where do you want to save the Purpose Results. Must be in .CSV format.")]
    public FileLocation TripChainResults;

    [RunParameter("Max trip chain length", 5, "The maximum trip chain length to analyze (anything over this length will be aggregated under the same bin)")]
    public int MaxTripChainLength;

    private ConcurrentDictionary<int,float> ResultsDictionary = new();

    public void Execute(ITashaHousehold household, int iteration)
    {
        float expFactor;
        if (iteration == Root.TotalIterations - 1)
        {
            foreach (var person in household.Persons)
            {
                if (ExpandedTrips)
                {
                    expFactor = household.ExpansionFactor;
                }
                else
                {
                    expFactor = 1.0f;
                }                    

                foreach (var tripChain in person.TripChains)
                {
                    AddToResults(tripChain.Trips.Count, expFactor); 
                }
            }
        } 
    }

    public void AddToResults(int tripChainLength, float expFactor)
    {
        int countBin;
        if (tripChainLength > MaxTripChainLength)
        {
            countBin = MaxTripChainLength;
        }
        else
        {
            countBin = tripChainLength; 
        }
        if (!ResultsDictionary.TryGetValue(countBin, out float data))
        {
            lock (ResultsDictionary)
            {
                if (!ResultsDictionary.TryGetValue(countBin, out data))
                {
                    data = 0;
                    ResultsDictionary[countBin] = data;
                }
            }
        }
        // we need to lock here in order to make sure we don't have a race condition between the read and write
        lock (this)
        {
            ResultsDictionary[countBin] = data + expFactor;
        }
    }

    public void IterationFinished(int iteration)
    {
        using (StreamWriter writer = new(TripChainResults))
        {
            writer.WriteLine("Trip Chain Length,Number of Trips");
            
            foreach (var pair in ResultsDictionary)
            {
                writer.WriteLine("{0}, {1}", pair.Key, pair.Value);                    
            }
        }
        ResultsDictionary.Clear();
    }

    public void Load(int maxIterations)
    {            
    }

    public void IterationStarting(int iteration)
    {            
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
        get { return new Tuple<byte, byte, byte>(120, 25, 100); }
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }
}
