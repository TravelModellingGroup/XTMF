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
    public sealed class GoNonDrive : ITashaMode
    {
        //constant data for V calculations
        /// <summary>
        ///
        /// </summary>
        [RunParameter( "AutoCost", 0.0f, "The factor applied to the auto cost" )]
        public float AutoCost;

        //Config Params
        [RunParameter( "CGoNonDrive", 0.0f, "The constant factor for the GoNonDrive mode" )]
        public float CGoNonDrive;

        /// <summary>
        ///
        /// </summary>
        [RunParameter( "FareCost", 0.0f, "The factor applied to the cost of the transit and GO" )]
        public float FareCost;

        /// <summary>
        /// Accessor for go transit data
        /// </summary>
        [SubModelInformation( Description = "The data used for Go", Required = true )]
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

        /// <summary>
        ///
        /// </summary>
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

        /// <summary>
        ///
        /// </summary>
        [RunParameter( "WalkTime", 0.0f, "The factor applied to the walk time" )]
        public float WalkTime;

        [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
        public float CurrentlyFeasible { get; set; }

        [RunParameter( "Name", "Go Non Drive", "The name of the mode" )]
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
            get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); }
        }

        /// <summary>
        ///
        /// </summary>
        [DoNotAutomate]
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
            int[] accessStations = (int[])trip.GetVariable( "feasible-ndg-stations" );
            int egressStation = GOData.GetClosestStations( trip.DestinationZone.ZoneNumber )[0];

            double[] v = new double[accessStations.Length];

            for ( int i = 0; i < v.Length; i++ )
            {
                v[i] += CGoNonDrive;

                //comined transit time
                v[i] += TransitTime * ( GOData.GetTransitAccessTime( trip.OriginalZone.ZoneNumber, accessStations[i] ) +
                                                +GOData.GetTransitEgressTime( egressStation, trip.DestinationZone.ZoneNumber ) );

                //go transit time
                v[i] += TransitRailTime * ( GOData.GetLineHaulTime( accessStations[i], egressStation ) );

                //combined walk time
                v[i] += WalkTime * ( GOData.GetAccessWalkTime( trip.OriginalZone.ZoneNumber, accessStations[i] )
                                                + GOData.GetEgressWalkTime( trip.DestinationZone.ZoneNumber, egressStation ) );
                //combined wait time
                v[i] += WaitTime * ( GOData.GetAccessWaitTime( trip.OriginalZone.ZoneNumber, accessStations[i] )
                                                + GOData.GetEgressWaitTime( trip.DestinationZone.ZoneNumber, egressStation ) );

                //fare
                v[i] += FareCost * ( GOData.GetTransitFair( trip.OriginalZone.ZoneNumber, accessStations[i] )
                                                + GOData.GetGoFair( accessStations[i], egressStation )
                                                + GOData.GetTransitFair( trip.DestinationZone.ZoneNumber, egressStation ) );

                if ( ( Common.GetTimePeriod( trip.ActivityStartTime ) == TravelTimePeriod.Morning ) ||
  ( Common.GetTimePeriod( trip.ActivityStartTime ) == TravelTimePeriod.Afternoon ) )
                {
                    v[i] += PeakTrip;
                }

                if ( trip.TripChain.Person.Occupation == Occupation.Retail )
                {
                    v[i] += OccSalesTransit;
                }

                if ( trip.TripChain.Person.Occupation == Occupation.Office )
                {
                    v[i] += OccGeneralTransit;
                }
            }

            Array.Sort(v);

            //int choice = Common.RandChoiceCDF(V, int.Parse(this.Configuration.Get("Seed")));
            int choice = 0;
            return v[choice];
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            throw new NotImplementedException();
        }

        ///  <summary>
        ///  The cost of the going from one zone to another
        /// 
        ///  </summary>
        ///  <param name="origin">the origin zone</param>
        ///  <param name="destination">the destination zone</param>
        /// <param name="time"></param>
        /// <returns>the cost</returns>
        public float Cost(IZone origin, IZone destination, Time time)
        {
            int[] accessStations = GOData.GetClosestStations( origin.ZoneNumber );
            int egressStation = GOData.GetClosestStations( destination.ZoneNumber )[0];
            float minCost = float.MaxValue;
            foreach ( int accessStation in accessStations )
            {
                var cost = GOData.GetGoFair( accessStation, egressStation );

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
        /// Check to see if base mode is feasible for the given trip
        /// </summary>
        /// <param name="trip">The trip to check if we can possibly be used for</param>
        /// <returns>If trip is feasible</returns>
        public bool Feasible(ITrip trip)
        {
            if ( trip.OriginalZone.Distance( trip.DestinationZone ) < GOData.MinDistance ) return false;
            int[] accessStations = GOData.GetClosestStations( trip.OriginalZone.ZoneNumber );
            int egressStation = GOData.GetClosestStations( trip.DestinationZone.ZoneNumber )[0];

            //closest access station same as egress station: skip
            if ( accessStations[0] == egressStation ) return false;

            int[] feasibleStations = new int[accessStations.Length];

            int i = 0;

            bool feasible = false;

            foreach ( int accessStation in accessStations )
            {
                float transitTime = GOData.GetTransitAccessTime( trip.OriginalZone.ZoneNumber, accessStation );
                float transitTime2 = GOData.GetTransitEgressTime( egressStation, trip.DestinationZone.ZoneNumber );
                float lineHaulTime = GOData.GetLineHaulTime( accessStation, egressStation );

                if ( ( accessStation == egressStation )
                    | ( transitTime < 0.1f )
                    | ( transitTime2 < 0.1f )
                    | ( lineHaulTime < 0.1f )
                    )
                {
                    //not feasible
                }
                else
                {
                    feasibleStations[i++] = accessStation;
                    feasible = true;
                }
            }
            Array.Resize( ref feasibleStations, i );
            trip.Attach( "feasible-ndg-stations", feasibleStations );
            return feasible;
        }

        /// <summary>
        /// Checking if trip chain is feasible for go non drive
        /// </summary>
        /// <param name="tripChain">The trip chain</param>
        /// <returns>is base trip chain feasible?</returns>
        public bool Feasible(ITripChain tripChain)
        {
            return true;
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
        /// Calculating travel time for non-drive go access
        /// </summary>
        /// <param name="origin">The origin zone</param>
        /// <param name="destination">The destination zone</param>
        /// <param name="time">The time period</param>
        /// <returns>the minumum travel time</returns>
        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return Time.Zero;
        }
    }
}