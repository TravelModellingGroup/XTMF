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
using System.Collections.Generic;
using Datastructure;
using XTMF;

namespace Tasha.Common
{
    public static class HouseholdExtender
    {
        public static ITashaRuntime TashaRuntime;

        private static TripComparer TripComparer = new TripComparer();

        /// <summary>
        /// Gets all the tripchains in a household
        /// </summary>
        /// <param name="household">The household</param>
        /// <returns>all the trip chains</returns>
        public static List<ITripChain> AllAuxTripsChains(this ITashaHousehold household)
        {
            List<ITripChain> trips = new List<ITripChain>();

            //flatten households trips
            foreach ( var p in household.Persons )
            {
                trips.AddRange( p.AuxTripChains );
            }

            return trips;
        }

        /// <summary>
        /// Gets all the tripchains in a household
        /// </summary>
        /// <param name="household">The household</param>
        /// <returns>all the trip chains</returns>
        public static List<ITripChain> AllTripChains(this ITashaHousehold household)
        {
            List<ITripChain> trips = new List<ITripChain>();
            //flatten households trips
            foreach ( var p in household.Persons )
            {
                trips.AddRange( p.TripChains );
                //auxiliary trip chains
                trips.AddRange( p.AuxTripChains );
            }
            return trips;
        }

        /// <summary>
        /// Gets all the trip chains without Auxiliary trip chains in the household that use the specified vehicle
        /// </summary>
        /// <param name="household"></param>
        /// <param name="vehicle"></param>
        /// <returns></returns>
        public static List<ITripChain> AllTripChainsThatUseVehicle(this ITashaHousehold household, IVehicleType vehicle)
        {
            List<ITripChain> trips = new List<ITripChain>();

            //flatten households trips
            foreach ( var p in household.Persons )
            {
                foreach ( var tc in p.TripChains )
                {
                    if ( tc.RequiresVehicle.Contains( vehicle ) )
                    {
                        trips.Add( tc );
                    }
                }
            }

            return trips;
        }

        /// <summary>
        /// Gets all the trip chains and auxiliary trip chains that use the specified vehicle
        /// </summary>
        /// <param name="household"></param>
        /// <param name="vehicle"></param>
        /// <returns></returns>
        public static List<ITripChain> AllTripChainsWithAuxThatUseVehicle(this ITashaHousehold household, IVehicleType vehicle)
        {
            List<ITripChain> trips = new List<ITripChain>();

            //flatten households trips
            foreach ( var p in household.Persons )
            {
                foreach ( var tc in p.TripChains )
                {
                    if ( tc.RequiresVehicle.Contains( vehicle ) )
                    {
                        trips.Add( tc );
                    }
                }
                foreach ( var tc in p.AuxTripChains )
                {
                    if ( tc.RequiresVehicle.Contains( vehicle ) )
                    {
                        trips.Add( tc );
                    }
                }
            }

            return trips;
        }

        /// <summary>
        /// Gets all the tripchains in a household not including auxiliary trip chains
        /// </summary>
        /// <param name="household">The household</param>
        /// <returns>all the trip chains</returns>
        public static List<ITripChain> AllTripsChainsWithoutAuxChains(this ITashaHousehold household)
        {
            List<ITripChain> trips = new List<ITripChain>();

            //flatten households trips
            foreach ( var p in household.Persons )
            {
                trips.AddRange( p.TripChains );
            }
            return trips;
        }

        public static Dictionary<TashaTimeSpan, int> FindVehicleAvailabilites(List<ITripChain> tc, int numVehicles)
        {
            int vehiclesAvailable = numVehicles;
            Dictionary<TashaTimeSpan, int> availabilities = new Dictionary<TashaTimeSpan, int>();
            if ( tc.Count == 0 )
            {
                availabilities.Add( new TashaTimeSpan( TashaRuntime.StartOfDay,
                TashaRuntime.EndOfDay ), vehiclesAvailable );
                return availabilities;
            }
            List<Pair<Time, int>> tripStartAndEndTimes = new List<Pair<Time, int>>();
            foreach ( var tripChain in tc )
            {
                Pair<Time, int> startTime = new Pair<Time, int>( tripChain.StartTime, -1 );
                Pair<Time, int> endTime = new Pair<Time, int>( tripChain.EndTime, 1 );

                tripStartAndEndTimes.Add( startTime );
                tripStartAndEndTimes.Add( endTime );
            }

            tripStartAndEndTimes.Sort( delegate(Pair<Time, int> p1, Pair<Time, int> p2)
            {
                var first = p1.First;
                var second = p2.First;
                if ( second < first ) return 1;
                if ( second > first ) return -1;
                return 0;
            } );

            for ( int i = -1; i < tripStartAndEndTimes.Count; i++ )
            {
                TashaTimeSpan span;
                //from last trip to end of day
                if ( i == tripStartAndEndTimes.Count - 1 )
                {
                    span = new TashaTimeSpan( tripStartAndEndTimes[i].First, Time.EndOfDay );
                    vehiclesAvailable += tripStartAndEndTimes[i].Second;
                    if ( availabilities.ContainsKey( span ) )
                    {
                        availabilities[span] += availabilities[span];
                    }
                    else
                    {
                        availabilities.Add( span, vehiclesAvailable );
                    }
                }
                else if ( i == -1 )//from start of day to first trip
                {
                    span = new TashaTimeSpan( Time.StartOfDay, tripStartAndEndTimes[i + 1].First );
                    if ( availabilities.ContainsKey( span ) )
                    {
                        availabilities[span] += availabilities[span];
                    }
                    else
                    {
                        availabilities.Add( span, vehiclesAvailable );
                    }
                }
                else //trips in between
                {
                    vehiclesAvailable += tripStartAndEndTimes[i].Second;
                    span = new TashaTimeSpan( tripStartAndEndTimes[i].First, tripStartAndEndTimes[i + 1].First );
                    if ( availabilities.ContainsKey( span ) )
                    {
                        availabilities[span] += availabilities[span];
                    }
                    else
                    {
                        availabilities.Add( span, vehiclesAvailable );
                    }
                }
            }
            return availabilities;
        }

        /// <summary>
        /// Returns a dictionary of time spans indicating at which time the # of vehicles not in use
        /// </summary>
        /// <param name="h"></param>
        /// <param name="vehicleType"></param>
        /// <param name="includeAuxTripChains"></param>
        /// <returns></returns>
        public static Dictionary<TashaTimeSpan, int> FindVehicleAvailabilites(this ITashaHousehold h, IVehicleType vehicleType, bool includeAuxTripChains)
        {
            return FindVehicleAvailabilitesHelper( h, vehicleType, includeAuxTripChains );
        }

        /// <summary>
        /// Gets all the trip chains of a Person
        /// The Auxiliary connecting trips are combined to (Non-Auxiliary) Trip chains
        /// The Regular trip chains (non-auxiliary) are therefore copied to not
        /// interfere with the next household iteration
        /// </summary>
        /// <param name="person"></param>
        /// <returns></returns>
        public static List<ITripChain> GetAllTripChains(this ITashaPerson person)
        {
            List<ITripChain> allTripChains = new List<ITripChain>();
            List<ITripChain> addedChains = new List<ITripChain>();

            //adding all auxiliary trip chains
            foreach ( var auxTripChain in person.AuxTripChains )
            {
                ITrip connectingTripChain = auxTripChain["ConnectingChain"] as ITrip;
                Activity purpose = (Activity)auxTripChain["Purpose"];
                if ( connectingTripChain == null )
                {
                    allTripChains.Add( auxTripChain );
                }
                else
                {
                    ITripChain clonedChain = connectingTripChain.TripChain.DeepClone();
                    if ( purpose == Activity.Dropoff )
                    {
                        clonedChain.Trips.RemoveAt( 0 );
                    }
                    else if ( purpose == Activity.Pickup )
                    {
                        clonedChain.Trips.RemoveAt( clonedChain.Trips.Count - 1 );
                    }
                    clonedChain.Trips.AddRange( auxTripChain.Trips );
                    clonedChain.Trips.Sort( TripComparer );
                    addedChains.Add( connectingTripChain.TripChain );
                    allTripChains.Add( clonedChain );
                }
            }

            //adding all regular trip chains
            foreach ( ITripChain chain in person.TripChains )
            {
                if ( !addedChains.Contains( chain ) )
                {
                    allTripChains.Add( chain.DeepClone() );
                }
            }
            return allTripChains;
        }

        /// <summary>
        /// Gets the number of vehicles available in the household for the given timespan
        /// </summary>
        /// <param name="h"></param>
        /// <param name="span"></param>
        /// <param name="vehicleType"></param>
        /// <param name="includeAuxTripChains"></param>
        /// <returns></returns>
        public static int NumberOfVehicleAvailable(this ITashaHousehold h, TashaTimeSpan span, IVehicleType vehicleType, bool includeAuxTripChains)
        {
            Dictionary<TashaTimeSpan, int> availabilities = h.FindVehicleAvailabilites( vehicleType, includeAuxTripChains );
            var vehicles = h.Vehicles;
            int available = 0;
            for ( int i = 0; i < vehicles.Length; i++ )
            {
                if ( vehicles[i].VehicleType == vehicleType )
                {
                    available++;
                }
            }
            foreach ( var a in availabilities )
            {
                if ( ( a.Key.Start < span.End ) && ( a.Key.End > span.Start ) )
                {
                    // this is strictly less than since we want the min of the vehicles
                    if ( a.Value < available )
                    {
                        available = a.Value;
                    }
                }
            }
            return available;
        }

        public static int NumberOfVehicleAvailable(List<ITripChain> tripChains, int numVehicles, IVehicleType vehicleType, TashaTimeSpan span)
        {
            Dictionary<TashaTimeSpan, int> availabilities = FindVehicleAvailabilites( tripChains, numVehicles );
            int available = numVehicles;
            foreach ( var a in availabilities )
            {
                if ( ( a.Key.Start < span.End ) && ( a.Key.End > span.Start ) )
                {
                    if ( a.Value < available )
                    {
                        available = a.Value;
                    }
                }
            }
            return available;
        }

        /// <summary>
        /// Cleans auxtrip chains and attached variables
        /// </summary>
        /// <param name="h"></param>
        public static void Reset(this ITashaHousehold h)
        {
            foreach ( var p in h.Persons )
            {
                if ( p.AuxTripChains == null )
                {
                    p.AuxTripChains = new List<ITripChain>();
                }
                else
                {
                    p.AuxTripChains.Clear();
                }
                foreach ( var tc in p.TripChains )
                {
                    foreach ( var t in tc.Trips )
                    {
                        t.Mode = null;
                    }
                }
            }
        }

        public static bool VehicleAvailableInTimeSpan(Dictionary<TashaTimeSpan, int> availabilites, TashaTimeSpan span, int available)
        {
            foreach ( var a in availabilites )
            {
                if ( ( a.Key.Start < span.End ) && ( a.Key.End > span.Start ) )
                {
                    if ( a.Value < available )
                    {
                        available = a.Value;
                    }
                }
            }
            return available > 0;
        }

        /// <summary>
        /// Returns a list of Timespans and the associated number of vehicles in that time span
        /// </summary>
        /// <param name="h"></param>
        /// <param name="vehicleType"></param>
        /// <param name="aux"></param>
        /// <returns></returns>
        private static Dictionary<TashaTimeSpan, int> FindVehicleAvailabilitesHelper(ITashaHousehold h, IVehicleType vehicleType, bool aux)
        {
            List<ITripChain> allTripChains;
            if ( aux )
            {
                allTripChains = h.AllTripChainsWithAuxThatUseVehicle( vehicleType );
            }
            else
            {
                allTripChains = h.AllTripChainsThatUseVehicle( vehicleType );
            }
            int vehiclesAvailable = 0;
            var vehicles = h.Vehicles;
            for ( int i = 0; i < vehicles.Length; i++ )
            {
                if ( vehicles[i].VehicleType == vehicleType )
                {
                    vehiclesAvailable++;
                }
            }
            return FindVehicleAvailabilites( allTripChains, vehiclesAvailable );
        }
    }

    internal class TripComparer : IComparer<ITrip>
    {
        public int Compare(ITrip x, ITrip y)
        {
            return (int)( ( x.ActivityStartTime.ToMinutes() - y.ActivityStartTime.ToMinutes() ) );
        }
    }
}