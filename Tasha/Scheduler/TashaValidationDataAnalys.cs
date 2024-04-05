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
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Scheduler;

public class TashaValidationDataAnalys : ITashaRuntime
{
    public static int MaxFrequency;

    public static int NumberOfAdultDistributions;

    public static int NumberOfAdultFrequencies;

    [RunParameter( "Max Frequency", 10, "The highest frequency number." )]
    public int MaxFrequencyLocal;

    [RunParameter( "NumberOfAdultDistributions", 6, "The total number of distributions for adults." )]
    public int NumberOfAdultDistributionsLocal;

    [RunParameter( "NumberOfAdultFrequencies", 9, "The total number of frequencies for adults." )]
    public int NumberOfAdultFrequenciesLocal;

    [RunParameter( "#OfDistributions", 262, "The number of distributions" )]
    public int NumberOfDistributionsLocal;

    [RunParameter( "Estimated Households", 148112, "A Guess at the number of households (for progress)" )]
    public int NumberOfHouseholds;

    [RunParameter( "Output Files", "ResultsFileHere.csv", "The Output File" )]
    public string OutputResults;

    [RunParameter( "Start Time Quantums", 96, "The number of different discreet time options" )]
    public int StartTimeQuantums;

    private float CompletedIterationPercentage;

    private int CurrentHousehold;

    private float IterationPercentage;

    [DoNotAutomate]
    public List<ITashaMode> AllModes { get; set; }

    [DoNotAutomate]
    public ITashaMode AutoMode { get; set; }

    [SubModelInformation( Description = "The type of vehicle that auto is", Required = true )]
    public IVehicleType AutoType { get; set; }

    [RunParameter( "End of Day", "28:00", typeof( Time ), "The time that Tasha will end at." )]
    public Time EndOfDay { get; set; }

    [SubModelInformation( Description = "The model that will load our household", Required = true )]
    public IDataLoader<ITashaHousehold> HouseholdLoader { get; set; }

    [RunParameter( "Input Directory", "../../Input", "The directory that the input files will be in." )]
    public string InputBaseDirectory { get; set; }

    [RunParameter( "Number of Iterations", 1, "How many iterations do you want?" )]
    public int TotalIterations { get; set; }

    [DoNotAutomate]
    public ITashaModeChoice ModeChoice { get; set; }

    public string Name
    {
        get;
        set;
    }

    [DoNotAutomate]
    public IList<INetworkData> NetworkData { get; set; }

    [DoNotAutomate]
    public List<ITashaMode> NonSharedModes { get; set; }

    [DoNotAutomate]
    public List<ITashaMode> OtherModes { get; set; }

    public string OutputBaseDirectory { get; set; }

    [RunParameter( "Parallel", false, "Should we run in Parallel?" )]
    public bool Parallel { get; set; }

    [SubModelInformation( Description = "PostHousehold List of Modules.", Required = false )]
    public List<IPostHousehold> PostHousehold { get; set; }

    [DoNotAutomate]
    public List<IPostIteration> PostIteration { get; set; }

    [DoNotAutomate]
    public List<ISelfContainedModule> PostRun { get; set; }

    [SubModelInformation( Description = "PostScheduler List. ", Required = false )]
    public List<IPostScheduler> PostScheduler { get; set; }

    [DoNotAutomate]
    public List<IPreIteration> PreIteration { get; set; }

    [DoNotAutomate]
    public List<ISelfContainedModule> PreRun { get; set; }

    public float Progress
    {
        get;
        set;
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return new Tuple<byte, byte, byte>( 50, 100, 50 ); }
    }

    [RunParameter( "Random Seed", 12345, "The seed for the random number generator" )]
    public int RandomSeed { get; set; }

    [SubModelInformation( Description = "The available resources for this model system.", Required = false )]
    public List<IResource> Resources { get; set; }

    [DoNotAutomate]
    public List<ISharedMode> SharedModes { get; set; }

    [RunParameter( "Start of Day", "4:00", typeof( Time ), "The time that Tasha will start at." )]
    public Time StartOfDay { get; set; }

    [DoNotAutomate]
    public List<IVehicleType> VehicleTypes { get; set; }

    [SubModelInformation( Description = "Zone System", Required = true )]
    public IZoneSystem ZoneSystem { get; set; }

    public ITrip CreateTrip(ITripChain chain, IZone originalZone, IZone destinationZone, Activity purpose, Time startTime)
    {
        throw new NotImplementedException();
    }

    public bool ExitRequest()
    {
        return false;
    }

    public int GetIndexOfMode(ITashaMode mode)
    {
        throw new NotImplementedException();
    }

    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    public void Start()
    {
        ZoneSystem.LoadData();

        if ( PostHousehold != null )
        {
            foreach ( var module in PostHousehold )
            {
                module.Load( TotalIterations );
            }
        }

        IterationPercentage = 1f / TotalIterations;
        //if (this.Scheduler != null)
        //{
        //this.Scheduler.LoadOneTimeLocalData();
        //}

        for ( int i = 0; i < TotalIterations; i++ )
        {
            CurrentHousehold = 0;
            CompletedIterationPercentage = i * IterationPercentage;
            HouseholdLoader.LoadData();
            RunIteration( i );
        }

        if ( PostRun != null )
        {
            foreach ( var module in PostRun )
            {
                module.Start();
            }
        }
        ZoneSystem.UnloadData();
    }

    private void Run(int i, ITashaHousehold household)
    {
        foreach ( var module in PostHousehold )
        {
            module.Execute( household, i );
        }
        System.Threading.Interlocked.Increment( ref CurrentHousehold );
        Progress = ( (float)CurrentHousehold / NumberOfHouseholds ) / TotalIterations + CompletedIterationPercentage;
        household.Recycle();
    }

    private void RunIteration(int i)
    {
        if ( NetworkData != null )
        {
            try
            {
                System.Threading.Tasks.Parallel.ForEach( NetworkData,
                    delegate(INetworkData network)
                    {
                        network.LoadData();
                    } );
            }
            catch ( AggregateException e )
            {
                if ( e.InnerException != null )
                {
                    throw e.InnerException;
                }
            }
        }

        if ( PostScheduler != null )
        {
            foreach ( var module in PostScheduler )
            {
                module.IterationStarting( i );
            }
        }

        if ( PostHousehold != null )
        {
            foreach ( var module in PostHousehold )
            {
                module.IterationStarting( i );
            }
        }

        if ( Parallel )
        {
            RunParallel( i );
        }

        else
        {
            RunSerial( i );
        }

        if ( NetworkData != null )
        {
            foreach ( var network in NetworkData )
            {
                network.UnloadData();
            }
        }

        if ( PostScheduler != null )
        {
            foreach ( var module in PostScheduler )
            {
                module.IterationFinished( i );
            }
        }

        if ( PostHousehold != null )
        {
            foreach ( var module in PostHousehold )
            {
                module.IterationFinished( i );
            }
        }
        HouseholdLoader.Reset();
    }

    private void RunParallel(int iteration)
    {
        var hhlds = HouseholdLoader.ToArray();
        System.Threading.Tasks.Parallel.For( 0, hhlds.Length,
           delegate(int i)
           {
               ITashaHousehold hhld = hhlds[i];
               Run( iteration, hhld );
           }
         );
    }

    private void RunSerial(int iteration)
    {
        var households = HouseholdLoader.ToArray();
        for ( int i = 0; i < households.Length; i++ )
        {
            ITashaHousehold household = households[i];
            Run( iteration, household );
        }
    }
}