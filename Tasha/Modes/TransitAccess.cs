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
    public sealed class TransitAccess : ITashaMode
    {
        #region IMode Members

        [RunParameter( "AutoCost", 0, "The weight term for auto cost." )]
        public float AutoCost;

        [RunParameter( "AutoTime", 0, "The weight term for auto time." )]
        public float AutoTime;

        //Config Params
        [RunParameter( "CDriveAccess", 0, "The constant term for transit access." )]
        public float CDriveAccess;

        [RunParameter( "CostParam", 0, "The weight term for the cost." )]
        public float CostParam;

        [RunParameter( "Min Distance", 0, "The minimum distance before you can use this mode." )]
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

        [SubModelInformation( Description = "The transit access data model", Required = true )]
        public ITripComponentData TransitAccessData;

        [RunParameter( "TransitTime", 0, "The weight term for transit time." )]
        public float TransitTime;

        [RunParameter( "WaitTime", 0, "The weight term for the wait time." )]
        public float WaitTime;

        [RunParameter( "WalkTime", 0, "The weight term for the walking time" )]
        public float WalkTime;

        /// <summary>
        /// Makes a new Transit Access mode
        /// </summary>
        public TransitAccess()
        {
        }

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

        /// <summary>
        /// We do need a personal vehicle
        /// </summary>
        public bool NonPersonalVehicle
        {
            get { return false; }
        }

        /// <summary>
        /// The character to use to identify this mode
        /// </summary>
        public char OutputSignature
        {
            get;
            set;
        }

        [DoNotAutomate]
        /// <summary>
        ///
        /// </summary>
        public IVehicleType RequiresVehicle
        {
            get { return this.TashaRuntime.AutoType; }
        }

        [RootModule]
        public ITashaRuntime TashaRuntime { get; set; }

        /// <summary>
        /// Calculates the V for closest stations and then chooses one based on a distribution function
        /// </summary>
        /// <param name="trip">The trip to calculate v for</param>
        /// <returns>a V value based on a distribution</returns>
        public double CalculateV(ITrip trip)
        {
            int[] accessStations = (int[])trip.GetVariable( "feasible-subway-stations" );

            double[] V = new double[accessStations.Length];
            var auto = this.TashaRuntime.AutoMode;
            for ( int i = 0; i < accessStations.Length; i++ )
            {
                var station = this.TashaRuntime.ZoneSystem.Get( accessStations[i] );
                V[i] = this.CDriveAccess;
                V[i] += this.AutoTime * auto.TravelTime( trip.OriginalZone, station, trip.TripStartTime ).ToFloat();
                V[i] += this.WalkTime * this.TransitAccessData.WalkTime( station, trip.DestinationZone, trip.TripStartTime ).ToMinutes();
                V[i] += this.WaitTime * this.TransitAccessData.WaitTime( station, trip.DestinationZone, trip.TripStartTime ).ToMinutes();
                V[i] += this.AutoCost * auto.Cost( trip.OriginalZone, station, trip.TripStartTime );
                V[i] += this.TransitTime * this.TransitAccessData.InVehicleTravelTime( station, trip.DestinationZone, trip.TripStartTime ).ToMinutes();
                V[i] += this.ParkingCost * this.TransitAccessData.Station( station ).ParkingCost;
                if ( ( Common.GetTimePeriod( trip.ActivityStartTime ) == Tasha.Common.TravelTimePeriod.Morning ) ||
                ( Common.GetTimePeriod( trip.ActivityStartTime ) == Tasha.Common.TravelTimePeriod.Afternoon ) )
                {
                    V[i] += PeakTrip;
                }

                if ( trip.TripChain.Person.Occupation == Occupation.Retail )
                {
                    V[i] += this.OccSalesTransit;
                }

                if ( trip.TripChain.Person.Occupation == Occupation.Office )
                {
                    V[i] += this.OccGeneralTransit;
                }
            }

            Array.Sort(V);

            //int RndChoice = Common.RandChoiceCDF(V, int.Parse(this.Configuration.Get("Seed")));
            int RndChoice = 0;
            trip.Attach( "subway-access-station", accessStations[RndChoice < 0 ? 0 : RndChoice] );

            return V[RndChoice < 0 ? 0 : RndChoice];
        }

        /// <summary>
        /// Getting the lowest cost possible for this particular trip
        /// </summary>
        /// <param name="origin">The origin of the trip</param>
        /// <param name="destination">The destination of the trip</param>
        /// <returns>The lowest cost</returns>
        public float Cost(IZone origin, IZone destination, Time time)
        {
            int[] Stations = this.TransitAccessData.ClosestStations( origin );

            float MinCost = float.MaxValue;
            var auto = this.TashaRuntime.AutoMode;
            var zoneSystem = TashaRuntime.ZoneSystem;
            for ( int i = 0; i < Stations.Length; i++ )
            {
                var stationZone = zoneSystem.Get( Stations[i] );
                float cost = auto.Cost( origin, stationZone, time )
                                    + this.TransitAccessData.Station( stationZone ).ParkingCost;
                if ( cost < MinCost )
                {
                    MinCost = cost;
                }
            }

            return MinCost;
        }

        /// <summary>
        /// Checks if there is a way to get from the origin to the destination
        /// via any of the five closest subway stations
        /// </summary>
        /// <param name="trip">The Trip</param>
        /// <returns>Is it feasible?</returns>
        public bool Feasible(ITrip trip)
        {
            if ( !trip.TripChain.Person.Licence || trip.TripChain.Person.Household.Vehicles.Length == 0 )
            {
                return false;
            }

            if ( trip.OriginalZone.Distance( trip.DestinationZone ) < this.MinDistance ) return false;
            trip.Attach( "subway-access-station", -1 );

            int[] stations = this.TransitAccessData.ClosestStations( trip.OriginalZone );

            bool feasible = false;

            int[] feasibleStations = new int[stations.Length];

            int numFeasible = 0;

            for ( int i = 0; i < stations.Length; i++ )
            {
                //checking if there auto travel time is > 0 and the time in transit > 0 : meaning there exists
                //a route from origin to destination through subway station
                IZone station;
                if ( this.TashaRuntime.AutoMode.TravelTime( trip.OriginalZone, station = this.TashaRuntime.ZoneSystem.Get( stations[i] ), trip.TripStartTime ) > Time.Zero
                    && this.TransitAccessData.InVehicleTravelTime( station, trip.DestinationZone, trip.TripStartTime ) > Time.Zero )
                {
                    feasibleStations[numFeasible++] = stations[i];
                    feasible = true;
                }
            }

            Array.Resize<int>( ref feasibleStations, numFeasible );
            trip.Attach( "feasible-subway-stations", feasibleStations );

            return feasible;
        }

        /// <summary>
        /// Testing whether the entire trip chain is feasible
        /// Its feasible if there is an egress after a subway access
        /// </summary>
        /// <param name="tripChain">the trip chain</param>
        /// <returns>if the trip chain is feasible</returns>
        public bool Feasible(ITripChain tripChain)
        {
            bool AccessOccured = false;
            int carLocation = tripChain.Trips[0].OriginalZone.ZoneNumber;

            //checking if each access has an Egress and car is located at origin of access
            foreach ( ITrip trip in tripChain.Trips )
            {
                if ( trip.Mode is Auto )
                {
                    carLocation = trip.DestinationZone.ZoneNumber;
                }

                if ( trip.Mode is TransitEgress )//if mode is an Egress
                {
                    if ( !AccessOccured )
                        return false; //there was no access before this Egress

                    AccessOccured = false; //cancel out the previous access with this Egress
                }
                else if ( trip.Mode is TransitAccess )
                {
                    if ( AccessOccured || trip.OriginalZone.ZoneNumber != carLocation )
                        return false; //there was an access before this access

                    AccessOccured = true;
                }
            }

            return !AccessOccured;
        }

        /// <summary>
        /// Lets the mode know that mode choice has finished
        /// </summary>
        public void ModeChoiceIterationComplete()
        {
        }

        /// <summary>
        /// Releases the data held by transit access
        /// </summary>
        public void ReleaseData()
        {
            if ( this.TransitAccessData != null )
            {
                this.TransitAccessData.UnloadData();
            }
        }

        /// <summary>
        /// Reloads the information about this mode
        /// </summary>
        public void ReloadNetworkData()
        {
            this.TransitAccessData.LoadData();
        }

        /// <summary>
        /// The travel time for this mode of transport
        /// </summary>
        /// <param name="origin">The origin zone</param>
        /// <param name="destination">The destination zone</param>
        /// <param name="time">The time period = 0</param>
        /// <returns>The travel time between specified zones</returns>
        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            //gets the closest stations
            int[] Stations = this.TransitAccessData.ClosestStations( origin );

            Time MinTravelTime = Time.EndOfDay;

            for ( int i = 0; i < Stations.Length; i++ )
            {
                var station = this.TashaRuntime.ZoneSystem.Get( Stations[i] );
                Time TravelTime = this.TashaRuntime.AutoMode.TravelTime( station, destination, time )
                                    + this.TransitAccessData.InVehicleTravelTime( station, destination, time )
                                    + this.TransitAccessData.WalkTime( station, destination, time )
                                    + this.TransitAccessData.WaitTime( station, destination, time );

                if ( TravelTime < MinTravelTime )
                {
                    MinTravelTime = TravelTime;
                }
            }

            return MinTravelTime;
        }

        #endregion IMode Members

        [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
        public float CurrentlyFeasible { get; set; }

        public string NetworkType
        {
            get;
            set;
        }

        [Parameter( "Observed Mode Character Code", 'A', "The character code used for model estimation." )]
        public char ObservedMode
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

        [Parameter( "Variance Scale", 1.0f, "The scale for varriance used for variance testing." )]
        public double VarianceScale
        {
            get;
            set;
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            throw new NotImplementedException();
        }

        public bool Feasible(IZone origin, IZone destination, Time timeOfDay)
        {
            return CurrentlyFeasible > 0;
        }

        /// <summary>
        ///
        /// </summary>
        public bool IsObservedMode(char observedMode)
        {
            return ( observedMode == this.ObservedMode );
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
    }
}