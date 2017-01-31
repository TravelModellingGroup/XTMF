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
using System.Collections.Concurrent;
using System.Collections.Generic;
using Datastructure;
using Tasha.Common;
using TMG;
using TMG.Input;
using XTMF;

namespace Tasha.Validation
{
    public class TashaAccessStation : IPreIteration
    {
        [RunParameter( "Auto Network Name", "Auto", "The name of the Auto Network." )]
        public string Auto;

        [DoNotAutomate]
        public INetworkData AutoData;

        [RunParameter( "Bording Time", 0f, "The factor applied to the boarding time." )]
        public float BoardingTime;

        [RunParameter( "Cost", 0f, "The factor applied to the cost after access." )]
        public float CostFactor;

        [RunParameter( "IVTT", 0f, "The factor to apply to the general time of travel." )]
        public float InVehicleTravelTime;

        [RootModule]
        public ITashaRuntime Root;

        [SubModelInformation( Description = "(Origin = Station Number, Destination = Parking Spots, Data = Number Of Trains)", Required = true )]
        public IReadODData<float> StationInformationReader;

        [RunParameter( "Transit Network Name", "Transit", "The name of the Transit Network" )]
        public string Transit;

        [DoNotAutomate]
        public ITripComponentData TransitData;

        [RunParameter( "Wait Time", 0f, "The factor to apply to the wait time." )]
        public float WaitTime;

        [RunParameter( "Walk Time", 0f, "The factor to apply to the general time of travel." )]
        public float WalkTime;

        private ConcurrentDictionary<int, KeyValuePair<int, float>> AgressToDestintion = new ConcurrentDictionary<int, KeyValuePair<int, float>>();
        private Time AM = new Time( "7:00:00" );
        private ConcurrentDictionary<int, EgressZoneChoice> EgressUtils = new ConcurrentDictionary<int, EgressZoneChoice>();
        private Time FF = new Time( "12:00:00" );
        private Time PM = new Time( "17:00:00" );
        private IList<Station> Stations = new List<Station>();

        private IList<Time> timeList = new List<Time>();

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get;
            set;
        }

        public void Execute(int iterationNumber, int totalIterations)
        {
            var zones = this.Root.ZoneSystem.ZoneArray;
            var flatData = zones.GetFlatData();
            timeList.Add( AM ); timeList.Add( PM ); timeList.Add( FF );

            foreach ( var record in this.StationInformationReader.Read() )
            {
                Station currentStation = new Station();
                currentStation.zoneNumber = record.O;
                currentStation.parkingSpots = record.D;
                currentStation.numberOfTrains = record.Data;
                Stations.Add( currentStation );
            }

            for ( int i = 0; i < flatData.Length; i++ )
            {
                EgressStation( flatData[i].ZoneNumber, zones );
            }
        }

        public void Load(int totalIterations)
        {
        }

        public bool RuntimeValidation(ref string error)
        {
            foreach ( var network in this.Root.NetworkData )
            {
                if ( network.NetworkType == Auto )
                {
                    this.AutoData = network;
                }

                else if ( network.NetworkType == Transit )
                {
                    var temp = network as ITripComponentData;
                    this.TransitData = temp == null ? this.TransitData : temp;
                }
            }
            return true;
        }

        internal bool GetEgressUtility(int egressStation, int destinationZone, Time time, out float egressUtility)
        {
            // Make sure that we can get from the egress station to the destination zone at that current point in the day
            if ( !TransitData.ValidOd( egressStation, destinationZone, time ) )
            {
                egressUtility = float.MinValue;
                return false;
            }

            Time ivtt, walk, wait, boarding;
            float cost;

            TransitData.GetAllData( egressStation, destinationZone, time, out ivtt, out walk, out wait, out boarding, out cost );
            egressUtility = ivtt.ToMinutes() + this.WaitTime * wait.ToMinutes() + this.WalkTime * walk.ToMinutes();

            return egressUtility != float.MaxValue;
        }

        private static float ComputeV(ITripComponentData data, int egress, int destination, Time time, float ivttWeight, float walkWeight, float waitWeight, float boardingWeight, float costWeight)
        {
            Time ivtt, walk, wait, boarding;
            float cost;
            data.GetAllData( egress, destination, time, out ivtt, out walk, out wait, out boarding, out cost );
            return ivttWeight * ivtt.ToMinutes()
                + walkWeight * walk.ToMinutes()
                + waitWeight * wait.ToMinutes()
                + boardingWeight * boarding.ToMinutes()
                + costWeight * cost;
        }

        private float CalculateEgressUtility(int egress, int destination, Time time)
        {
            return ComputeV( TransitData, egress, destination, time, this.InVehicleTravelTime, this.WalkTime, this.WaitTime, this.BoardingTime, this.CostFactor );
        }

        // private SparseArray<EgressZoneChoice> EgressChoiceCache;
        private void EgressStation(int flatDest, SparseArray<IZone> zones)
        {
            float bestTime = float.MaxValue;
            Station bestEgress = new Station();
            float travelTime;

            foreach ( var station in this.Stations )
            {
                if ( EgressTravelTime( station.zoneNumber, flatDest, AM, bestTime, out travelTime ) )
                {
                    bestTime = travelTime;
                    bestEgress = station;
                }
            }
            if ( bestEgress == null )
            {
                EgressUtils.TryAdd( flatDest, new EgressZoneChoice() { egressZone = null, EgressUtility = float.NaN } );
            }
            else
            {
                EgressUtils.TryAdd( flatDest, new EgressZoneChoice() { egressZone = zones.GetFlatData()[bestEgress.zoneNumber], EgressUtility = CalculateEgressUtility( bestEgress.zoneNumber, flatDest, AM ) } );
            }
        }

        private bool EgressTravelTime(int egressZone, int destination, Time time, float bestTime, out float travelTime)
        {
            travelTime = float.MaxValue;
            float egressUtility;
            if ( !GetEgressUtility( egressZone, destination, time, out egressUtility ) )
            {
                return false;
            }

            travelTime += egressUtility;

            if ( travelTime >= bestTime )
            {
                return false;
            }

            // In GTAModel it also checks if this travel time is smaller than local transit all the way - but in this case we do not because we split the trip into parts

            return true;
        }

        private sealed class EgressZoneChoice
        {
            internal float EgressUtility;
            internal IZone egressZone;
        }

        private sealed class Station
        {
            internal float numberOfTrains;
            internal int parkingSpots;
            internal int zoneNumber;
        }
    }
}