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
    public sealed class GoAccess : ITashaMode
    {
        /// <summary>
        ///
        /// </summary>
        [Parameter( "AutoCost", 0.0f, "The factor applied to the auto cost" )]
        public float AutoCost;

        //constant data for V calculations
        /// <summary>
        ///
        /// </summary>
        [Parameter( "AutoTime", 0.0f, "The factor applied to the auto travel time" )]
        public float AutoTime;

        //Config Params
        [RunParameter( "CGoAccess", 0.0f, "The constant factor for the GoAccess mode" )]
        public float CDriveAccess;

        /// <summary>
        ///
        /// </summary>
        [Parameter( "FareCost", 0.0f, "The factor applied to the cost of the transit and GO" )]
        public float FareCost;

        [SubModelInformation( Description = "The data to use for Go Transit", Required = true )]
        public IGoData goData;

        [Parameter( "OccGeneralTransit", 0.0f, "The factor applied when their occupation is general." )]
        public float OccGeneralTransit;

        [Parameter( "OccSalesTransit", 0.0f, "The factor applied when their occupation is sales" )]
        public float OccSalesTransit;

        [Parameter( "ParkingCost", 0.0f, "The factor applied to the cost of parking" )]
        public float ParkingCost;

        [Parameter( "PeakTrip", 0.0f, "The factor applied when this is taken during the peak hours" )]
        public float PeakTrip;

        [RootModule]
        public ITashaRuntime TashaRuntime;

        [Parameter( "TransitRailTime", 0.0f, "The factor applied to the time spent on rail" )]
        public float TransitRailTime;

        /// <summary>
        ///
        /// </summary>
        [Parameter( "TransitTime", 0.0f, "The factor applied to the transit time" )]
        public float TransitTime;

        [Parameter( "UnionStation", 0, "The zone number of union station" )]
        public int UnionStation;

        /// <summary>
        ///
        /// </summary>
        [Parameter( "WaitTime", 0.0f, "The factor applied to the time waiting" )]
        public float WaitTime;

        /// <summary>
        ///
        /// </summary>
        /// <summary>
        ///
        /// </summary>
        [Parameter( "WalkTime", 0.0f, "The factor applied to the walk time" )]
        public float WalkTime;

        /// <summary>
        ///
        /// </summary>
        public GoAccess()
        {
        }

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
        /// Does this NOT require a vehical
        /// </summary>
        public bool NonPersonalVehicle
        {
            get { return false; }
        }

        [Parameter( "Observed Mode Character Code", 'A', "The character code used for model estimation." )]
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

        [DoNotAutomate]
        /// <summary>
        /// What kind of vehical does this require?
        /// </summary>
        public IVehicleType RequiresVehicle
        {
            get { return this.TashaRuntime.AutoType; }
        }

        [Parameter( "Variance Scale", 1.0f, "The scale for varriance used for variance testing." )]
        public double VarianceScale
        {
            get;
            set;
        }

        /// <summary>
        /// Calculates the V for a given trip
        /// </summary>
        /// <param name="trip"></param>
        /// <returns></returns>
        public double CalculateV(ITrip trip)
        {
            int[] accessStations = (int[])trip.GetVariable( "feasible-go-stations" );
            int egressStation = goData.GetClosestStations( trip.DestinationZone.ZoneNumber )[0];

            double[] V = new double[accessStations.Length];

            for ( int i = 0; i < accessStations.Length; i++ )
            {
                V[i] = this.CDriveAccess;
                V[i] += AutoTime * goData.GetAutoTime( trip.OriginalZone.ZoneNumber, accessStations[i] );
                V[i] += AutoCost * goData.GetAutoCost( trip.OriginalZone.ZoneNumber, accessStations[i] );
                V[i] += TransitRailTime * goData.GetLineHaulTime( accessStations[i], egressStation );
                V[i] += TransitTime * goData.GetTransitEgressTime( egressStation, trip.DestinationZone.ZoneNumber );
                V[i] += WalkTime * goData.GetEgressWalkTime( trip.DestinationZone.ZoneNumber, egressStation );
                V[i] += WaitTime * goData.GetEgressWaitTime( trip.DestinationZone.ZoneNumber, egressStation );
                V[i] += FareCost * ( goData.GetGoFair( accessStations[i], egressStation )
                                                + goData.GetTransitFair( trip.DestinationZone.ZoneNumber, egressStation ) );

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

            //int choice = Common.RandChoiceCDF(V, int.Parse(this.Configuration.Get("Seed")));
            int choice = 0;
            //attaching the station chosen
            trip.Attach( "go-access-station", accessStations[choice] );

            return V[choice];
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Getting the minimum cost of a Go Access
        /// </summary>
        /// <param name="origin">The origin zone</param>
        /// <param name="destination">The destination zone</param>
        /// <returns>The cost of the trip</returns>
        public float Cost(IZone origin, IZone destination, Time time)
        {
            float MinCost = float.MaxValue;

            int[] accessStations = goData.GetClosestStations( origin.ZoneNumber );
            int egressStation = goData.GetClosestStations( destination.ZoneNumber )[0];

            foreach ( int accessStation in accessStations )
            {
                float cost = goData.GetAutoCost( origin.ZoneNumber, accessStation ) +
                                    goData.GetGoFair( accessStation, egressStation );

                if ( cost < MinCost )
                {
                    MinCost = cost;
                }
            }

            return MinCost;
        }

        public bool Feasible(IZone origin, IZone destination, Time timeOfDay)
        {
            return CurrentlyFeasible > 0;
        }

        /// <summary>
        /// Is this trip feasible?
        /// </summary>
        /// <param name="trip">The trip</param>
        /// <returns>is it feasible</returns>
        public bool Feasible(ITrip trip)
        {
            //trip.Attach("go-access-station", -2);

            if ( !trip.TripChain.Person.Licence || trip.TripChain.Person.Household.Vehicles.Length == 0 )
                return false;

            if ( trip.OriginalZone.Distance( trip.DestinationZone ) < goData.MinDistance )
                return false;

            bool feasible = false;

            int[] accessStations = goData.GetClosestStations( trip.OriginalZone.ZoneNumber );
            int egressStationNum = goData.GetClosestStations( trip.DestinationZone.ZoneNumber )[0];

            //same closest stations, skip it
            if ( accessStations[0] == egressStationNum || egressStationNum == -1 ) return false;

            ITransitStation egressStation = goData.GetStation( egressStationNum );

            int[] feasibleStations = new int[accessStations.Length];

            int i = 0;

            foreach ( int access in accessStations )
            {
                float duration, frequencyAtStart;
                duration = goData.GetAutoTime( trip.OriginalZone.ZoneNumber, access );
                frequencyAtStart = goData.GetGoFrequency( access, egressStation.StationNumber, trip.ActivityStartTime.ToFloat() );
                ITransitStation accessStation = goData.GetStation( access );

                if (
                    ( ( duration > 0 )
                     & ( access != egressStationNum )
                     & ( frequencyAtStart > 0 )
                     & ( access != -1 )
                    )
                    && duration + trip.ActivityStartTime.ToFloat() > goData.StartTime
                    && duration + trip.ActivityStartTime.ToFloat() < goData.EndTime
                    )
                {
                    feasible = true;
                    feasibleStations[i++] = access;
                }
            }
            Array.Resize<int>( ref feasibleStations, i );
            trip.Attach( "feasible-go-stations", feasibleStations );
            return feasible;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="tripChain"></param>
        /// <returns></returns>
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
            return ( observedMode == this.ObservedMode );
        }

        public void ModeChoiceIterationComplete()
        {
            //nothing
        }

        public void ReleaseData()
        {
            goData.Release();
        }

        public void ReloadNetworkData()
        {
            goData.Reload();
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
        /// Getting the travel time for travelling froma specific origin to destination
        /// </summary>
        /// <param name="origin">The origin zone</param>
        /// <param name="destination">The destination zone</param>
        /// <param name="time">the time period</param>
        /// <returns>The minumum travel time</returns>
        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            Time MinTravelTime = Time.EndOfDay;
            int[] accessStations = goData.GetClosestStations( origin.ZoneNumber );
            int egressStation = goData.GetClosestStations( destination.ZoneNumber )[0];
            foreach ( int accessStation in accessStations )//go through all possible stations
            {
                /*Time travelTime = goData.GetAutoTime(origin.ZoneNumber, accessStation) +
                                    goData.GetLineHaulTime(accessStation, egressStation) +
                                    goData.GetTotalTransitEgressTime(egressStation, destination.ZoneNumber);
                */
                Time travelTime = Time.Zero;
                if ( travelTime < MinTravelTime )
                {
                    MinTravelTime = travelTime;
                }
            }
            return MinTravelTime;
        }
    }
}