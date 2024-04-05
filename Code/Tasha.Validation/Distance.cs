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
using System.IO;
using Tasha.Common;
using TMG.Input;
using XTMF;

namespace Tasha.Validation;

[ModuleInformation(
    Description = "This module is used to validate the Trip Distance of the TASHA results. " +
                    "As an input, it takes in the currently scheduled households and uses " +
                    "the trip chains of each member and calculates the Manhattan distance " +
                    "between the origin and destination of each trip. Finally, it outputs a file which " +
                    "has the frequency of all distances traveled."
    )]
public class Distance : IPostHousehold
{
    [SubModelInformation(Required = true, Description = "The location to save the validation data.")]
    public FileLocation OutputFile;


    [RootModule]
    public ITashaRuntime Root;

    private class DistanceCount
    {
        internal float TotalDistance;
        internal int Records;
        public DistanceCount(float totalDistance, int records)
        {
            TotalDistance = totalDistance;
            Records = records;
        }
    }

    private Dictionary<Activity, DistanceCount> DistancesDictionary = [];

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
        get { return new Tuple<byte, byte, byte>( 100, 100, 100 ); }
    }

    public void Execute(ITashaHousehold household, int iteration)
    {
        // only run on the last iteration
        if ( iteration == Root.TotalIterations - 1 )
        {
            lock (this)
            {
                foreach ( var person in household.Persons )
                {
                    foreach ( var tripChain in person.TripChains )
                    {
                        foreach ( var trip in tripChain.Trips )
                        {
                            float currentDistance = 0;
                            if ( trip.Mode == null )
                            {
                                continue;
                            }

                            if ( trip.OriginalZone == trip.DestinationZone )
                            {
                                currentDistance += trip.OriginalZone.InternalDistance;
                            }
                            else
                            {
                                currentDistance += ( Math.Abs( trip.OriginalZone.X - trip.DestinationZone.X ) + Math.Abs( trip.OriginalZone.Y - trip.DestinationZone.Y ) );
                            }

                            if ( DistancesDictionary.ContainsKey( trip.Purpose ) )
                            {
                                var record = DistancesDictionary[trip.Purpose];
                                record.TotalDistance += currentDistance * 0.001f;
                                record.Records++;
                            }
                            else
                            {
                                DistancesDictionary.Add( trip.Purpose, new DistanceCount( currentDistance * 0.001f, 1 ) );
                            }
                        }
                    }
                }
            }
        }
    }

    public void IterationFinished(int iteration)
    {
        if ( iteration == Root.TotalIterations - 1 )
        {
            using StreamWriter writer = new(OutputFile, true);
            writer.WriteLine("Iteration,Activity,AverageDistance");
            lock (this)
            {
                foreach (var pair in DistancesDictionary)
                {
                    float averageDistance = pair.Value.TotalDistance / pair.Value.Records;
                    writer.WriteLine("{2}, {0}, {1}", pair.Key, averageDistance, iteration);
                }
            }
        }
    }

    public void Load(int maxIterations)
    {
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void IterationStarting(int iteration)
    {
    }
}