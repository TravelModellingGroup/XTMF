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

namespace Tasha.Modes
{
    /// <summary>
    ///
    /// </summary>
    public sealed class Transit : ITashaMode
    {
        [RunParameter( "ChildBus", 0.0f, "The factor for a child taking the bus" )]
        public float ChildBus;

        [RunParameter( "CTransit", 0.0f, "The constant factor for transit" )]
        public float CTransit;

        [DoNotAutomate]
        public ITripComponentData Data;

        [RunParameter( "dpurp_oth_drive", 0f, "The weight for the cost of doing an other drive (ITashaRuntime only)" )]
        public float dpurp_oth_drive;

        [RunParameter( "dpurp_shop_drive", 0f, "The weight for the cost of doing a shopping drive (ITashaRuntime only)" )]
        public float dpurp_shop_drive;

        [RunParameter( "Fare", 0.0f, "The factor for transit fare" )]
        public float Fare;

        [RunParameter( "Intrazonal Constant", 0f, "The constant to use for intrazonal trips." )]
        public float IntrazonalConstantWeight;

        [RunParameter( "Intrazonal Distance", 0f, "The parameter applied to the distance of an intrazonal trip." )]
        public float IntrazonalDistanceWeight;

        [RunParameter( "MinAgeAlone", 16, "The youngest a person can be and take the bus" )]
        public int MinAgeAlone;

        [RunParameter( "OccGeneralTransit", 0.0f, "(Tasha Only) The factor for having a general job" )]
        public float OccGeneralTransit;

        [RunParameter( "OccSalesTransit", 0.0f, "(Tasha Only) The factor for working a sales job" )]
        public float OccSalesTransit;

        [RootModule]
        public ITravelDemandModel TashaRuntime;

        [RunParameter( "travelTime", 0.0f, "The factor for travel time" )]
        public float travelTime;

        [RunParameter( "Use Intrazonal Regression", false, "Should we use a regression for intrazonal trips based on the interzonal distance?" )]
        public bool UseIntrazonalRegression;

        [RunParameter( "waitTime", 0.0f, "The factor for wait time" )]
        public float waitTime;

        [RunParameter( "walkTime", 0.0f, "The factor for walk time" )]
        public float walkTime;

        [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
        public float CurrentlyFeasible { get; set; }

        [RunParameter( "Name", "Transit", "The name of the mode" )]
        public string ModeName { get; set; }

        /// <summary>
        /// The name of this mode
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        [RunParameter( "Transit Network Name", "Transit", "The name of the transit network to use for this mode." )]
        public string NetworkType
        {
            get;
            set;
        }

        /// <summary>
        /// This is not a personal vehical
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
        [RunParameter( "Observed Signature Code", 'A', "The character code used for model output." )]
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
        /// Does not require any vehicle type
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
        ///
        /// </summary>
        public double CalculateV(ITrip trip)
        {
            double V = 0;
            var person = trip.TripChain.Person;
            if ( trip.OriginalZone == trip.DestinationZone && UseIntrazonalRegression )
            {
                V += IntrazonalConstantWeight + trip.OriginalZone.InternalDistance * IntrazonalDistanceWeight;
            }
            else
            {
                //transit constant
                V += CTransit;
                //In vehicle Travel Time
                V += Data.InVehicleTravelTime( trip.OriginalZone, trip.DestinationZone, trip.ActivityStartTime ).ToMinutes() * travelTime;
                //Wait time
                V += Data.WaitTime( trip.OriginalZone, trip.DestinationZone, trip.ActivityStartTime ).ToMinutes() * waitTime;
                //walk time
                V += Data.WalkTime( trip.OriginalZone, trip.DestinationZone, trip.ActivityStartTime ).ToMinutes() * walkTime;
                //cost
                if ( person.TransitPass != TransitPass.Metro | person.TransitPass != TransitPass.Combination )
                {
                    V += Data.TravelCost( trip.OriginalZone, trip.DestinationZone, trip.ActivityStartTime ) * Fare;
                }
            }
            if ( person.Occupation == Occupation.Retail )
            {
                V += OccSalesTransit;
            }
            if ( person.Child )
            {
                V += ChildBus;
            }
            if ( person.Occupation == Occupation.Office )
            {
                V += OccGeneralTransit;
            }
            if ( trip.Purpose == Activity.Market | trip.Purpose == Activity.JointMarket )
            {
                V += dpurp_shop_drive;
            }
            else if ( trip.Purpose == Activity.IndividualOther | trip.Purpose == Activity.JointOther )
            {
                V += dpurp_oth_drive;
            }
            return V;
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            //transit constant
            float V = CTransit;
            //In vehicle Travel Time
            V += Data.InVehicleTravelTime( origin, destination, time ).ToMinutes() * travelTime;
            //Wait time
            V += Data.WaitTime( origin, destination, time ).ToMinutes() * waitTime;
            //walk time
            V += Data.WalkTime( origin, destination, time ).ToMinutes() * walkTime;
            //cost
            V += Data.TravelCost( origin, destination, time ) * Fare;
            return V;
        }

        public float Cost(IZone origin, IZone destination, Time time)
        {
            return Data.TravelCost( origin, destination, time );
        }

        public bool Feasible(IZone origin, IZone destination, Time timeOfDay)
        {
            return ( origin.ZoneNumber == destination.ZoneNumber && UseIntrazonalRegression ) || ( CurrentlyFeasible > 0 & Data.ValidOd( origin, destination, timeOfDay ) );
        }

        /// <summary>
        /// Is this trip feasible
        /// </summary>
        /// <param name="trip">The trip to evaluate feasibility on</param>
        /// <returns>Whether the trip is feasible</returns>
        public bool Feasible(ITrip trip)
        {
            var origin = trip.OriginalZone;
            var destination = trip.DestinationZone;
            var time = trip.ActivityStartTime;
            if ( trip.Purpose != Activity.JointOther && trip.TripChain.Person.Age < MinAgeAlone )
            {
                return false;
            }
            if ( UseIntrazonalRegression && trip.OriginalZone.ZoneNumber == trip.DestinationZone.ZoneNumber )
            {
                return true;
            }
            if ( !Data.ValidOd( origin, destination, time ) )
            {
                return false;
            }
            return TravelTime( origin, destination, time ) > Time.Zero;
        }

        /// <summary>
        /// </summary>
        /// <param name="tripChain">TripChain to to check feasibility with</param>
        /// <returns>if its feasible</returns>
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
        ///
        /// </summary>
        public void ModeChoiceIterationComplete()
        {
            //do nothing here
        }

        /// <summary>
        /// Release the data in the cache from
        /// being read
        /// </summary>
        public void ReleaseData()
        {
            Data.UnloadData();
        }

        /// <summary>
        /// Load the network data for this mode
        /// </summary>
        public void ReloadNetworkData()
        {
            Data.LoadData();
        }

        /// <summary>
        /// This is called before the start method as a way to pre-check that all of the parameters that are selected
        /// are in fact valid for this module.
        /// </summary>
        /// <param name="error">A string that should be assigned a detailed error</param>
        /// <returns>If the validation was successful or if there was a problem</returns>
        public bool RuntimeValidation(ref string error)
        {
            Data = null;
            foreach ( var network in TashaRuntime.NetworkData )
            {
                if ( network.NetworkType == NetworkType && network is ITripComponentData )
                {
                    Data = network as ITripComponentData;
                }
            }
            if ( Data == null )
            {
                error = "We were unable to find a transit network with the name \"" + NetworkType + "\"!";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Getting the full travel time of this trip
        /// </summary>
        /// <param name="origin">origin of trip</param>
        /// <param name="destination">destination of trip</param>
        /// <param name="time">time of day</param>
        /// <returns>full travel time for this trip</returns>
        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return Data.WalkTime( origin, destination, time ) +
                Data.WaitTime( origin, destination, time ) +
                Data.InVehicleTravelTime( origin, destination, time );
        }
    }
}