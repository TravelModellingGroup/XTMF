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
using XTMF;

namespace TMG.GTAModel.V2.Modes
{
    public class SubwayAccessStation : IStationMode
    {
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

        [RunParameter( "Cost", 0f, "The factor applied to the cost after access." )]
        public float CostFactor;

        [RunParameter( "Egress Network Name", "Transit", "The name of the network to use after going to the egress zone." )]
        public string EgressNetworkName;

        [DoNotAutomate]
        public INetworkData First;

        public float FTTC;

        [RunParameter( "IVTT", 0f, "The factor to apply to the general time of travel." )]
        public float InVehicleTravelTime;

        [RunParameter( "Log Parking Factor", 0f, "The factor applied to the log of the number of parking spots." )]
        public float LogParkingFactor;

        [RunParameter( "Max Access To Destination Time", 150f, "The maximum time in minutes that going from an access station to the destination." )]
        public float MaxAccessToDestinationTime;

        [ParentModel]
        public SubwayAccessMode Parent;

        [RunParameter( "Parking Cost", 0f, "The factor applied to the cost of parking at the access station." )]
        public float ParkingCost;

        [RunParameter( "Access Network Name", "Transit", "The name of the network to use after the interchange." )]
        public string PrimaryModeName;

        [RootModule]
        public I4StepModel Root;

        [DoNotAutomate]
        public ITripComponentData Second;

        [RunParameter( "All Station Zones", "7000", typeof( RangeSet ), "The station numbers to check to make sure that we are the closest one." )]
        public RangeSet StationRanges;

        [RunParameter( "Wait Time", 0f, "The factor to apply to the wait time." )]
        public float WaitTime;

        [RunParameter( "Walk Time", 0f, "The factor to apply to the general time of travel." )]
        public float WalkTime;

        internal SparseArray<bool> ClosestZone;

        internal IZone InterchangeZone;

        private int _Parking;

        private bool CacheLoaded = false;

        private float LogOfParking;

        [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
        public float CurrentlyFeasible { get; set; }

        [RunParameter( "Mode Name", "DAS 6000", "The name of this mixed mode option" )]
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
                LogOfParking = value <= 0 ? float.NegativeInfinity : (float)Math.Log( this.Parking );
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
            var zoneArray = this.Root.ZoneSystem.ZoneArray;
            var flatOrigin = zoneArray.GetFlatIndex( origin.ZoneNumber );
            var flatDestination = zoneArray.GetFlatIndex( destination.ZoneNumber );
            var flatInterchange = zoneArray.GetFlatIndex( InterchangeZone.ZoneNumber );

            // Make sure that this is a valid trip first
            var toDestinationTime = this.Second.InVehicleTravelTime( flatInterchange, flatDestination, time ).ToMinutes();
            if ( toDestinationTime > this.MaxAccessToDestinationTime )
            {
                return float.NaN;
            }

            float v = this.LogParkingFactor * LogOfParking;
            if ( this.ClosestZone.GetFlatData()[flatOrigin] )
            {
                v += this.Closest;
            }

            // calculate this second in case the toDestinationTime is invalid
            // Cost of accessing the station
            v += this.AccessInVehicleTravelTime * this.First.TravelTime( flatOrigin, flatInterchange, time ).ToMinutes()
                + ( this.AccessCost * ( this.First.TravelCost( flatOrigin, flatInterchange, time ) + FTTC ) );

            // Station to Destination time
            v += this.InVehicleTravelTime * toDestinationTime;

            // Walk Time
            v += this.WalkTime * this.Second.WalkTime( flatInterchange, flatDestination, time ).ToMinutes();
            return v;
        }

        public float Cost(IZone origin, IZone destination, Time time)
        {
            CheckInterchangeZone();
            return this.First.TravelCost( origin, InterchangeZone, time ) + this.Second.TravelCost( origin, InterchangeZone, time );
        }

        public bool Feasible(IZone originZone, IZone destinationZone, Time time)
        {
            if ( this.CurrentlyFeasible <= 0 ) return false;
            CheckInterchangeZone();
            var zoneArray = this.Root.ZoneSystem.ZoneArray;
            var origin = zoneArray.GetFlatIndex( originZone.ZoneNumber );
            var destination = zoneArray.GetFlatIndex( destinationZone.ZoneNumber );
            var interchange = zoneArray.GetFlatIndex( InterchangeZone.ZoneNumber );
            var component = this.First as ITripComponentData;
            if ( component != null )
            {
                // make sure that there is a valid walk time if we are walking/transit to the station
                if ( component.WalkTime( origin, interchange, time ).ToMinutes() <= 0 )
                {
                    return false;
                }
            }
            return ItermediateZoneCloserThanDestination( origin, destination, interchange );
        }

        public bool RuntimeValidation(ref string error)
        {
            foreach ( var network in this.Root.NetworkData )
            {
                if ( network.Name == this.AccessModeName )
                {
                    this.First = network;
                }

                if ( network.Name == this.PrimaryModeName )
                {
                    var temp = network as ITripComponentData;
                    this.Second = temp == null ? this.Second : temp;
                }
            }
            if ( this.First == null )
            {
                error = "In '" + this.Name + "' the name of the access network data type was not found!";
                return false;
            }
            else if ( this.Second == null )
            {
                error = "In '" + this.Name + "' the name of the primary network data type was not found or does not contain trip component data!";
                return false;
            }
            return true;
        }

        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            CheckInterchangeZone();
            return this.First.TravelTime( origin, InterchangeZone, time ) + this.Second.TravelTime( InterchangeZone, destination, time );
        }

        private static float ComputeSubV(ITripComponentData data, int flatOrigin, int flatDestination, Time t, float ivttWeight, float walkWeight, float waitWeight, float costWeight)
        {
            Time ivtt, walk, wait, boarding;
            float cost;
            data.GetAllData( flatOrigin, flatDestination, t, out ivtt, out walk, out wait, out boarding, out cost );
            return ivttWeight * ivtt.ToMinutes()
                + walkWeight * walk.ToMinutes()
                + waitWeight * wait.ToMinutes()
                + costWeight * cost;
        }

        private bool AreWeClosest(IZone origin, SparseArray<IZone> zoneArray, SparseTwinIndex<float> distances)
        {
            var ourDistance = distances[origin.ZoneNumber, this.InterchangeZone.ZoneNumber];
            foreach ( var range in this.StationRanges )
            {
                for ( int i = range.Start; i <= range.Stop; i++ )
                {
                    if ( i == this.StationZone ) continue;
                    var otherZone = zoneArray[i];
                    if ( distances[origin.ZoneNumber, otherZone.ZoneNumber] < ourDistance ) return false;
                }
            }
            return true;
        }

        private void CheckInterchangeZone()
        {
            if ( !this.CacheLoaded )
            {
                lock ( this )
                {
                    System.Threading.Thread.MemoryBarrier();
                    if ( !this.CacheLoaded )
                    {
                        var zones = this.Root.ZoneSystem.ZoneArray;
                        var distances = this.Root.ZoneSystem.Distances;
                        var zone = zones[StationZone];
                        if ( zone == null )
                        {
                            throw new XTMFRuntimeException( "The zone " + StationZone + " does not exist!  Please check the mode '" + this.ModeName + "!" );
                        }
                        this.InterchangeZone = zone;
                        this.ClosestZone = zones.CreateSimilarArray<bool>();
                        var flatClosestZone = this.ClosestZone.GetFlatData();
                        var flatZones = zones.GetFlatData();
                        for ( int i = 0; i < flatZones.Length; i++ )
                        {
                            flatClosestZone[i] = AreWeClosest( flatZones[i], zones, distances );
                        }

                        this.CacheLoaded = true;
                        System.Threading.Thread.MemoryBarrier();
                    }
                }
            }
        }

        private bool ItermediateZoneCloserThanDestination(int origin, int destination, int flatInt)
        {
            var distances = this.Root.ZoneSystem.Distances.GetFlatData();
            return distances[origin][flatInt] < distances[origin][destination];
        }
    }
}