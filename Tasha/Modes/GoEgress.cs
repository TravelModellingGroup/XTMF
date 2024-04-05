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
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Modes;

/// <summary>
/// GO auto egress mode
/// </summary>
public sealed class GoEgress : ITashaMode
{
    /// <summary>
    ///
    /// </summary>
    [RunParameter( "AutoCost", 0.0f, "The factor applied to the auto cost" )]
    public float AutoCost;

    //constant data for V calculations
    /// <summary>
    ///
    /// </summary>
    [RunParameter( "AutoTime", 0.0f, "The factor applied to the auto travel time" )]
    public float AutoTime;

    //Config Params
    [RunParameter( "CGoEgress", 0.0f, "The constant factor for the GoEgress mode" )]
    public float CDriveEgress;

    /// <summary>
    ///
    /// </summary>
    [Parameter( "FareCost", 0.0f, "The factor applied to the cost of the transit and GO" )]
    public float FareCost;

    [SubModelInformation( Description = "The go data to use", Required = true )]
    public IGoData GOData;

    [RunParameter( "OccGeneralTransit", 0.0f, "The factor applied when their occupation is general." )]
    public float OccGeneralTransit;

    [RunParameter( "OccSalesTransit", 0.0f, "The factor applied when their occupation is sales" )]
    public float OccSalesTransit;

    [RunParameter( "ParkingCost", 0.0f, "The factor applied to the cost of parking" )]
    public float ParkingCost;

    [RunParameter( "PeakTrip", 0.0f, "The factor applied when this is taken during the peak hours" )]
    public float PeakTrip;

    [RootModule]
    public ITashaRuntime TashaRuntime;

    [RunParameter( "TransitRailTime", 0.0f, "The factor applied to the time spent on rail" )]
    public float TransitRailTime;

    /// <summary>
    ///
    /// </summary>
    [RunParameter( "TransitTime", 0.0f, "The factor applied to the transit time" )]
    public float TransitTime;

    [RunParameter( "UnionStation", 0, "The zone number of union station" )]
    public int UnionStation;

    /// <summary>
    ///
    /// </summary>
    [RunParameter( "WaitTime", 0.0f, "The factor applied to the time waiting" )]
    public float WaitTime;

    [SubModelInformation( Description = "The walking model to use", Required = true )]
    public ITashaMode Walking;

    /// <summary>
    ///
    /// </summary>
    /// <summary>
    ///
    /// </summary>
    [RunParameter( "WalkTime", 0.0f, "The factor applied to the walk time" )]
    public float WalkTime;

    [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
    public float CurrentlyFeasible { get; set; }

    [RunParameter( "Name", "Walking", "The name of the mode" )]
    public string ModeName { get; set; }

    /// <summary>
    /// The name of this mode
    /// </summary>
    public string Name
    {
        get;
        set;
    }

    public string NetworkType
    {
        get;
        set;
    }

    /// <summary>
    /// it does require a non-personal vehical
    /// </summary>
    public bool NonPersonalVehicle
    {
        get { return false; }
    }

    [RunParameter( "Observed Mode Character Code", 'A', "The character code used for model estimation." )]
    public char ObservedMode
    {
        get;
        set;
    }

    /// <summary>
    ///
    /// </summary>
    [Parameter( "Observed Signature Code", 'A', "The character code used for model output." )]
    public char OutputSignature
    {
        get;
        set;
    }

    public float Progress
    {
        get { return 0; }
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return new Tuple<byte, byte, byte>( 100, 200, 100 ); }
    }


    /// <summary>
    /// does this mode require a vehicle?
    /// </summary>
    [DoNotAutomate]
    public IVehicleType RequiresVehicle
    {
        get { return TashaRuntime.AutoType; }
    }

    [RunParameter( "Variance Scale", 1.0f, "The scale for varriance used for variance testing." )]
    public double VarianceScale
    {
        get;
        set;
    }

    /// <summary>
    /// Calculate the V for Go Egress
    /// Assumes that there is an access before this egress.
    /// </summary>
    /// <param name="trip">The trip</param>
    /// <returns>The V for this trip</returns>
    public double CalculateV(ITrip trip)
    {

        int egressStation = LatestAccessStation(trip.TripChain, trip, out ITrip accessTrip);
        int accessStation = GOData.GetClosestStations( trip.DestinationZone.ZoneNumber )[0];

        trip.Attach( "go-egress-station", accessStation );

        double v = CDriveEgress;
        if ( trip["Walking"] != null )
        {
            ITrip intermediateTrip = (ITrip)trip["Walking"];
            v += Walking.CalculateV( intermediateTrip );
        }
        else
        {
            v += TransitTime * GOData.GetTransitAccessTime( trip.OriginalZone.ZoneNumber, accessStation );
            v += WalkTime * GOData.GetEgressWalkTime( trip.OriginalZone.ZoneNumber, accessStation );
            v += WaitTime * GOData.GetEgressWaitTime( trip.OriginalZone.ZoneNumber, accessStation );
        }
        v += AutoTime * GOData.GetAutoTime( trip.DestinationZone.ZoneNumber, egressStation );
        v += AutoCost * GOData.GetAutoCost( trip.DestinationZone.ZoneNumber, egressStation );
        v += TransitRailTime * GOData.GetLineHaulTime( accessStation, egressStation );
        v += FareCost * ( GOData.GetGoFair( accessStation, egressStation )
                                          + GOData.GetTransitFair( trip.DestinationZone.ZoneNumber, egressStation ) );

        if ( ( Common.GetTimePeriod( trip.ActivityStartTime ) == TravelTimePeriod.Morning ) ||
( Common.GetTimePeriod( trip.ActivityStartTime ) == TravelTimePeriod.Afternoon ) )
        {
            v += PeakTrip;
        }

        if ( trip.TripChain.Person.Occupation == Occupation.Retail )
        {
            v += OccSalesTransit;
        }

        if ( trip.TripChain.Person.Occupation == Occupation.Office )
        {
            v += OccGeneralTransit;
        }

        return v;
    }

    public float CalculateV(IZone origin, IZone destination, Time time)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Calculating the minumum cost of a go Egress
    /// </summary>
    /// <param name="origin">The origin zone</param>
    /// <param name="destination">The destination zone</param>
    /// <param name="time"></param>
    /// <returns>the minumum cost</returns>
    public float Cost(IZone origin, IZone destination, Time time)
    {
        float minCost = float.MaxValue;

        int[] egressStations = GOData.GetClosestStations( origin.ZoneNumber );
        int accessStation = GOData.GetClosestStations( destination.ZoneNumber )[0];

        foreach ( int egressStation in egressStations )
        {
            float cost = GOData.GetAutoCost( destination.ZoneNumber, egressStation )
                       + GOData.GetGoFair( accessStation, egressStation );

            if ( cost < minCost )
            {
                minCost = cost;
            }
        }

        return minCost;
    }

    public bool Feasible(IZone origin, IZone destination, Time timeOfDay)
    {
        return CurrentlyFeasible > 0;
    }

    /// <summary>
    /// Calculating if an egress trip is feasible
    /// Assuming the go access Calculate V was called before this function
    /// </summary>
    /// <param name="trip">the trip</param>
    /// <returns>is this trip feasible?</returns>
    public bool Feasible(ITrip trip)
    {
        if ( !trip.TripChain.Person.Licence || trip.TripChain.Person.Household.Vehicles.Length == 0 )
            return false;

        if ( trip.OriginalZone.Distance( trip.DestinationZone ) < GOData.MinDistance ) return false;

        int[] accessStations = GOData.GetClosestStations( trip.OriginalZone.ZoneNumber );
        int accessStation = -1;
        int egressStation = LatestAccessStation(trip.TripChain, trip, out ITrip accessTrip);

        if ( egressStation == -1 ) return false; //there was no access prior to this egress

        Time time = Time.Zero;
        foreach ( int station in accessStations )
        {
            if ( ( time = new Time( GOData.GetTransitAccessTime( trip.OriginalZone.ZoneNumber, station ) ) ) > Time.Zero )
            {
                accessStation = station;
            }
        }

        if ( ( accessStation == -1 )  )
        {
            if (Walking == null)
            {
                return false;
            }
            else
            {
                foreach (var station in accessStations)
                {
                    ITransitStation transitStation = GOData.GetStation(station);

                    if (transitStation.ClosestZone == -1) continue;

                    ITrip intermediateTrip = TashaRuntime.CreateTrip(trip.TripChain, trip.OriginalZone, TashaRuntime.ZoneSystem.Get(transitStation.ClosestZone), Activity.Intermediate,
                        trip.ActivityStartTime);

                    if (Walking.Feasible(intermediateTrip))
                    {
                        time = Walking.TravelTime(intermediateTrip.OriginalZone,
                                                  intermediateTrip.DestinationZone,
                                                  intermediateTrip.ActivityStartTime);

                        trip.Attach("Walking", intermediateTrip);

                        accessStation = station;

                        //found a walking trip no need to look further
                        break;
                    }
                }
            }
        }

        if ( time == Time.Zero ) return false;

        Time duration = time;

        //if there was an access before this egress
        if ( egressStation == accessStation
             // ReSharper disable once CompareOfFloatsByEqualityOperator
            || GOData.GetGoFrequency( accessStation, egressStation, trip.ActivityStartTime.ToFloat() ) == 0
            || duration + trip.ActivityStartTime < new Time( GOData.StartTime )
            || duration + trip.ActivityStartTime > new Time( GOData.EndTime )
            )
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checking Feasability of the entire trip chain:
    /// same as Go Access
    /// </summary>
    /// <param name="tripChain">The trips</param>
    /// <returns>is this trip chain feasible?</returns>
    public bool Feasible(ITripChain tripChain)
    {
        bool carIsOut = false;
        foreach ( var trip in tripChain.Trips )
        {
            if ( trip.Mode is GoAccess )
            {
                if ( carIsOut ) return false;
                carIsOut = true;
            }
            else if ( trip.Mode is GoEgress )
            {
                if ( !carIsOut ) return false;
                carIsOut = false;
            }
        }

        return !carIsOut;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="observedMode"></param>
    /// <returns></returns>
    public bool IsObservedMode(char observedMode)
    {
        return ( observedMode == 'G' );
    }

    public void ModeChoiceIterationComplete()
    {
        //nothing
    }

    /// <summary>
    /// This is called before the start method as a way to pre-check that all of the parameters that are selected
    /// are in fact valid for this module.
    /// </summary>
    /// <param name="error">A string that should be assigned a detailed error</param>
    /// <returns>If the validation was successful or if there was a problem</returns>
    public bool RuntimeValidation(ref string error)
    {
        return true;
    }

    /// <summary>
    /// Calculates the minimum travel time for go egress
    ///
    /// </summary>
    ///
    /// <param name="origin">The origin zone</param>
    /// <param name="destination">The destination zone</param>
    /// <param name="time">The time period</param>
    /// <returns>the minimum travel time</returns>
    public Time TravelTime(IZone origin, IZone destination, Time time)
    {
        return Time.Zero;
    }

    /// <summary>
    /// finds the latest access station before this current egress
    /// </summary>
    /// <param name="tripChain">the trip chain of the trip</param>
    /// <param name="trip">the trip</param>
    /// <param name="accessTrip"></param>
    /// <returns>latest access station or -1 if none</returns>
    private int LatestAccessStation(ITripChain tripChain, ITrip trip, out ITrip accessTrip)
    {
        int egressStation = -1;
        accessTrip = null;
        //looking for latest access
        foreach ( ITrip trip2 in tripChain.Trips )
        {
            if ( trip2 == trip ) break;

            if ( trip2.GetVariable( "go-access-station" ) != null )
            {
                egressStation = (int)trip2.GetVariable( "go-access-station" );
                accessTrip = trip2;
            }
        }

        return egressStation;
    }
}