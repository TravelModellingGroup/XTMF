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
using Datastructure;
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Modes
{
    /// <summary>
    ///
    /// </summary>
    public sealed class Taxi : ITashaMode
    {
        [RunParameter( "AutoNetworkName", "Auto", "The name of the auto network to use." )]
        public string AutoNetworkName;

        //parameters
        [RunParameter( "CTaxi", 0.0f, "The constant weight for the taxi mode" )]
        public float CTaxi;

        [DoNotAutomate]
        public INetworkData data;

        [RunParameter( "dpurp_oth_drive", 0f, "The weight for the cost of doing an other drive (ITashaRuntime only)" )]
        public float dpurp_oth_drive;

        [RunParameter( "dpurp_shop_drive", 0f, "The weight for the cost of doing a shopping drive (ITashaRuntime only)" )]
        public float dpurp_shop_drive;

        [RunParameter( "FareCost", 0.0f, "The weight factor for the cost" )]
        public float FareCost;

        [RunParameter( "from_to_transport_terminal", 0f, "The weight for the cost of doing a from/to transport terminal drive (ITashaRuntime only)" )]
        public float from_to_transport_terminal;

        // constants from taxi directory in config file
        [RunParameter( "InitialFare", 2.75f, "The weight for the initial cost of the taxi" )]
        public float InitialFare;

        [RunParameter( "Intrazonal Constant", 0f, "The constant to use for intrazonal trips." )]
        public float IntrazonalConstantWeight;

        [RunParameter( "Intrazonal Distance", 0f, "The parameter applied to the distance of an intrazonal trip." )]
        public float IntrazonalDistanceWeight;

        [RunParameter( "MaxZone", 2649, "The highest zone number taxi is allowed for." )]
        public float MaxZone;

        [RunParameter( "MinZone", 1609, "The smallest zone number taxi is allowed for." )]
        public float MinZone;

        [RunParameter( "OffPeakTrip", 0.0f, "The factor for being off peak period" )]
        public float OffPeakTrip;

        [RunParameter( "PerKFare", 1.32f, "The weight for the fare per km" )]
        public float PerKFare;

        [RunParameter( "PerMinuteFare", 0.48f, "The weight for the fare per minute." )]
        public float PerMinuteFare;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Speed", 833.33F, "Speed in meters per minute." )]
        public float taxiSpeed;

        [RunParameter( "Time", 0.0f, "The weight factor for the time" )]
        public float Time;

        [RunParameter( "Tip", 1.1f, "(1.1 is 10%) The multiplier to factor in for the tip." )]
        public float TipWeight;

        [RunParameter( "Transport Terminal Zone", "10106,10205,10307,20104,20808,20811,20812,40118", typeof( RangeSet ), "The zone number of transport terminal" )]
        public RangeSet TransportTerminal;

        [RunParameter( "Use Intrazonal Regression", false, "Should we use a regression for intrazonal trips based on the interzonal distance?" )]
        public bool UseIntrazonalRegression;

        [RunParameter( "WaitTime", 5.00f, "The weight for the wait time." )]
        public float WaitTime;

        [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
        public float CurrentlyFeasible { get; set; }

        [RunParameter( "Name", "Taxi", "The name of the mode" )]
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
        /// it is a non personal vehicle
        /// </summary>
        public bool NonPersonalVehicle
        {
            get { return true; }
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
        /// <summary>
        ///
        /// </summary>
        public IVehicleType RequiresVehicle
        {
            get { return null; }
        }

        [RunParameter( "Variance Scale", 1.0f, "The scale for varriance used for variance testing." )]
        public double VarianceScale
        {
            get;
            set;
        }

        /// <summary>
        /// Calculates the V for this mode
        /// </summary>
        /// <param name="trip">The trip</param>
        /// <returns>The V</returns>
        public double CalculateV(ITrip trip)
        {
            double v = 0.0;
            if ( UseIntrazonalRegression && trip.OriginalZone == trip.DestinationZone )
            {
                v += IntrazonalConstantWeight + trip.OriginalZone.InternalDistance * IntrazonalDistanceWeight;
            }
            else
            {
                v += CTaxi;
                v += Time * TravelTime( trip.OriginalZone, trip.DestinationZone, trip.TripStartTime ).ToMinutes();
                v += FareCost * CalculateFare( trip.OriginalZone, trip.DestinationZone, trip.ActivityStartTime );
            }
            if ( Common.GetTimePeriod( trip.ActivityStartTime ) == TravelTimePeriod.Offpeak )
            {
                v += OffPeakTrip;
            }
            if ( TransportTerminal.Contains( trip.OriginalZone.ZoneNumber ) | TransportTerminal.Contains( trip.DestinationZone.ZoneNumber ) )
            {
                v += from_to_transport_terminal;
            }
            if ( trip.Purpose == Activity.Market | trip.Purpose == Activity.JointMarket )
            {
                v += dpurp_shop_drive;
            }
            else if ( trip.Purpose == Activity.IndividualOther | trip.Purpose == Activity.JointOther )
            {
                v += dpurp_oth_drive;
            }
            return v;
        }

        public float CalculateV(IZone OriginalZone, IZone DestinationZone, Time time)
        {
            float v = CTaxi;
            v += Time * TravelTime( OriginalZone, DestinationZone, time ).ToMinutes();
            v += FareCost * CalculateFare( OriginalZone, DestinationZone, time );
            if ( Common.GetTimePeriod( time ) == TravelTimePeriod.Offpeak )
            {
                v += OffPeakTrip;
            }
            return v;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        public float Cost(IZone origin, IZone destination, Time time)
        {
            //TODO: Add time parameter
            float fare;
            if ( ( fare = CalculateFare( origin, destination, time ) ) < 0 )
            {
                fare = 0;
            }
            return fare;
        }

        public bool Feasible(IZone origin, IZone destination, Time timeOfDay)
        {
            return CurrentlyFeasible > 0;
        }

        /// <summary>
        /// Is this trip feasible based on Taxi standards?
        /// </summary>
        /// <param name="trip">The trip to check feasibility on</param>
        /// <returns>is it feasible?</returns>
        public bool Feasible(ITrip trip)
        {
            var originalZone = trip.OriginalZone.ZoneNumber;
            var destinationZone = trip.DestinationZone.ZoneNumber;
            return ( ( originalZone >= MinZone ) & ( originalZone <= MaxZone ) & ( destinationZone >= MinZone ) & ( destinationZone <= MaxZone ) )
                && ( ( originalZone == destinationZone && UseIntrazonalRegression ) || TravelTime( trip.OriginalZone, trip.DestinationZone, trip.TripStartTime ) > XTMF.Time.Zero );
        }

        /// <summary>
        ///
        /// </summary>
        public bool Feasible(ITripChain tripChain)
        {
            return true;
        }

        /// <summary>
        ///
        /// </summary>
        public bool IsObservedMode(char observedMode)
        {
            return ( observedMode == ObservedMode );
        }

        /// <summary>
        /// This gets called when mode choice is finished running
        /// </summary>
        public void ModeChoiceIterationComplete()
        {
            //do nothing here
        }

        /// <summary>
        /// Reloads the bindings to the network information
        /// </summary>
        public void ReloadNetworkData()
        {
            data.LoadData();
        }

        /// <summary>
        /// This is called before the start method as a way to pre-check that all of the parameters that are selected
        /// are in fact valid for this module.
        /// </summary>
        /// <param name="error">A string that should be assigned a detailed error</param>
        /// <returns>If the validation was successful or if there was a problem</returns>
        public bool RuntimeValidation(ref string error)
        {
            if ( Root == null )
            {
                error = "The Root Model System for the taxi mode was never loaded!";
                return false;
            }
            if ( Root.NetworkData == null )
            {
                error = "There was no network data in this model system to load from!";
                return false;
            }
            bool found = false;
            foreach ( var data in Root.NetworkData )
            {
                if ( data.NetworkType == AutoNetworkName )
                {
                    found = true;
                    this.data = data;
                    break;
                }
            }
            if ( !found )
            {
                error = String.Concat( "We could not find any data named ", AutoNetworkName, " to load as the auto network data for the Taxi mode!" );
                return false;
            }
            return true;
        }

        /// <summary>
        /// Calculates how long it will take to travel between zones
        /// </summary>
        /// <param name="origin">Where to start</param>
        /// <param name="destination">Where you go</param>
        /// <param name="time">What time of day do you start hh.mm</param>
        /// <returns>The amount of time it takes to go between the zones</returns>
        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return data.TravelTime( origin, destination, time );
        }

        /// <summary>
        /// This calculates the fare based on a simple formula
        /// This should be changed to reflect a specific cities fare rate standards
        /// </summary>
        /// <param name="origin">Where the trip starts</param>
        /// <param name="destination">Where the trip ends</param>
        /// <param name="time">What time the trip starts at in hh.mm</param>
        /// <returns>The Fare</returns>
        private float CalculateFare(IZone origin, IZone destination, Time time)
        {
            float distance = Math.Abs( origin.X - destination.X ) + Math.Abs( origin.Y - destination.Y );
            //gets auto travel time
            Time traveltime = data.TravelTime( origin, destination, time );
            //fare formula
            return ( InitialFare + ( distance / 1000.0f ) * PerKFare + traveltime.ToMinutes() * PerMinuteFare ) * TipWeight;
        }
    }
}