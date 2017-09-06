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
using System.Threading;
using Datastructure;
using XTMF;

namespace TMG.GTAModel.V2.Modes
{
    public class GoAccessStation : IStationMode
    {
        [RunParameter( "Access", true, "Is this mode in access mode or egress mode?" )]
        public bool Access;

        [RunParameter( "Access Cost", 0f, "The cost of travelling from the origin to the access station." )]
        public float AccessCost;

        [RunParameter( "Access IVTT", 0f, "The factor to apply to the general time of travel." )]
        public float AccessInVehicleTravelTime;

        [RunParameter( "Access Network Name", "Auto", "The name of the network to use to get to the interchange." )]
        public string AccessModeName;

        [RunParameter( "Closest", 1.4437f, "The constant to be added if we are the closest station to the origin." )]
        public float Closest;

        [RunParameter( "Closest Distance", 0f, "The factor to apply to the distance if this is the closest station between the origin and this station." )]
        public float ClosestDistance;

        [Parameter( "Compute Egress Station", true, "Compute the station to egress to?" )]
        public bool ComputeEgressStation;

        [RunParameter( "Constant", 0.0f, "A base constant for the utility." )]
        public float Constant;

        [RunParameter( "Cost", 0f, "The factor applied to the cost after access." )]
        public float CostFactor;

        [RunParameter( "Egress Network Name", "Transit", "The name of the network to use after going to the egress zone." )]
        public string EgressNetworkName;

        public float EgressWaitFactor;

        public float EgressWalkFactor;

        [DoNotAutomate]
        public INetworkData First;

        [DoNotAutomate]
        public ITripComponentData FirstComponent;

        public SparseArray<float> FreeTransfers;

        [RunParameter( "IVTT", 0f, "The factor to apply to the general time of travel." )]
        public float InVehicleTravelTime;

        public int LineNumber;

        [RunParameter( "Log Parking Factor", 0f, "The factor applied to the log of the number of parking spots." )]
        public float LogParkingFactor;

        [RunParameter( "Max Access To Destination Time", 150f, "The maximum time in minutes that going from an access station to the destination." )]
        public float MaxAccessToDestinationTime;

        public SparseTwinIndex<float> NumberOfTrains;

        [ParentModel]
        public GoAccessMode Parent;

        [RunParameter( "Primary Network Name", "Transit", "The name of the network to use after the interchange." )]
        public string PrimaryModeName;

        [RootModule]
        public I4StepModel Root;

        [DoNotAutomate]
        public ITripComponentData Second;

        [RunParameter( "All Station Zones", "7000", typeof( RangeSet ), "The station numbers to check to make sure that we are the closest one." )]
        public RangeSet StationRanges;

        [DoNotAutomate]
        public ITripComponentData Third;

        [RunParameter( "Trains Factor", 0f, "The factor to apply to the number of trains the occure during the peak period." )]
        public float TrainsFactor;

        [RunParameter( "Wait Time", 0f, "The factor to apply to the wait time." )]
        public float WaitTime;

        [RunParameter( "Walk Time", 0f, "The factor to apply to the general time of travel." )]
        public float WalkTime;

        internal SparseArray<bool> ClosestZone;

        internal SparseArray<EgressZoneChoice> EgressChoiceCache;

        internal IZone InterchangeZone;

        private int _Parking;

        private bool CacheLoaded;

        private bool LocalTransitCacheLoaded;

        private float LogOfParking;

        [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
        public float CurrentlyFeasible { get; set; }

        [RunParameter( "Mode Name", "DAS 7000", "The name of this mixed mode option" )]
        public string ModeName
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public string NetworkType
        {
            get { return null; }
        }

        public bool NonPersonalVehicle
        {
            get { return true; }
        }

        [RunParameter( "Parking Spots", 0, "The number of parking spots for this station." )]
        public int Parking
        {
            get
            {
                return _Parking;
            }

            set
            {
                _Parking = value;
                LogOfParking = value <= 0 ? float.NegativeInfinity : (float)Math.Log( Parking );
            }
        }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        [RunParameter( "Interchange Zone", 7000, "The zone number to use as the point of interchange." )]
        public int StationZone { get; set; }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            CheckInterchangeZone();
            var zoneArray = Root.ZoneSystem.ZoneArray;
            var flatOrigin = zoneArray.GetFlatIndex( origin.ZoneNumber );
            var flatDestination = zoneArray.GetFlatIndex( destination.ZoneNumber );
            var flatInterchange = zoneArray.GetFlatIndex( InterchangeZone.ZoneNumber );
            var egressZoneChoice = FindEgressStation( flatDestination, time );
            var egress = egressZoneChoice.EgressZone;
            if ( egress == null )
            {
                return float.NaN;
            }
            var flatEgress = zoneArray.GetFlatIndex( egress.ZoneNumber );
            // caluclate all of the terms in all equations first
            float v = TrainsFactor * GetFrequency( flatInterchange, flatEgress );
            if ( ClosestZone.GetFlatData()[flatOrigin] )
            {
                // closest distance is in KM
                v += Closest + ClosestDistance * ( Root.ZoneSystem.Distances.GetFlatData()[flatOrigin][flatInterchange] / 1000 );
            }

            if ( !First.ValidOd( flatOrigin, flatInterchange, time ) )
            {
                return float.NaN;
            }

            // TAG
            if ( FirstComponent != null )
            {
                // Travel Time components
                v += InVehicleTravelTime * ( ( FirstComponent.InVehicleTravelTime( flatOrigin, flatInterchange, time )
                    + Third.InVehicleTravelTime( flatEgress, flatDestination, time ) ).ToMinutes() );
                //Walk Time components
                v += WalkTime * ( FirstComponent.WalkTime( flatOrigin, flatInterchange, time ) 
                    + Third.WalkTime( flatEgress, flatDestination, time ) ).ToMinutes();
                //Wait time components
                v += WaitTime * ( ( FirstComponent.WaitTime( flatOrigin, flatInterchange, time )
                    + Third.WaitTime( flatEgress, flatDestination, time ) ).ToMinutes() + 5 );
                // FreeTransfers is 0 IFF the transfer is free
                if ( FreeTransfers[destination.PlanningDistrict] > 0 )
                {
                    v += CostFactor * Third.TravelCost( flatEgress, flatDestination, time );
                }
                if ( FreeTransfers[origin.PlanningDistrict] > 0 )
                {
                    v += CostFactor * FirstComponent.TravelCost( flatOrigin, flatInterchange, time );
                }
                v += CostFactor * Second.TravelCost( flatInterchange, flatEgress, time );
            }
            //DAG
            else
            {
                v += LogParkingFactor * LogOfParking;
                // Travel Time components
                v += AccessInVehicleTravelTime * First.TravelTime( flatOrigin, flatInterchange, time ).ToMinutes();
                // FreeTransfers is 0 IFF the transfer is free
                if ( FreeTransfers[destination.PlanningDistrict] > 0 )
                {
                    v += CostFactor * ( Third.TravelCost( flatEgress, flatDestination, time ) );
                }
                v += AccessCost * First.TravelCost( flatOrigin, flatInterchange, time );
                v += CostFactor * ( Second.TravelCost( flatInterchange, flatEgress, time ) );
            }
            return v;
        }

        public float Cost(IZone origin, IZone destination, Time time)
        {
            CheckInterchangeZone();
            return First.TravelCost( origin, InterchangeZone, time ) + Second.TravelCost( origin, InterchangeZone, time );
        }

        public void DumpCaches()
        {
            LocalTransitCacheLoaded = false;
            Thread.MemoryBarrier();
            lock ( this )
            {
                EgressChoiceCache = Root.ZoneSystem.ZoneArray.CreateSimilarArray<EgressZoneChoice>();
                LocalTransitCacheLoaded = true;
                Thread.MemoryBarrier();
            }
        }

        public bool Feasible(IZone originZone, IZone destinationZone, Time time)
        {
            if ( CurrentlyFeasible <= 0 ) return false;
            CheckInterchangeZone();
            var zoneArray = Root.ZoneSystem.ZoneArray;
            var origin = zoneArray.GetFlatIndex( originZone.ZoneNumber );
            var destination = zoneArray.GetFlatIndex( destinationZone.ZoneNumber );
            var interchange = zoneArray.GetFlatIndex( InterchangeZone.ZoneNumber );
            if (First is ITripComponentData component)
            {
                // make sure that there is a valid walk time if we are walking/transit to the station
                if (component.WalkTime(origin, interchange, time).ToMinutes() <= 0)
                {
                    return false;
                }
            }
            return ItermediateZoneCloserThanDestination( origin, destination, interchange );
        }

        public bool RuntimeValidation(ref string error)
        {
            foreach ( var network in Root.NetworkData )
            {
                if ( network.Name == AccessModeName )
                {
                    First = network;
                    FirstComponent = network as ITripComponentData;
                }

                if ( network.Name == PrimaryModeName )
                {
                    var temp = network as ITripComponentData;
                    Second = temp ?? Second;
                }

                if ( network.NetworkType == EgressNetworkName )
                {
                    var temp = network as ITripComponentData;
                    Third = temp ?? Third;
                }
            }
            if ( First == null )
            {
                error = "In '" + Name + "' the name of the access network data type was not found!";
                return false;
            }
            if ( Second == null )
            {
                error = "In '" + Name + "' the name of the primary network data type was not found or does not contain trip component data!";
                return false;
            }
            if ( Third == null && ComputeEgressStation )
            {
                error = "In '" + Name + "' the name of the egress network data type was not found or does not contain trip component data!";
                return false;
            }
            return true;
        }

        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            CheckInterchangeZone();
            return First.TravelTime( origin, InterchangeZone, time ) + Second.TravelTime( InterchangeZone, destination, time );
        }

        private static bool ComputeThird(ITripComponentData data, int flatOrigin, int flatDestination, Time t, float walkTime, float waitTime, out float result)
        {
            data.GetAllData(flatOrigin, flatDestination, t, out Time ivtt, out Time walk, out Time wait, out Time boarding, out float cost);
            var timeWalkingInMinutes = walk.ToMinutes();
            if ( timeWalkingInMinutes <= 0 )
            {
                result = float.PositiveInfinity;
                return false;
            }
            result = timeWalkingInMinutes * walkTime
                    + wait.ToMinutes() * waitTime
                    + ivtt.ToMinutes();
            return true;
        }

        private bool AreWeClosest(IZone origin, SparseArray<IZone> zoneArray, SparseTwinIndex<float> distances)
        {
            var ourDistance = distances[origin.ZoneNumber, InterchangeZone.ZoneNumber];
            foreach ( var range in StationRanges )
            {
                for ( int i = range.Start; i <= range.Stop; i++ )
                {
                    if ( i == StationZone ) continue;
                    var otherZone = zoneArray[i];
                    if ( otherZone == null ) continue;
                    if ( distances[origin.ZoneNumber, otherZone.ZoneNumber] < ourDistance ) return false;
                }
            }
            return true;
        }

        private void CheckInterchangeZone()
        {
            if ( !CacheLoaded )
            {
                lock ( this )
                {
                    Thread.MemoryBarrier();
                    if ( !CacheLoaded )
                    {
                        var zones = Root.ZoneSystem.ZoneArray;
                        var distances = Root.ZoneSystem.Distances;
                        var zone = zones[StationZone];
                        InterchangeZone = zone ?? throw new XTMFRuntimeException(this, "The zone " + StationZone + " does not exist!  Please check the mode '" + ModeName + "!" );
                        ClosestZone = zones.CreateSimilarArray<bool>();
                        var flatZones = zones.GetFlatData();
                        var flatClosest = ClosestZone.GetFlatData();
                        for ( int i = 0; i < flatZones.Length; i++ )
                        {
                            flatClosest[i] = AreWeClosest( flatZones[i], zones, distances );
                        }
                        CacheLoaded = true;
                        Thread.MemoryBarrier();
                    }
                }
            }
        }

        private EgressZoneChoice FindEgressStation(int flatDestination, Time time)
        {
            float bestTime;
            if ( !LocalTransitCacheLoaded )
            {
                lock ( this )
                {
                    Thread.MemoryBarrier();
                    if ( !LocalTransitCacheLoaded )
                    {
                        DumpCaches();
                        Thread.MemoryBarrier();
                    }
                }
            }
            var egressChoice = EgressChoiceCache.GetFlatData()[flatDestination];
            if ( egressChoice != null )
            {
                return egressChoice;
            }
            int bestEgressZone = -1;
            bestTime = float.MaxValue;
            var zones = Root.ZoneSystem.ZoneArray;
            var flatInterchange = zones.GetFlatIndex( StationZone );
            foreach ( var set in StationRanges )
            {
                for ( int i = set.Start; i <= set.Stop; i++ )
                {
                    var flatEgressZone = zones.GetFlatIndex(i);
                    if ( flatEgressZone < 0 )
                    {
                        continue;
                    }
                    if ( flatInterchange == flatEgressZone ) continue;
                    if ( GetEgressTravelTime( flatEgressZone, flatDestination, flatInterchange, time, bestTime, out float tt ) )
                    {
                        if ( tt < bestTime )
                        {
                            bestTime = tt;
                            bestEgressZone = flatEgressZone;
                        }
                    }
                }
            }
            if ( bestEgressZone < 0 )
            {
                return ( EgressChoiceCache.GetFlatData()[flatDestination] = new EgressZoneChoice { EgressZone = null } );
            }
            return ( EgressChoiceCache.GetFlatData()[flatDestination] = new EgressZoneChoice
            {
                            EgressZone = zones.GetFlatData()[bestEgressZone]
                        } );
        }

        private bool GetEgressTravelTime(int flatEgressZone, int flatDestinationZone, int flatInterchangeZone, Time time, float bestTime, out float tt)
        {
            tt = float.MaxValue;
            // make sure that we can actually travel to the end station
            if ( !Second.ValidOd( flatInterchangeZone, flatEgressZone, time ) )
            {
                return false;
            }
            // now that we know it is possible go and get that travel time
            var lineHaul = Second.InVehicleTravelTime( flatInterchangeZone, flatEgressZone, time ).ToMinutes();
            // if the travel time is zero, then this is an invalid option (Use this as a check since it would mean that frequency is not aligned with times)
            if ( lineHaul <= 0 )
            {
                return false;
            }
            if (!Parent.GetEgressUtility(flatEgressZone, flatDestinationZone, time, out float egressUtility))
            {
                return false;
            }
            tt = lineHaul + egressUtility;
            // if we are not already better than the best continue
            if ( tt >= bestTime )
            {
                return false;
            }
            // now make sure that tt is actually smaller than just using transit all way
            float localAllWayTime;
            if ( Third.ValidOd( flatInterchangeZone, flatDestinationZone, time ) )
            {
                if (!ComputeThird(Third, flatInterchangeZone, flatDestinationZone, time, EgressWalkFactor, EgressWaitFactor, out float result))
                {
                    return false;
                }
                localAllWayTime = result;
            }
            else
            {
                localAllWayTime = float.MaxValue;
            }
            return tt < localAllWayTime;
        }

        private float GetFrequency(int flatAccessOrigin, int flatDestination)
        {
            return NumberOfTrains.GetFlatData()[flatAccessOrigin][flatDestination];
        }

        private bool ItermediateZoneCloserThanDestination(int origin, int destination, int flatInt)
        {
            var distances = Root.ZoneSystem.Distances.GetFlatData();
            return distances[origin][flatInt] < distances[origin][destination];
        }

        internal sealed class EgressZoneChoice
        {
            internal IZone EgressZone;
        }
    }
}