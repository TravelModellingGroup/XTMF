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
/// Provides the ability to ride a bike
/// </summary>
public sealed class Bike : ITashaMode
{
    [RunParameter( "CBike", 0f, "The constant factor applied for the bicycle mode" )]
    public float CBike;

    [RunParameter( "dpurp_oth_drive", 0f, "The weight for the cost of doing an other drive (ITashaRuntime only)" )]
    public float DpurpOthDrive;

    [RunParameter( "dpurp_shop_drive", 0f, "The weight for the cost of doing a shopping drive (ITashaRuntime only)" )]
    public float DpurpShopDrive;

    [RunParameter( "Intrazonal", 0f, "The factor applied for being an intrazonal trip" )]
    public float Intrazonal;

    /// <summary>
    /// The maximum distance one will travel on a bike
    /// </summary>
    [RunParameter( "Max Travel Distance", 12000, "The largest distance a person is allowed to bike (meters)" )]
    public int MaxTravelDistance;

    [RunParameter( "TravelTimeBeta", 0f, "The factor applied for the travel time" )]
    public float TravelTimeBeta;

    [DoNotAutomate]
    public IVehicleType VehicleType = null;

    [RunParameter( "YoungAdult", 0f, "(Tasha Only) The factor applied for being a young adult" )]
    public float YoungAdult;

    [RunParameter( "Youth", 0f, "(Tasha Only) The factor applied for the being a youth" )]
    public float Youth;

    private float AvgTravelSpeed;

    /// <summary>
    /// Avg speed of riding a bike
    /// </summary>
    [RunParameter( "Average Speed", 15.0f, "The average travel speed in KM/H" )]
    public float AvgTravelSpeedInKMperHour
    {
        get
        {
            return AvgTravelSpeed / 1000 * 60; //now m/min
        }

        set
        {
            AvgTravelSpeed = value * 1000 / 60; //now m/min
        }
    }

    [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
    public float CurrentlyFeasible { get; set; }

    [RunParameter( "Name", "Bike", "The name of the mode" )]
    public string ModeName { get; set; }

    /// <summary>
    ///
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
    ///
    /// </summary>
    public bool NonPersonalVehicle
    {
        get { return true; }
    }

    [RunParameter( "Observed Signature Code", 'A', "The character code used for model output." )]
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

    [DoNotAutomate]
    public IVehicleType RequiresVehicle
    {
        get { return VehicleType; }
    }

    [RunParameter( "Variance Scale", 1.0f, "The scale for varriance used for variance testing." )]
    public double VarianceScale
    {
        get;
        set;
    }

    /// <summary>
    /// The V for a trip whose mode is bike
    /// </summary>
    /// <param name="trip">The trip</param>
    /// <returns>The V for this trip</returns>
    public double CalculateV(ITrip trip)
    {
        double v = 0;
        v += CBike;
        v += TravelTimeBeta * TravelTime( trip.OriginalZone, trip.DestinationZone, trip.ActivityStartTime ).ToMinutes();
        if ( trip.TripChain.Person.Youth )
        {
            v += Youth;
        }
        if ( trip.TripChain.Person.YoungAdult )
        {
            v += YoungAdult;
        }
        if ( trip.OriginalZone == trip.DestinationZone )
        {
            v += Intrazonal;
        }
        return v;
    }

    /// <summary>
    /// Calculat ethe utility of moving between and origin and a destination
    /// with a disrgard for the person taking it
    /// </summary>
    /// <param name="origin">Where they start</param>
    /// <param name="destination">where they go</param>
    /// <param name="time">What time the trip started at</param>
    /// <returns>The number of minutes it takes</returns>
    public float CalculateV(IZone origin, IZone destination, Time time)
    {
        float v = 0;
        v += CBike;
        v += TravelTimeBeta * TravelTime( origin, destination, time ).ToMinutes();
        if ( origin.ZoneNumber == destination.ZoneNumber )
        {
            v += Intrazonal;
        }
        return v;
    }

    /// <summary>
    /// Returns the cost of riding a bike
    /// </summary>
    /// <param name="origin">The origin zone</param>
    /// <param name="destination">The destination zone</param>
    /// <param name="time"></param>
    /// <returns>the cost</returns>
    public float Cost(IZone origin, IZone destination, Time time)
    {
        return 0;
    }

    public bool Feasible(IZone origin, IZone destination, Time timeOfDay)
    {
        return CurrentlyFeasible > 0 && origin.Distance( destination ) <= MaxTravelDistance;
    }

    /// <summary>
    /// Checking Bike Feasibility
    /// </summary>
    /// <param name="trip">The Trip</param>
    /// <returns>is it Feasible?</returns>
    public bool Feasible(ITrip trip)
    {
        return Feasible( trip.OriginalZone, trip.DestinationZone, trip.ActivityStartTime );
    }

    /// <summary>
    /// Checking feasibility of entire trip chain for a bike
    /// </summary>
    /// <param name="tripChain">The Trip Chain to test feasibility on</param>
    /// <returns>is this trip chain feasible?</returns>
    public bool Feasible(ITripChain tripChain)
    {
        var trips = tripChain.Trips;
        var numberOfTrips = trips.Count;
        int lastPlace = trips[0].OriginalZone.ZoneNumber;
        int homeZone = lastPlace;
        bool lastMadeWithBike = true;
        bool firstWasBike = trips[0].Mode == this;
        bool anyBike = false;
        for ( int i = 0; i < numberOfTrips; i++ )
        {
            var trip = trips[i];
            if ( trip.Mode == this )
            {
                anyBike = true;
                if ( trip.OriginalZone.ZoneNumber != lastPlace )
                {
                    return false;
                }
                lastPlace = trip.DestinationZone.ZoneNumber;
                lastMadeWithBike = true;
            }
            else
            {
                lastMadeWithBike = false;
            }
        }
        return !anyBike | ( firstWasBike & lastPlace == homeZone & lastMadeWithBike );
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="observedMode"></param>
    /// <returns></returns>
    public bool IsObservedMode(char observedMode)
    {
        return ( observedMode == ObservedMode );
    }

    /// <summary>
    ///
    /// </summary>
    public void ModeChoiceIterationComplete()
    {
        //do nothing
    }

    /// <summary>
    ///
    /// </summary>
    public void ReleaseData()
    {
        //nothing to release
    }

    /// <summary>
    ///
    /// </summary>
    public void ReloadNetworkData()
    {
        //no network data to reload
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
    /// The Travel time from an origin to a zone using a bike
    /// </summary>
    /// <param name="origin">The origin zone</param>
    /// <param name="destination">The destination zone</param>
    /// <param name="time">The time of day</param>
    /// <returns>The travel time</returns>
    public Time TravelTime(IZone origin, IZone destination, Time time)
    {
        double distance = origin == destination ? origin.InternalDistance : origin.Distance( destination );
        Time ret = Time.FromMinutes( (float)( distance / AvgTravelSpeed ) );
        return ret;
    }
}