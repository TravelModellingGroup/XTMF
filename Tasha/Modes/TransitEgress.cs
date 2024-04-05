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
///
/// </summary>
public sealed class TransitEgress : ITashaMode
{
    [RunParameter( "AutoCost", 0, "The weight term for auto cost." )]
    public float AutoCost;

    [RunParameter( "AutoTime", 0, "The weight term for auto time." )]
    public float AutoTime;

    //Config Params
    [RunParameter( "CDriveEgress", 0, "The constant term for transit egress." )]
    public float CDriveEgress;

    [RunParameter( "CostParam", 0, "The weight term for the cost." )]
    public float CostParam;

    [RunParameter( "Min Distance", 0, "The smallest distance required before this mode can be used" )]
    public float MinDistance;

    [RunParameter( "OccGeneralTransit", 0, "The constant term for transit egress." )]
    public float OccGeneralTransit;

    [RunParameter( "OccSalesTransit", 0, "The constant term for transit egress." )]
    public float OccSalesTransit;

    [RunParameter( "ParkingCost", 0, "The constant term for transit egress." )]
    public float ParkingCost;

    [RunParameter( "ParkingSpace", 0, "The weight term for the parking space." )]
    public float ParkingSpace;

    [RunParameter( "PeakTrip", 0, "The constant term for transit egress." )]
    public float PeakTrip;

    [SubModelInformation( Description = "The data used for Transit Egress", Required = true )]
    public ITripComponentData TransitAccessData;

    [RunParameter( "TransitTime", 0, "The weight term for transit time." )]
    public float TransitTime;

    [RunParameter( "WaitTime", 0, "The weight term for the wait time." )]
    public float WaitTime;

    [RunParameter( "WalkTime", 0, "The weight term for the walking time" )]
    public float WalkTime;

    [DoNotAutomate]
    private IVehicleType AutoType;

    [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
    public float CurrentlyFeasible { get; set; }

    [RunParameter( "Name", "Walking", "The name of the mode" )]
    public string ModeName { get; set; }

    /// <summary>
    /// Gets the name of the mode
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
    /// Gets if this mode does not use a personal vehical
    /// </summary>
    [DoNotAutomate]
    public bool NonPersonalVehicle
    {
        get { return false; }
    }

    public char ObservedMode
    {
        get;
        set;
    }

    /// <summary>
    ///
    /// </summary>
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
    /// Which Vehical [if any] does this mode require
    /// </summary>
    public IVehicleType RequiresVehicle
    {
        get { return AutoType; }
    }

    [RootModule]
    public ITashaRuntime TashaRuntime { get; set; }

    public double VarianceScale
    {
        get;
        set;
    }

    /// <summary>
    /// Calculates the V for a given trip
    ///
    /// Returns a 'random' V
    ///
    ///
    /// </summary>
    /// <param name="trip">The trip to calculate V for</param>
    /// <returns>The V for this trip</returns>
    public double CalculateV(ITrip trip)
    {
        int egressStation = (int)trip["AccessStation"];
        //LatestAccessStation(trip.TripChain, trip);
        var auto = TashaRuntime.AutoMode;
        var station = TashaRuntime.ZoneSystem.Get( egressStation );
        float v = CDriveEgress;
        v += AutoTime * auto.TravelTime( trip.DestinationZone, station, trip.TripStartTime ).ToMinutes();
        v += WalkTime * TransitAccessData.WalkTime( station, trip.OriginalZone, trip.TripStartTime ).ToMinutes();
        v += WaitTime * TransitAccessData.WaitTime( station, trip.OriginalZone, trip.TripStartTime ).ToMinutes();
        v += AutoCost * auto.Cost( trip.DestinationZone, station, trip.TripStartTime );
        v += TransitTime * TransitAccessData.InVehicleTravelTime( station, trip.OriginalZone, trip.TripStartTime ).ToMinutes();
        v += ParkingCost * TransitAccessData.Station( station ).ParkingCost;
        if ( ( Common.GetTimePeriod( trip.ActivityStartTime ) == TravelTimePeriod.Morning ) ||
                ( Common.GetTimePeriod( trip.ActivityStartTime ) == TravelTimePeriod.Afternoon ) )
        {
            v += PeakTrip;
        }
        if ( trip.TripChain.Person.Occupation == Occupation.Retail )
        {
            v += OccSalesTransit;
        }
        else if ( trip.TripChain.Person.Occupation == Occupation.Office )
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
    /// Getting the lowest cost possible for this particular trip
    /// </summary>
    /// <param name="origin">The origin of the trip</param>
    /// <param name="destination">The destination of the trip</param>
    /// <param name="time"></param>
    /// <returns>The lowest cost</returns>
    public float Cost(IZone origin, IZone destination, Time time)
    {
        int[] egressStations = TransitAccessData.ClosestStations( destination );

        float minCost = float.MaxValue;
        var auto = TashaRuntime.AutoMode;
        for ( int i = 0; i < egressStations.Length; i++ )
        {
            var station = TashaRuntime.ZoneSystem.Get( egressStations[i] );
            float cost = auto.Cost( destination, station, time )
                                + TransitAccessData.Station( station ).ParkingCost;

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
    /// Checking that the person has a licence and a vehical
    /// and that there exist a station st. the auto travel time and
    /// transit time is > 0
    /// </summary>
    /// <param name="trip">The trip to test feasibility on</param>
    /// <returns>if it was feasible</returns>
    public bool Feasible(ITrip trip)
    {
        if ( trip.OriginalZone.Distance( trip.DestinationZone ) < MinDistance ) return false;
        if ( !trip.TripChain.Person.Licence || trip.TripChain.Person.Household.Vehicles.Length == 0 )
        {
            return false;
        }
        int egressStation;
        if ( ( egressStation = LatestAccessStation( trip.TripChain, trip ) ) == -1 ) return false;
        var station = TashaRuntime.ZoneSystem.Get( egressStation );
        var auto = TashaRuntime.AutoMode;
        //checking if there auto travel time is > 0 and the time in transit > 0 : meaning there exists
        //a route from origin to destination through subway station
        if ( auto.TravelTime( trip.DestinationZone, station, trip.TripStartTime ) > Time.Zero
            && TransitAccessData.InVehicleTravelTime( station, trip.OriginalZone, trip.TripStartTime ) > Time.Zero )
        {
            trip.Attach( "AccessStation", egressStation );
            return true;
        }
        return false;
    }

    /// <summary>
    /// Checking Feasibility of the entire trip chain
    /// TODO: Switch this to "Feasible", inform James
    /// </summary>
    /// <param name="tripChain"></param>
    /// <returns></returns>
    public bool Feasible(ITripChain tripChain)
    {
        bool accessOccured = false;
        var trips = tripChain.Trips;
        int carLocation = trips[0].OriginalZone.ZoneNumber;
        //checking if each access has an Egress and car is located at origin of access
        foreach ( ITrip trip in trips )
        {
            var mode = trip.Mode;
            if ( mode is TransitEgress )//if mode is an Egress
            {
                //there was no access before this Egress
                if ( !accessOccured )
                {
                    return false;
                }
                accessOccured = false; //cancel out the previous access with this Egress
            }
            else if ( mode is TransitAccess )
            {
                // if there was an access before this access
                if ( accessOccured | ( trip.OriginalZone.ZoneNumber != carLocation ) )
                {
                    return false;
                }
                accessOccured = true;
            }
            else if ( mode.RequiresVehicle == AutoType )
            {
                carLocation = trip.DestinationZone.ZoneNumber;
            }
        }
        return !accessOccured;
    }

    /// <summary>
    ///
    /// </summary>
    public bool IsObservedMode(char observedMode)
    {
        return ( observedMode == ObservedMode );
    }

    /// <summary>
    ///
    /// </summary>
    public void ModeChoiceIterationComplete()
    {
    }

    /// <summary>
    /// Nothing to do here, data is released by TransitAccess
    /// </summary>
    public void ReleaseData()
    {
        TransitAccessData.UnloadData();
    }

    /// <summary>
    /// The data is loaded by the TransitAccess
    /// </summary>
    public void ReloadNetworkData()
    {
        TransitAccessData.LoadData();
    }

    /// <summary>
    /// This is called before the start method as a way to pre-check that all of the parameters that are selected
    /// are in fact valid for this module.
    /// </summary>
    /// <param name="error">A string that should be assigned a detailed error</param>
    /// <returns>If the validation was successful or if there was a problem</returns>
    public bool RuntimeValidation(ref string error)
    {
        AutoType = TashaRuntime.AutoType;
        return true;
    }

    /// <summary>
    /// Gets the minimum amount of Travel Time for
    /// a Transit Egress by finding the Station
    /// with the lowest travel time
    /// </summary>
    /// <param name="origin">The start zone</param>
    /// <param name="destination">The end zone</param>
    /// <param name="time">The time period = 0</param>
    /// <returns>The lowest travel time among closest stations</returns>
    public Time TravelTime(IZone origin, IZone destination, Time time)
    {
        int[] egressStations = TransitAccessData.ClosestStations( origin );

        Time minTravelTime = Time.EndOfDay;
        var auto = TashaRuntime.AutoMode;
        for ( int i = 0; i < egressStations.Length; i++ )
        {
            var station = TashaRuntime.ZoneSystem.Get( egressStations[i] );
            Time travelTime = auto.TravelTime( destination, station, time )
                                + TransitAccessData.TravelTime( station, origin, time );

            if ( travelTime < minTravelTime )
            {
                minTravelTime = travelTime;
            }
        }

        return minTravelTime;
    }

    private int LatestAccessStation(ITripChain tripChain, ITrip trip)
    {
        int egressStation = -1;

        //looking for latest access
        foreach ( ITrip trip2 in tripChain.Trips )
        {
            if ( trip2 == trip ) break;

            if ( trip2.GetVariable( "subway-access-station" ) != null )
            {
                egressStation = (int)trip2.GetVariable( "subway-access-station" );
            }
        }

        return egressStation;
    }
}