/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datastructure;
using TMG.Input;
using TMG.Modes;
using XTMF;

namespace TMG.GTAModel.V2.Modes
{
    public class SubwayAccessMode : IStationCollectionMode, IUtilityComponentMode, IIterationSensitive
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

        [RunParameter( "Constant", 0.0f, "A base constant for the utility." )]
        public float Constant;

        [RunParameter( "Cost", 0f, "The factor applied to the cost after access." )]
        public float CostFactor;

        [RunParameter( "Egress Network Name", "Transit", "The name of the network to use after going to the egress zone." )]
        public string EgressNetworkName;

        [SubModelInformation( Description = "An optional test for mode feasibility.", Required = false )]
        public ICalculation<Pair<IZone, IZone>, bool> FeasibilityTest;

        [DoNotAutomate]
        public INetworkData First;

        [DoNotAutomate]
        public INetworkData FirstAlternative;

        [RunParameter( "fttc", 1.71f, "The fair to add for driving to the TTC, in V2 this was 1.71." )]
        public float FareTTC;

        [RunParameter( "General Time", -0.103420f, "The factor to apply to the general time of travel." )]
        public float GeneralTime;

        [RunParameter( "Max Access Stations", 5, "The maximum access stations to consider when computing utility." )]
        public int MaxAccessStations;

        [RunParameter( "Max Access To Destination Time", 150f, "The maximum time in minutes that going from an access station to the destination." )]
        public float MaxAccessToDestinationTime;

        [RunParameter( "Min Access Station LogsumValue", -10f, "The miniumum utility that the logsum of access stations can produce and still have a feasible trip." )]
        public float MinAccessStationLogsumValue;

        [RunParameter( "Parking Cost", 0f, "The factor applied to the cost of parking at the access station." )]
        public float ParkingCost;

        [RunParameter( "ParkingCostAverageFactor", 0f, "The factor to apply to the average parking cost across different access stations." )]
        public float ParkingCostAverageFactor;

        [RunParameter( "Parking Factor", 0.388380f, "The factor applied to the log of the number of parking spots." )]
        public float ParkingFactor;

        [RunParameter( "Primary Network Name", "Transit", "The name of the network to use after the interchange." )]
        public string PrimaryModeName;

        [RunParameter( "Require Parking", true, "Skip stations that do not have a parking spot." )]
        public bool RequireParking;

        [RootModule]
        public I4StepModel Root;

        [DoNotAutomate]
        public ITripComponentData Second;

        [SubModelInformation( Description = "0th entry is zone number, 1st entry is number of parking spots.", Required = true )]
        public IDataLineSource<float[]> StationZoneData;

        [RunParameter( "Wait Time", -0.086483f, "The factor to apply to the wait time." )]
        public float WaitTime;

        [RunParameter( "WaitTimeAverageFactor", 0f, "The factor to apply to the average wait time across different access stations." )]
        public float WaitTimeAverageFactor;

        [RunParameter( "Walk Time", -0.295330f, "The factor to apply to the general time of travel." )]
        public float WalkTime;

        private float _CurrentlyFeasible;

        private SparseTwinIndex<CacheData> Cache;

        private Time CacheTime = new Time { Hours = -1 };

        private SparseArray<int[]> ClosestStations;

        private int LastIteration = -1;

        [RunParameter( "Access", true, "Is this mode in access mode or egress mode?" )]
        public bool Access
        {
            get;
            set;
        }

        [DoNotAutomate]
        public List<SubwayAccessStation> Children { get; set; }

        [RunParameter( "Correlation", 1f, "The correlation between the alternatives.  1 means no correlation, 0 means perfect correlation." )]
        public float Correlation { get; set; }

        [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
        public float CurrentlyFeasible
        {
            get
            {
                return _CurrentlyFeasible;
            }

            set
            {
                _CurrentlyFeasible = value;
                if ( Children != null )
                {
                    foreach ( var child in Children )
                    {
                        child.CurrentlyFeasible = value;
                    }
                }
            }
        }

        [RunParameter( "Mode Name", "DAS", "The name of this mixed mode option" )]
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
            get { return false; }
        }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        [SubModelInformation( Description = "Additional Utility Components, part of the combined value.", Required = false )]
        public List<IUtilityComponent> UtilityComponents { get; set; }

        public float CalculateCombinedV(IZone origin, IZone destination, Time time)
        {
            var v = Constant;
            for ( int i = 0; i < UtilityComponents.Count; i++ )
            {
                v += UtilityComponents[i].CalculateV( origin, destination, time );
            }
            return v;
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            if ( ( LastIteration != Root.CurrentIteration ) | ( time != CacheTime ) )
            {
                RebuildCache( time );
            }
            var data = Cache[origin.ZoneNumber, destination.ZoneNumber];
            if ( data == null )
            {
                int alternatives = 0;
                float averageTotalWaitTime = 0f;
                float averageParkingCost = 0f;
                IZone[] childrenZone = new IZone[MaxAccessStations];
                // make sure we clip the number of possible stations
                float[] childrenV = new float[MaxAccessStations];
                for ( int i = 0; i < MaxAccessStations; i++ )
                {
                    childrenV[i] = float.MinValue;
                    childrenZone[i] = null;
                }
                var lookAt = ClosestStations[origin.ZoneNumber];
                for ( int childIndex = 0; childIndex < lookAt.Length; childIndex++ )
                {
                    var index = lookAt[childIndex];
                    // once we hit an invalid child index we are done
                    if ( index < 0 ) break;
                    var child = Children[index];
                    if ( child.Feasible( origin, destination, time ) )
                    {
                        var localV = child.CalculateV( origin, destination, time );
                        if ( localV < MinAccessStationLogsumValue ) continue;
                        if ( !float.IsNaN( localV ) )
                        {
                            int minChild = 0;
                            // find the option with the lowest value
                            for ( int i = 1; i < childrenV.Length; i++ )
                            {
                                if ( childrenV[i] < childrenV[minChild] )
                                {
                                    minChild = i;
                                }
                            }
                            // replace the least utility with this new station
                            if ( childrenV[minChild] < localV )
                            {
                                childrenV[minChild] = localV;
                                childrenZone[minChild] = child.InterchangeZone;
                            }
                            alternatives++;
                        }
                    }
                }
                if ( alternatives > 0 )
                {
                    float logsum = 0f;
                    // If there are more than 1 alternatives, then we need to actually compute the logsum
                    if ( alternatives > 1 )
                    {
                        for ( int i = 0; ( i < alternatives ) & ( i < childrenV.Length ); i++ )
                        {
                            averageTotalWaitTime += Second.WaitTime( childrenZone[i], destination, time ).ToMinutes();
                            averageParkingCost += childrenZone[i].ParkingCost;
                            logsum += ( childrenV[i] = (float)Math.Exp( childrenV[i] ) );
                        }
                        logsum = (float)Math.Log( logsum );
                    }
                    else
                    {
                        // If there is only one alternative, log of exp is just the same value, so don't bother with all of the additional math
                        averageTotalWaitTime = Second.WaitTime( childrenZone[0], destination, time ).ToMinutes();
                        averageParkingCost = childrenZone[0].ParkingCost;
                        logsum = childrenV[0];
                        childrenV[0] = (float)Math.Exp( childrenV[0] );
                    }
                    data = new CacheData
                    {
                        Feasible = true,
                        AccessUtil = childrenV,
                        AccessZone = childrenZone,
                        Logsum = logsum,
                        AverageParking = averageParkingCost,
                        AverageWait = averageTotalWaitTime,
                        AccessStations = alternatives
                    };
                }
                else
                {
                    data = new CacheData
                    {
                        Feasible = false,
                        Logsum = float.NaN
                    };
                }
                Cache[origin.ZoneNumber, destination.ZoneNumber] = data;
            }
            if ( float.IsNaN( data.Logsum ) )
            {
                return float.NaN;
            }
            return CalculateCombinedV( origin, destination, time )
                + ( Correlation * data.Logsum )
                + ( ( WaitTimeAverageFactor * data.AverageWait
                + ParkingCostAverageFactor * data.AverageParking ) / data.AccessStations );
        }

        public float Cost(IZone origin, IZone destination, Time time)
        {
            return 0f;
        }

        public bool Feasible(IZone origin, IZone destination, Time time)
        {
            if ( CalcDistance( origin, destination ) < 5000f )
            {
                return false;
            }
            if ( FeasibilityTest != null )
            {
                return CurrentlyFeasible > 0 && FeasibilityTest.ProduceResult( new Pair<IZone, IZone>( origin, destination ) );
            }
            return CurrentlyFeasible > 0;
        }

        public Tuple<IZone[], IZone[], float[]> GetSubchoiceSplit(IZone origin, IZone destination, Time time)
        {
            var data = Cache[origin.ZoneNumber, destination.ZoneNumber];
            if ( data == null || data.Feasible == false )
            {
                return null;
            }
            return new Tuple<IZone[], IZone[], float[]>( data.AccessZone, null, data.AccessUtil );
        }

        public void IterationEnding(int iterationNumber, int maxIterations)
        {
        }

        public void IterationStarting(int iterationNumber, int maxIterations)
        {
            if ( iterationNumber == 0 )
            {
                LoadClosestStations();
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( String.IsNullOrWhiteSpace( ModeName ) )
            {
                error = "In module '" + Name + "', please add in a 'Mode Name' for your nested choice!";
                return false;
            }
            if ( Correlation > 1 || Correlation < 0 )
            {
                error = "Correlation must be between 0 and 1 for " + ModeName + "!";
                return false;
            }
            if ( MaxAccessStations <= 0 )
            {
                error = "The number of feasible access stations must be greater than 0!";
                return false;
            }
            foreach ( var network in Root.NetworkData )
            {
                if ( network.NetworkType == AccessModeName )
                {
                    First = network;
                }

                if ( network.NetworkType == PrimaryModeName )
                {
                    var temp = network as ITripComponentData;
                    Second = temp == null ? Second : temp;
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
            // If everything is fine we can now Generate our children
            if ( !GenerateChildren() )
            {
                return false;
            }
            return true;
        }

        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return Time.Zero;
        }

        private static double CalcDistance(IZone zone1, IZone zone2)
        {
            var x1 = zone1.X;
            var y1 = zone1.Y;
            var x2 = zone2.X;
            var y2 = zone2.Y;
            return Math.Sqrt( ( x1 - x2 ) * ( x1 - x2 )
                            + ( y1 - y2 ) * ( y1 - y2 ) );
        }

        private void CreateChild(int stationZone, int parkingSpots)
        {
            SubwayAccessStation station = new SubwayAccessStation();
            //Setup the parameters
            station.Root = Root;
            station.Parent = this;
            station.Closest = Closest;
            station.ClosestDistance = ClosestDistance;
            station.MaxAccessToDestinationTime = MaxAccessToDestinationTime;
            station.CurrentlyFeasible = 1.0f;
            // The constant for this option is not the same as for the station choice
            //station.Constant = this.Constant;
            station.AccessInVehicleTravelTime = AccessInVehicleTravelTime;
            station.AccessCost = AccessCost;
            station.InVehicleTravelTime = GeneralTime;
            station.ParkingCost = ParkingCost;
            station.LogParkingFactor = ParkingFactor;
            station.WaitTime = WaitTime;
            station.WalkTime = WalkTime;
            station.FareTTC = FareTTC;

            // Setup the modes
            station.First = First;
            station.Second = Second;

            // Create all of the individual parameters
            station.ModeName = String.Format( "{0}:{1}", ModeName, stationZone );
            station.StationZone = stationZone;
            station.Parking = parkingSpots;
            // Add it to the list of children
            Children.Add( station );
        }

        private bool GenerateChildren()
        {
            Children = new List<SubwayAccessStation>();
            List<Range> rangeList = new List<Range>();
            var start = 0;
            int stop;
            int current = 0;
            bool first = true;
            foreach ( var record in StationZoneData.Read() )
            {
                var zoneNumber = (int)Math.Round( record[0] );
                var parkingSpots = zoneNumber;
                if ( RequireParking && parkingSpots <= 0 )
                {
                    // skip zones without parking spots
                    continue;
                }
                if ( first )
                {
                    first = false;
                }
                else if ( current + 1 != zoneNumber )
                {
                    stop = current;
                    rangeList.Add( new Range(start, stop) );
                    start = zoneNumber;
                }
                current = zoneNumber;
                CreateChild( zoneNumber, (int)record[1] );
            }
            if ( !first )
            {
                stop = current;
                rangeList.Add( new Range(start, stop) );
                var set = new RangeSet( rangeList );
                foreach ( var child in Children )
                {
                    child.StationRanges = set;
                }
            }
            return true;
        }

        private void LoadClosestStations()
        {
            var zoneSystem = Root.ZoneSystem.ZoneArray;
            var flatZones = zoneSystem.GetFlatData();
            var closest = zoneSystem.CreateSimilarArray<int[]>();
            var flat = closest.GetFlatData();
            Parallel.For( 0, flatZones.Length, origin =>
            {
                var tempClosest = new double[MaxAccessStations];
                flat[origin] = new int[MaxAccessStations];
                for ( int j = 0; j < tempClosest.Length; j++ )
                {
                    tempClosest[j] = double.PositiveInfinity;
                    for ( int k = 0; k < tempClosest.Length; k++ )
                    {
                        flat[origin][k] = -1;
                    }
                }
                //Go through all of the children and quickly add them here, insertion sorted
                for ( int j = 0; j < Children.Count; j++ )
                {
                    var distance = CalcDistance( flatZones[origin], zoneSystem[Children[j].StationZone] );
                    for ( int k = 0; k < tempClosest.Length; k++ )
                    {
                        if ( distance < tempClosest[k] )
                        {
                            //shift down if not last
                            if ( k < tempClosest.Length - 1 )
                            {
                                Array.Copy( tempClosest, k, tempClosest, k + 1, tempClosest.Length - ( k + 1 ) );
                                Array.Copy( flat[origin], k, flat[origin], k + 1, flat[origin].Length - ( k + 1 ) );
                            }
                            tempClosest[k] = distance;
                            flat[origin][k] = j;
                            break;
                        }
                    }
                }
            } );
            ClosestStations = closest;
        }

        private void RebuildCache(Time time)
        {
            lock ( this )
            {
                Thread.MemoryBarrier();
                if ( LastIteration == Root.CurrentIteration ) return;
                Cache = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<CacheData>();
                LastIteration = Root.CurrentIteration;
                CacheTime = time;
                Thread.MemoryBarrier();
            }
        }

        private class CacheData
        {
            internal int AccessStations;
            internal float[] AccessUtil;
            internal IZone[] AccessZone;
            internal float AverageParking;
            internal float AverageWait;
            internal bool Feasible;
            internal float Logsum;
        }
    }
}