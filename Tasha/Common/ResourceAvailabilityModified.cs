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
using Tasha.ModeChoice;
using XTMF;

namespace Tasha.Common
{
    public class ResourceAvailabilityModified : IResourceAvailability
    {
        #region IResourceAvailability Members

        [RunParameter( "Random Seed", 12345, "The random seed to use for creating a normal distribution" )]
        public int RandomSeed;

        [RootModule]
        public ITashaRuntime TashaRuntime;

        /// <summary>
        /// A Random number generator
        /// for the given thread
        /// </summary>
        [ThreadStatic]
        private static Random Rand;

        public enum AdjustmentType
        {
            Passenger,
            Driver,
            Compromise
        }

        public bool AssignPossibleDrivers(ITrip trip, ISharedMode mode)
        {
            //if not a joint trip determine if its a pick up or drop off
            if ( !trip.TripChain.JointTrip )
            {
                bool dropOff;
                ITripChain auxiliaryTripChain;
                bool success;
                //if this trip is a pick up ... then create the appropriate trip chain (and go back home trip)
                if ( isPickUpTrip( trip ) )
                {
                    auxiliaryTripChain = CreatePickUpTrip( trip, mode, out success );
                    dropOff = false;
                }//if this is a drop off trip ... then create the appropriate trip chain (and go back home trip)
                else if ( isDropOffTrip( trip ) )
                {
                    auxiliaryTripChain = CreateDropOffTrip( trip, mode, out success );
                    dropOff = true;
                }
                else
                {
                    return false;
                }
                if ( !success )
                {
                    return false;
                }
                success = false;
                if ( dropOff )
                {
                    if ( AssignFacilitateAndContinueTripChains( trip.TripChain.Person.Household, mode, trip, ( (Time)trip.GetVariable( "MaxDriverTimeThreshold" ) ).Minutes ) )
                    {
                        success = true;
                    }
                }
                else
                {
                    if ( AssignPickUpAndReturnTripChains( trip.TripChain.Person.Household, mode, trip, ( (Time)trip.GetVariable( "MaxDriverTimeThreshold" ) ).Minutes ) )
                    {
                        success = true;
                    }
                }
                if ( AssignGoToAndReturnTripChains( auxiliaryTripChain, trip.TripChain.Person.Household, mode.RequiresVehicle, trip, mode ) )
                {
                    success = true;
                }
                return success;
            }
            else
            {
                //is a joint trip
                var jointTripChains = trip.TripChain.JointTripChains;
                for ( int i = 0; i < jointTripChains.Count; i++ )
                {
                    for ( int j = 0; j < jointTripChains[i].Trips.Count; j++ )
                    {
                        var t = jointTripChains[i].Trips[j];
                        if ( t.Mode.NonPersonalVehicle == false )
                        {
                            //this is a possible driver
                            //save the rideshare data
                            trip.SharedModeDriver = t.TripChain.Person;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Calculates the U of the facilitated trip
        /// </summary>
        /// <param name="tripChain"></param>
        /// <param name="oldU"></param>
        /// <param name="newU"></param>
        public void CalculateU(ITripChain tripChain, out double oldU, out double newU)
        {
            ITrip facilitatedTrip = (ITrip)tripChain["FacilitateTrip"];
            ISharedMode facilitatedTripMode = (ISharedMode)tripChain["SharedMode"];
            ITrip connectingTrip = tripChain["ConnectingChain"] as ITrip;

            //the mode data for the facilitated trip
            ModeData facilitatedTripData = ModeData.Get( facilitatedTrip );
            int indexOfPass = TashaRuntime.GetIndexOfMode( facilitatedTripMode );
            double UofAuxiliaryTrip = CalculateUofAuxTrip( tripChain );
            facilitatedTripData.V[indexOfPass] = facilitatedTripMode.CalculateV( facilitatedTrip );

            newU = facilitatedTripData.U( indexOfPass )
                + UofAuxiliaryTrip;
            if ( facilitatedTrip.Mode == null )
            {
                // if there is no other way
                oldU = float.NegativeInfinity;
            }
            else
            {
                if ( connectingTrip == null )
                {
                    oldU = facilitatedTripData.U( TashaRuntime.GetIndexOfMode( facilitatedTrip.Mode ) );
                }
                else
                {
                    ModeData connectingTripData = ModeData.Get( connectingTrip );
                    oldU = facilitatedTripData.U( TashaRuntime.GetIndexOfMode( facilitatedTrip.Mode ) )
                        + connectingTripData.U( TashaRuntime.GetIndexOfMode( connectingTrip.Mode ) );
                }
            }
        }

        public double GetNormal()
        {
            double sum = 0;
            if ( Rand == null )
            {
                Rand = new Random( this.RandomSeed );
            }
            for ( int i = 0; i < 12; i++ )
            {
                // There is 1 Rand per thread
                sum += Rand.NextDouble();
            }
            sum = ( sum - 6 );
            return sum;
        }

        /// <summary>
        /// Gets if the requested vehicle is available (Excluding auxiliary trips)
        /// </summary>
        /// <param name="veqType"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <param name="hh"></param>
        /// <returns></returns>
        public int numVehiclesAvailable(IVehicleType veqType, Time start, Time end, ITashaHousehold hh)
        {
            return ( hh.NumberOfVehicleAvailable( new TashaTimeSpan( start, end ), veqType, false ) );
        }

        /// <summary>
        /// Adds the auxiliary trip chain to the person if its Utility is greater than not including it
        /// </summary>
        /// <param name="driver">the driver</param>
        /// <param name="passenger">the passenger</param>
        /// <param name="trip1">aux trip 1</param>
        /// <param name="trip2">aux trip 2</param>
        /// <param name="facilitateTrip">the trip being facilitated</param>
        /// <param name="mode">the shared mode to assign</param>
        /// <param name="dropoff">is this a dropoff or pickup aux trip chain?</param>
        /// <param name="connectingTrip">what is the chain this aux trip chain connects to if any?</param>
        private void AddAuxTripChain(ITashaPerson driver, ITashaPerson passenger, ITrip trip1, ITrip trip2, ITrip facilitateTrip, ISharedMode mode, bool dropoff, ITrip connectingTrip)
        {
            ITripChain auxTripChain = new AuxiliaryTripChain();
            auxTripChain.Attach( "SharedMode", mode );
            auxTripChain.Attach( "FacilitateTrip", facilitateTrip );
            trip1.TripChain = auxTripChain;
            trip2.TripChain = auxTripChain;
            auxTripChain.Trips.Add( trip1 );
            auxTripChain.Trips.Add( trip2 );
            auxTripChain.Person = driver;
            facilitateTrip.Attach( "isDriver", false );
            if ( dropoff )
            {
                trip1.Passengers.Add( passenger );
                trip1.Attach( "isDriver", true );
            }
            else
            {
                trip2.Passengers.Add( passenger );
                trip2.Attach( "isDriver", true );
            }
            if ( connectingTrip != null )
            {
                auxTripChain.Attach( "ConnectingChain", connectingTrip );
            }
            if ( dropoff )
            {
                if ( connectingTrip == null )
                {
                    auxTripChain.Attach( "Purpose", Activity.DropoffAndReturn );
                    trip1.Purpose = Activity.FacilitatePassenger;
                    trip2.Purpose = Activity.Home;
                }
                else if ( connectingTrip != null )
                {
                    auxTripChain.Attach( "Purpose", Activity.Dropoff );
                    auxTripChain.Attach( "OriginalPurpose", connectingTrip.Purpose );
                    trip1.Purpose = Activity.FacilitatePassenger;
                    trip2.Purpose = connectingTrip.Purpose;

                    //transfering feasible transit stations and such
                    foreach ( var key in connectingTrip.Keys )
                    {
                        trip1.Attach( key, connectingTrip[key] );
                    }
                }
            }
            else
            {
                if ( connectingTrip == null )
                {
                    auxTripChain.Attach( "Purpose", Activity.PickupAndReturn );
                    trip1.Purpose = Activity.FacilitatePassenger;
                    trip2.Purpose = Activity.Home;
                }
                else if ( connectingTrip != null )
                {
                    ///TODO: Look into this again
                    auxTripChain.Attach( "Purpose", Activity.Pickup );
                    auxTripChain.Attach( "OriginalPurpose", connectingTrip.Purpose );
                    trip1.Purpose = Activity.FacilitatePassenger;
                    trip2.Purpose = Activity.Home;

                    //transfering feasible transit stations and such
                    foreach ( var key in connectingTrip.Keys )
                    {
                        trip2.Attach( key, connectingTrip[key] );
                    }
                }
            }
            double oldU, newU;
            CalculateU( auxTripChain, out oldU, out newU );
            if ( double.IsNegativeInfinity( oldU ) | ( newU > oldU ) )
            {
                driver.AuxTripChains.Add( auxTripChain );
                auxTripChain.Attach( "FacilitateTripMode", mode );
            }
        }

        private bool AssignFacilitateAndContinueTripChains(ITashaHousehold h, ISharedMode mode, ITrip facilitateTrip, int maxWaitThreshold)
        {
            bool success = false;
            for ( int pIt = 0; pIt < h.Persons.Length; pIt++ )
            {
                var p = h.Persons[pIt];
                //is this person the same person as the trip we are looking to facilitate?
                //can this person use the specified vehicle?
                if ( p == facilitateTrip.TripChain.Person || !mode.RequiresVehicle.CanUse( p ) )
                {
                    continue;
                }
                var numberOfTripChains = p.TripChains.Count;
                for ( int i = 0; i < numberOfTripChains; i++ )
                {
                    ITrip originToIntermediatePoint;
                    ITrip intermediateToDestination;
                    //if this trip chain is part of a joint trip chain then it can't be used
                    if ( p.TripChains[i].JointTrip )
                    {
                        continue;
                    }
                    //look for the first trip chain with a start time after the passengers start time
                    //determine if it is feasible
                    if ( p.TripChains[i].Trips[0].ActivityStartTime >= facilitateTrip.ActivityStartTime )
                    {
                        if ( i == 0 ) // first trip of the day...
                        {
                            //trip uses vehicle needed for specified shared mode
                            if ( p.TripChains[i].requiresVehicle.Contains( mode.RequiresVehicle ) )
                            {
                                originToIntermediatePoint = AuxiliaryTrip.MakeAuxiliaryTrip( h.HomeZone, facilitateTrip.DestinationZone, mode, facilitateTrip.ActivityStartTime );
                                //travel time from the facilitated trips destination to the trips destination
                                Time travelTime =
                                    p.TripChains[i].Trips[0].Mode.TravelTime( facilitateTrip.DestinationZone,
                                                                                        p.TripChains[i].Trips[0].DestinationZone, p.TripChains[i].Trips[0].TripStartTime );
                                //new start time is drop offs start time plus additional time to get to destination
                                Time newStartTime = travelTime + originToIntermediatePoint.ActivityStartTime;
                                intermediateToDestination = AuxiliaryTrip.MakeAuxiliaryTrip( facilitateTrip.DestinationZone,
                                    p.TripChains[i].Trips[0].DestinationZone,
                                    p.TripChains[i].Trips[0].Mode,
                                    newStartTime );
                                intermediateToDestination.TripChain = p.TripChains[i];
                                //if time between drop off and first trip is too long it is invalid
                                if ( !intermediateToDestination.Mode.Feasible( intermediateToDestination ) ||
                                    !WithinWaitThreshold( intermediateToDestination.ActivityStartTime,
                                                        p.TripChains[i].Trips[0].ActivityStartTime,
                                                        maxWaitThreshold ) )
                                {
                                    continue;
                                }
                                AddAuxTripChain( p, facilitateTrip.TripChain.Person,
                                    originToIntermediatePoint, intermediateToDestination,
                                    facilitateTrip, mode, true, p.TripChains[i].Trips[0] );
                                success = true;
                            }
                        }
                        else
                        {
                            if ( p.TripChains[i].TripChainRequiresPV )
                            {
                                //determine if they have been at home enough long enough
                                //to allow for total driving time and to get to their destination
                                Time homeTime = p.TripChains[i - 1].EndTime;
                                originToIntermediatePoint = AuxiliaryTrip.MakeAuxiliaryTrip( h.HomeZone, facilitateTrip.DestinationZone, mode, facilitateTrip.ActivityStartTime );
                                Time travelTime = p.TripChains[i].Trips[0].Mode.TravelTime( facilitateTrip.DestinationZone,
                                                                                        p.TripChains[i].Trips[0].DestinationZone, p.TripChains[i].Trips[0].TripStartTime );
                                Time newStartTime = travelTime + originToIntermediatePoint.ActivityStartTime;
                                intermediateToDestination = AuxiliaryTrip.MakeAuxiliaryTrip( facilitateTrip.DestinationZone,
                                    p.TripChains[i].Trips[0].DestinationZone,
                                    p.TripChains[i].Trips[0].Mode,
                                    newStartTime );
                                intermediateToDestination.TripChain = p.TripChains[i];
                                if ( intermediateToDestination.TravelTime.ToFloat() == 0f
                                    || originToIntermediatePoint.TravelTime.ToFloat() == 0f ) continue; //travel times do not exist; skip
                                if ( originToIntermediatePoint.TripStartTime > ( homeTime )
                                   && WithinWaitThreshold( intermediateToDestination.ActivityStartTime,
                                                          p.TripChains[i].Trips[0].ActivityStartTime,
                                                          maxWaitThreshold ) )
                                {
                                    if ( !intermediateToDestination.Mode.Feasible( intermediateToDestination ) )
                                    {
                                        continue;
                                    }
                                    //adding aux trip chain to driver
                                    AddAuxTripChain( p, facilitateTrip.TripChain.Person,
                                        originToIntermediatePoint, intermediateToDestination,
                                        facilitateTrip, mode, true, p.TripChains[i].Trips[0] );
                                    success = true;
                                }
                            }
                            else
                            {
                                //cant drive.. since no personal vehicle
                                break;
                            }
                        }
                    }
                }
            }
            return success;
        }

        private bool AssignGoToAndReturnTripChains(ITripChain auxiliaryTripChain, ITashaHousehold household, IVehicleType vehicleType, ITrip trip, ISharedMode mode)
        {
            if ( numVehiclesAvailable( vehicleType, auxiliaryTripChain.StartTime, auxiliaryTripChain.EndTime, household ) == 0 )
                return false;

            bool success = false;

            bool conflict;
            foreach ( var p in household.Persons )
            {
                if ( !vehicleType.CanUse( p ) )
                {
                    continue;
                }
                conflict = false;
                foreach ( var tc in p.TripChains )
                {
                    if ( tc.StartTime <= auxiliaryTripChain.EndTime && tc.EndTime >= auxiliaryTripChain.StartTime )
                    {
                        conflict = true;
                        break; // go onto next person
                    }
                }

                if ( !conflict )
                {
                    //this person has no conflict so give him an aux trip chain
                    AddAuxTripChain( p, trip.TripChain.Person, copyTrip( auxiliaryTripChain.Trips[0] ), copyTrip( auxiliaryTripChain.Trips[1] ), trip, mode, isDropOffTrip( trip ), null );
                    success = true;
                }
            }

            return success;
        }

        private bool AssignPickUpAndReturnTripChains(ITashaHousehold h, ISharedMode mode, ITrip facilitateTrip, int maxWaitThreshold)
        {
            bool success = false;
            for ( int i = 0; i < h.Persons.Length; i++ )
            {
                var p = h.Persons[i];
                if ( p == facilitateTrip.TripChain.Person || !mode.RequiresVehicle.CanUse( p ) )
                {
                    continue;
                }
                foreach ( var tc in p.TripChains )
                {
                    if ( tc.requiresVehicle.Contains( mode.RequiresVehicle ) && tc.Trips[tc.Trips.Count - 1].TripStartTime < facilitateTrip.ActivityStartTime )
                    {
                        ITrip lastTrip = tc.Trips[tc.Trips.Count - 1];
                        ITrip originToIntermediatePoint = AuxiliaryTrip.MakeAuxiliaryTrip( lastTrip.OriginalZone,
                                                                            facilitateTrip.OriginalZone,
                                                                            lastTrip.Mode,
                                                                            lastTrip.ActivityStartTime );
                        originToIntermediatePoint.TripChain = tc;
                        ITrip intermediateToDestination = AuxiliaryTrip.MakeAuxiliaryTrip( facilitateTrip.OriginalZone,
                                                                            facilitateTrip.DestinationZone,
                                                                            mode,
                                                                            facilitateTrip.ActivityStartTime );
                        intermediateToDestination.TripChain = tc;
                        Time newEndTime = facilitateTrip.TripStartTime + intermediateToDestination.TravelTime;
                        if ( !WithinWaitThreshold( newEndTime,
                                                 tc.EndTime, maxWaitThreshold ) )
                        {
                            continue;
                        }
                        //not the last trip: check for overlap with next tc
                        if ( !( p.TripChains[p.TripChains.Count - 1] == tc ) )
                        {
                            //last chain
                            if ( newEndTime > p.TripChains[p.TripChains.IndexOf( tc ) + 1].StartTime )
                            {
                                continue;
                            }
                        }
                        //check feasibility
                        if ( !originToIntermediatePoint.Mode.Feasible( originToIntermediatePoint ) )
                        {
                            continue;
                        }
                        //adding aux chain
                        AddAuxTripChain( p, facilitateTrip.TripChain.Person,
                                   originToIntermediatePoint, intermediateToDestination,
                                   facilitateTrip, mode, false, lastTrip );

                        success = true;
                    }
                }
            }
            return success;
        }

        private double CalculateUofAuxTrip(ITripChain currentTripChain)
        {
            double U = 0;
            for ( int i = 0; i < currentTripChain.Trips.Count; i++ )
            {
                var trip = currentTripChain.Trips[i];
                ModeData md = ModeData.MakeModeData();
                md.Store( trip );
                int indexOfMode = TashaRuntime.GetIndexOfMode( trip.Mode );
                md.V[indexOfMode] = trip.Mode.CalculateV( trip );
                md.Error[indexOfMode] = this.GetNormal();
                U += md.U( indexOfMode );
            }
            currentTripChain.Attach( "U", U );
            return U;
        }

        private ITrip copyTrip(ITrip trip)
        {
            return AuxiliaryTrip.MakeAuxiliaryTrip( trip.OriginalZone, trip.DestinationZone, trip.Mode, trip.ActivityStartTime );
        }

        private ITripChain CreateDropOffTrip(ITrip trip, ISharedMode mode, out bool success)
        {
            ITripChain auxTripChain = new AuxiliaryTripChain();

            //go to trip
            ITrip goToTrip = AuxiliaryTrip.MakeAuxiliaryTrip( trip.TripChain.Person.Household.HomeZone, trip.DestinationZone, mode, trip.ActivityStartTime );

            //End Time of return trip
            Time travelTimeToHome = mode.TravelTime( trip.DestinationZone, trip.TripChain.Person.Household.HomeZone, trip.TripStartTime );
            Time tripEndTime = trip.ActivityStartTime + travelTimeToHome;

            //return home trip
            ITrip returnHomeTrip = AuxiliaryTrip.MakeAuxiliaryTrip( trip.DestinationZone, trip.TripChain.Person.Household.HomeZone, mode.AssociatedMode, tripEndTime );

            //travel times generated
            success = returnHomeTrip.TravelTime > Time.Zero && goToTrip.TravelTime > Time.Zero;

            auxTripChain.Trips.Add( goToTrip );
            auxTripChain.Trips.Add( returnHomeTrip );

            return auxTripChain;
        }

        private ITripChain CreatePickUpTrip(ITrip trip, ISharedMode mode, out bool success)
        {
            ITripChain auxTripChain = new AuxiliaryTripChain();

            //go to trip
            ITrip goToTrip = AuxiliaryTrip.MakeAuxiliaryTrip( trip.OriginalZone, trip.TripChain.Person.Household.HomeZone, mode.AssociatedMode, trip.ActivityStartTime );

            //End Time of return trip
            Time travelTimeToHome = mode.TravelTime( trip.OriginalZone, trip.TripChain.Person.Household.HomeZone, trip.TripStartTime );
            Time tripEndTime = trip.ActivityStartTime + travelTimeToHome;

            //return home trip
            ITrip returnHomeTrip = AuxiliaryTrip.MakeAuxiliaryTrip( trip.TripChain.Person.Household.HomeZone, trip.OriginalZone, mode, tripEndTime );

            //travel times generated
            success = returnHomeTrip.TravelTime > Time.Zero && goToTrip.TravelTime > Time.Zero;

            auxTripChain.Trips.Add( goToTrip );
            auxTripChain.Trips.Add( returnHomeTrip );

            return auxTripChain;
        }

        private bool isDropOffTrip(ITrip trip)
        {
            return trip.TripChain.Trips[0] == trip;// trip.OriginalZone.ZoneNumber == trip.TripChain.Person.Household.HomeZone.ZoneNumber;
        }

        private bool isPickUpTrip(ITrip trip)
        {
            return ( trip.TripChain.Trips[0] != trip ) && trip.DestinationZone.ZoneNumber == trip.TripChain.Person.Household.HomeZone.ZoneNumber;
        }

        private bool OffsetActivityStartTime(ITrip driverTrip,
                        ITrip passengerTrip,
                        ISharedMode sharedMode,
                        bool pickUp,
                        int driverWaitThreshold,
                        int passengerWaitThreshold,
                        AdjustmentType adjustmentType,
                        out Time newDriverStartTime,
                        out Time newPassengerStartTime)
        {
            newPassengerStartTime = Time.Zero;
            newDriverStartTime = Time.Zero;

            if ( adjustmentType == AdjustmentType.Driver )
            {
                Time startTime = passengerTrip.ActivityStartTime;
                newDriverStartTime = startTime + driverTrip.Mode.TravelTime( passengerTrip.DestinationZone, driverTrip.DestinationZone, driverTrip.TripStartTime );
                newPassengerStartTime = startTime;

                return WithinWaitThreshold( newDriverStartTime, driverTrip.ActivityStartTime, driverWaitThreshold );
            }
            else if ( adjustmentType == AdjustmentType.Passenger )
            {
                Time toIntermediatefTime;
                Time toDestinationfTime;
                //determining travel times given whether its a pickup or drop-off trip
                if ( pickUp )
                {
                    toIntermediatefTime = driverTrip.Mode.TravelTime( driverTrip.OriginalZone, passengerTrip.OriginalZone, passengerTrip.TripStartTime );
                    toDestinationfTime = sharedMode.TravelTime( passengerTrip.OriginalZone, passengerTrip.DestinationZone, passengerTrip.TripStartTime );
                }
                else
                {
                    toIntermediatefTime = sharedMode.TravelTime( driverTrip.OriginalZone, passengerTrip.DestinationZone, passengerTrip.TripStartTime );
                    toDestinationfTime = driverTrip.Mode.TravelTime( passengerTrip.DestinationZone, driverTrip.DestinationZone, driverTrip.TripStartTime );
                }
                Time toIntermediateTime = toIntermediatefTime;
                Time toDestinationTime = toDestinationfTime;

                newPassengerStartTime = driverTrip.ActivityStartTime - toDestinationTime;
                newDriverStartTime = driverTrip.ActivityStartTime;

                return WithinWaitThreshold( newPassengerStartTime, passengerTrip.ActivityStartTime, passengerWaitThreshold );
            }
            else if ( adjustmentType == AdjustmentType.Compromise )
            {
                throw new NotImplementedException( "AdjustmentType.Compromise not implemented" );
            }

            return false;
        }

        private bool WithinWaitThreshold(Time start, Time end, int maxWaitThreshold)
        {
            return ( end - start ) < Time.FromMinutes( maxWaitThreshold );
        }

        #endregion IResourceAvailability Members

        public string Name
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
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}