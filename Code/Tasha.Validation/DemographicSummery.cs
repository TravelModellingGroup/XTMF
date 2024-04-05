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
using System.IO;
using Datastructure;
using Tasha.Common;
using TMG.Input;
using XTMF;

namespace Tasha.Validation;

public class DemographicSummery : IPostHousehold
{
    [RunParameter( "Age Sets", "0-10,11-15,16-18,19-25,26-30,31-100", typeof( RangeSet ), "The different age categories to break the population into." )]
    public RangeSet AgeSets;

    [SubModelInformation( Required = true, Description = "The name/location of the csv file to save to." )]
    public FileLocation OutputFileName;

    private float[] AgeSetCount;

    private int TotalIterations;

    public string Name { get; set; }

    public float Progress
    {
        get { return 0f; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return null; }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage( "Microsoft.Reliability", "CA2002:DoNotLockOnObjectsWithWeakIdentity" )]
    public void Execute(ITashaHousehold household, int iteration)
    {
        // we only want to process this data on our last iteration
        if ( iteration < TotalIterations - 1 )
        {
            return;
        }
        var expansionFactor = household.ExpansionFactor;
        foreach ( var person in household.Persons )
        {
            var index = AgeSets.IndexOf( person.Age );
            if ( index >= 0 )
            {
                lock ( AgeSetCount )
                {
                    AgeSetCount[index] += expansionFactor;
                }
            }
        }
    }

    public void IterationFinished(int iteration)
    {
        // if we are on the last iteration then process the data
        if ( iteration < TotalIterations - 1 )
        {
            return;
        }
        using StreamWriter writer = new(OutputFileName.GetFilePath());
        writer.WriteLine("AgeRange,ExpandedPersons");
        for (int i = 0; i < AgeSets.Count; i++)
        {
            writer.Write(AgeSets[i].Start);
            writer.Write('-');
            writer.Write(AgeSets[i].Stop);
            writer.Write(',');
            writer.WriteLine(AgeSetCount[i]);
        }
    }

    public void Load(int maxIterations)
    {
        TotalIterations = maxIterations;
        AgeSetCount = new float[AgeSets.Count];
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void IterationStarting(int iteration)
    {
    }
}