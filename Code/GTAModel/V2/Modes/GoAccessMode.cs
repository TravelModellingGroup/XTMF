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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Datastructure;
using TMG.Input;
using TMG.Modes;
using XTMF;

namespace TMG.GTAModel.V2.Modes
{
    public class GoAccessMode : IStationCollectionMode, IUtilityComponentMode, IIterationSensitive
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

        [RunParameter( "Compute Egress Station", true, "Compute the station to egress to?" )]
        public bool ComputeEgressStation;

        [RunParameter( "Constant", 0.0f, "A base constant for the utility." )]
        public float Constant;

        [RunParameter( "Cost", 0f, "The factor applied to the cost after access." )]
        public float CostFactor;

        [RunParameter( "Egress Network Name", "Transit", "The name of the network to use after going to the egress zone." )]
        public string EgressNetworkName;

        [RunParameter( "Egress Wait Factor", 2.0f, "The factor applied to wait time for selecting an egress station." )]
        public float EgressWaitFactor;

        [RunParameter( "Egress Walk Factor", 2.0f, "The factor applied to walk time for selecting an egress station." )]
        public float EgressWalkFactor;

        [SubModelInformation( Description = "An optional test for mode feasibility.", Required = false )]
        public ICalculation<Pair<IZone, IZone>, bool> FeasibilityTest;

        [DoNotAutomate]
        public INetworkData First;

        [SubModelInformation( Description = "Used to check if there are free Transfers.", Required = true )]
        public IDataSource<SparseArray<float>> FreeTransfers;

        [RunParameter( "General Time", -0.103420f, "The factor to apply to the general time of travel." )]
        public float GeneralTime;

        [RunParameter( "GO Station Zones", "7000-7999", typeof( RangeSet ), "The range of zones that GO Transit represents." )]
        public RangeSet GoZones;

        [RunParameter( "LineHullAverageFactor", 0f, "The factor to apply to the average line hull time across different access stations." )]
        public float LineHullAverageFactor;

        [RunParameter( "Max Access To Destination Time", 150f, "The maximum time in minutes that going from an access station to the destination." )]
        public float MaxAccessToDestinationTime;

        [RunParameter( "Min Access Station LogsumValue", -10f, "The miniumum utility that the logsum of access stations can produce and still have a feasible trip." )]
        public float MinAccessStationLogsumValue;

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

        [DoNotAutomate]
        public ITripComponentData Third;

        [SubModelInformation( Description = "Get the frequency of the trains passing from origin to destination.", Required = false )]
        public IReadODData<float> TrainFrequency;

        [RunParameter( "Trains Factor", 0.013336f, "The factor to apply to the number of trains the occure during the peak period." )]
        public float TrainsFactor;

        [RunParameter( "TravelTimeAverageFactor", 0f, "The factor to apply to the average travel time across different access stations." )]
        public float TravelTimeAverageFactor;

        [RunParameter( "Union", 0f, "The weight applied against the frequency (fraction) of Union Station being the optimal (assumed chosen) egress station, given the set of feasible access stations " )]
        public float Union;

        [RunParameter( "Union Station Zone Number", 7001, "The zone number that union station is in." )]
        public int UnionStationZoneNumber;

        [RunParameter( "Wait Time", -0.086483f, "The factor to apply to the wait time." )]
        public float WaitTime;

        [RunParameter( "WaitTimeAverageFactor", 0f, "The factor to apply to the average wait time across different access stations." )]
        public float WaitTimeAverageFactor;

        [RunParameter( "Walk Time", -0.295330f, "The factor to apply to the general time of travel." )]
        public float WalkTime;

        [RunParameter( "WalkTimeAverageFactor", 0f, "The factor to apply to the average walk time across different access stations." )]
        public float WalkTimeAverageFactor;

        private bool _Access;

        private float _CurrentlyFeasible;

        private SparseTwinIndex<CacheData> Cache;

        private Time CacheTime = new Time { Hours = -1 };

        /// <summary>
        /// [Origin][StationChildIndex]
        /// </summary>
        private int[][] ClosestAccessStationsToOrigins;

        private int lastIteration = -1;

        [RunParameter( "Access", true, "Is this mode in access mode or egress mode?" )]
        public bool Access
        {
            get { return _Access; }

            set
            {
                _Access = value;
                if ( Children == null ) return;
                foreach ( var c in Children )
                {
                    c.Access = value;
                }
            }
        }

        [DoNotAutomate]
        public List<GoAccessStation> Children { get; set; }

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

        [RunParameter( "Mode Name", "DAG", "The name of this mixed mode option" )]
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
            if ( ( lastIteration != Root.CurrentIteration ) | ( time != CacheTime ) )
            {
                RebuildCache( time );
            }
            var data = Cache[origin.ZoneNumber, destination.ZoneNumber];
            if ( data == null )
            {
                var zones = Root.ZoneSystem.ZoneArray;
                var toCheck = ClosestAccessStationsToOrigins[zones.GetFlatIndex( origin.ZoneNumber )];
                int alternatives = 0;
                float averageTotalWaitTime = 0f;
                float averageTotalWalkTime = 0f;
                float averageTravelTime = 0f;
                float averageLineHull = 0f;
                // make sure we clip the number of possible stations
                float[] childrenEToV = new float[4];
                IZone[] childrenAccessZone = new IZone[4];
                IZone[] childrenEgressZone = new IZone[4];
                int unions = 0;

                for ( int computeChild = 0; computeChild < 4; computeChild++ )
                {
                    childrenEToV[computeChild] = float.MinValue;
                    var child = Children[toCheck[computeChild]];
                    if ( child.Feasible( origin, destination, time ) )
                    {
                        var localV = child.CalculateV( origin, destination, time );
                        if ( !float.IsNaN( localV ) )
                        {
                            if ( localV < MinAccessStationLogsumValue ) continue;
                            var egressZone = child.EgressChoiceCache[destination.ZoneNumber].EgressZone;
                            if ( egressZone.ZoneNumber == UnionStationZoneNumber )
                            {
                                unions++;
                            }
                            else if ( origin.RegionNumber == 1 )
                            {
                                // if we are in toronto
                                var flatAcessZone = zones.GetFlatIndex( child.InterchangeZone.ZoneNumber );
                                var flatEgressZone = zones.GetFlatIndex( egressZone.ZoneNumber );
                                // if the to stations are next to eachother just continue on
                                if ( Math.Abs( flatAcessZone - flatEgressZone ) <= 1 )
                                {
                                    continue;
                                }
                            }
                            // replace the least utility with this new station
                            if ( childrenEToV[alternatives] < localV )
                            {
                                childrenEToV[alternatives] = localV;
                                childrenAccessZone[alternatives] = child.InterchangeZone;
                                childrenEgressZone[alternatives] = egressZone;
                                alternatives++;
                                // now add up the averages
                                var lineHull = Second.InVehicleTravelTime( child.InterchangeZone, egressZone, time ).ToMinutes();
                                averageLineHull += lineHull;
                                // 5 minutes added for the initial GO wait time
                                averageTotalWaitTime += Third.WaitTime( egressZone, destination, time ).ToMinutes() + 5f;
                                averageTotalWalkTime += Third.WalkTime( egressZone, destination, time ).ToMinutes();
                                // average travel time does not include walk or wait times
                                averageTravelTime += Third.InVehicleTravelTime( egressZone, destination, time ).ToMinutes() + lineHull;
                            }
                        }
                    }
                }
                if ( alternatives > 0 )
                {
                    double logsum = 0f;
                    // if there is only 1 alternative there is no reason to do the log, just solve it here
                    if ( alternatives == 1 )
                    {
                        // do the sum here to avoid doing a log
                        logsum = childrenEToV[0];
                        // we still need this exponentiated for doing our station split
                        childrenEToV[0] = (float)Math.Exp( childrenEToV[0] );
                    }
                    else
                    {
                        for ( int i = 0; i < alternatives; i++ )
                        {
                            var temp = Math.Exp( childrenEToV[i] );
                            childrenEToV[i] = (float)temp;
                            logsum += temp;
                        }
                        logsum = Math.Log( logsum );
                    }
                    data = new CacheData
                    {
                        Feasible = true,
                        AccessUtil = childrenEToV,
                        AccessZone = childrenAccessZone,
                        EgressZone = childrenEgressZone,
                        Logsum = (float)logsum,
                        AccessStations = alternatives,
                        Unions = unions,
                        AverageLineHull = averageLineHull,
                        AverageTravelTime = averageTravelTime,
                        AverageWaitTime = averageTotalWaitTime,
                        AverageWalkTime = averageTotalWalkTime
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
            // if we have nothing, return nothing
            if ( float.IsNaN( data.Logsum ) )
            {
                return float.NaN;
            }
            // otherwise return our utility
            var v = CalculateCombinedV( origin, destination, time ) + ( Correlation * data.Logsum )
                            + ( ( ( Union * data.Unions )
                            + ( data.AverageWaitTime * WaitTimeAverageFactor )
                            + ( data.AverageLineHull * LineHullAverageFactor )
                            + ( data.AverageWalkTime * WalkTimeAverageFactor )
                            + ( data.AverageTravelTime * TravelTimeAverageFactor ) ) / data.AccessStations );
            return v;
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
            // do the reagion check here
            var originRegion = origin.RegionNumber;
            var destinationRegion = destination.RegionNumber;
            // if the regions are the same
            if ( originRegion == destinationRegion )
            {
                // They must be in the city of toronto
                if ( originRegion != 1 )
                {
                    return false;
                }
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
            return new Tuple<IZone[], IZone[], float[]>( data.AccessZone, data.EgressZone, data.AccessUtil );
        }

        public void IterationEnding(int iterationNumber, int maxIterations)
        {
        }

        public void IterationStarting(int iterationNumber, int maxIterations)
        {
            if ( iterationNumber == 0 )
            {
                string error = null;
                // If everything is fine we can now Generate our children
                if ( !GenerateChildren( ref error ) )
                {
                    throw new XTMFRuntimeException( error );
                }
                // if we are in the first iteration go and make sure that we have our Origin - > Access Station[]'s built
                BuildOriginToAccessStations();
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

                if ( network.NetworkType == EgressNetworkName )
                {
                    var temp = network as ITripComponentData;
                    Third = temp == null ? Third : temp;
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
            return Time.Zero;
        }

        internal bool GetEgressUtility(int flatEgressZone, int flatDestinationZone, Time time, out float egressUtility)
        {
            // Make sure that we can get from the egress station to the destination zone at that current point in the day
            if ( !Third.ValidOd( flatEgressZone, flatDestinationZone, time ) )
            {
                egressUtility = float.MinValue;
                return false;
            }
            Time ivtt, walk, wait, boarding;
            float cost;
            Third.GetAllData( flatEgressZone, flatDestinationZone, time, out ivtt, out walk, out wait, out boarding, out cost );
            // Cost does not factor in for V2
            egressUtility = ivtt.ToMinutes() + EgressWaitFactor * wait.ToMinutes() + EgressWalkFactor * walk.ToMinutes();
            return egressUtility != float.MaxValue;
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

        private static void InitializeBestSoFar(float[][] bestSoFarDistance, int[][] bestSoFarStationIndex)
        {
            for ( int i = 0; i < bestSoFarDistance.Length; i++ )
            {
                bestSoFarDistance[i] = new float[2];
                bestSoFarStationIndex[i] = new int[2];
                for ( int j = 0; j < bestSoFarDistance[i].Length; j++ )
                {
                    bestSoFarDistance[i][j] = float.PositiveInfinity;
                    bestSoFarStationIndex[i][j] = -1;
                }
            }
        }

        private static void LoadInOptimalLines(int[] row, float[][] bestSoFarDistance, int[][] bestSoFarStationIndex)
        {
            float bestDistance = float.PositiveInfinity;
            int bestLine = -1;
            for ( int i = 0; i < bestSoFarDistance.Length; i++ )
            {
                if ( bestDistance > bestSoFarDistance[i][0] )
                {
                    bestLine = i;
                    row[0] = bestSoFarStationIndex[i][0];
                    row[1] = bestSoFarStationIndex[i][1];
                    bestDistance = bestSoFarDistance[i][0];
                }
            }
            bestDistance = float.PositiveInfinity;
            for ( int i = 0; i < bestSoFarDistance.Length; i++ )
            {
                if ( i == bestLine ) continue;
                if ( bestDistance > bestSoFarDistance[i][0] )
                {
                    row[2] = bestSoFarStationIndex[i][0];
                    row[3] = bestSoFarStationIndex[i][1];
                    bestDistance = bestSoFarDistance[i][0];
                }
            }
        }

        private void BuildOriginToAccessStations()
        {
            int numberOfPossibleAccessStations = 4;
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            ClosestAccessStationsToOrigins = new int[zones.Length][];
            var lineLookup = CreateLineInverseLookup();
            var distances = Root.ZoneSystem.Distances;
            Parallel.For( 0, zones.Length, origin =>
            {
                var row = new int[numberOfPossibleAccessStations];
                float[][] bestSoFarDistance = new float[lineLookup.Count][];
                int[][] bestSoFarStationIndex = new int[lineLookup.Count][];
                InitializeBestSoFar( bestSoFarDistance, bestSoFarStationIndex );
                SetClosestTwoStationsPerLine( zones, lineLookup, distances, origin, bestSoFarDistance, bestSoFarStationIndex );
                LoadInOptimalLines( row, bestSoFarDistance, bestSoFarStationIndex );
                ClosestAccessStationsToOrigins[origin] = row;
            } );
        }

        private void CreateChild(int stationZone, int parkingSpots, SparseTwinIndex<float> numberOfTrains, int lineNumber)
        {
            GoAccessStation station = new GoAccessStation();
            //Setup the parameters
            station.Root = Root;
            station.Parent = this;
            station.Closest = Closest;
            station.ClosestDistance = ClosestDistance;
            station.MaxAccessToDestinationTime = MaxAccessToDestinationTime;
            station.Access = Access;
            station.CurrentlyFeasible = 1.0f;
            // The constant for this option is not the same as for the station choice
            //station.Constant = this.Constant;
            station.AccessInVehicleTravelTime = AccessInVehicleTravelTime;
            station.AccessCost = AccessCost;
            station.InVehicleTravelTime = GeneralTime;
            station.LogParkingFactor = ParkingFactor;
            station.TrainsFactor = TrainsFactor;
            station.WaitTime = WaitTime;
            station.WalkTime = WalkTime;
            station.CostFactor = CostFactor;
            station.EgressWalkFactor = EgressWalkFactor;
            station.EgressWaitFactor = EgressWaitFactor;

            // Setup the modes
            station.ComputeEgressStation = ComputeEgressStation;
            station.First = First;
            station.FirstComponent = First as ITripComponentData;
            station.Second = Second;
            station.Third = Third;
            station.FreeTransfers = FreeTransfers.GiveData();

            // Create all of the individual parameters
            station.ModeName = String.Format( "{0}:{1}", ModeName, stationZone );
            station.StationZone = stationZone;
            station.LineNumber = lineNumber;
            station.Parking = parkingSpots;
            station.NumberOfTrains = numberOfTrains;
            // Add it to the list of children
            Children.Add( station );
        }

        private SortedList<int, List<int>> CreateLineInverseLookup()
        {
            var lineLookup = new SortedList<int, List<int>>();
            for ( int i = 0; i < Children.Count; i++ )
            {
                List<int> currentLine;
                if ( !lineLookup.TryGetValue( Children[i].LineNumber, out currentLine ) )
                {
                    currentLine = new List<int>();
                    lineLookup[Children[i].LineNumber] = currentLine;
                }
                currentLine.Add( i );
            }
            return lineLookup;
        }

        private bool GenerateChildren(ref string error)
        {
            FreeTransfers.LoadData();
            Children = new List<GoAccessStation>();
            List<Range> rangeList = new List<Range>();
            SparseTwinIndex<float> frequencies = ReadFrequencies();
            foreach ( var record in StationZoneData.Read() )
            {
                var zoneNumber = (int)Math.Round( record[0] );
                var parkingSpots = record[1];
                // either it doesn't require parking, or there have to be parking spaces
                if ( !RequireParking | parkingSpots > 0 )
                {
                    CreateChild( zoneNumber, (int)record[1], frequencies, (int)record[2] );
                }
            }
            foreach ( var child in Children )
            {
                child.StationRanges = GoZones;
            }
            FreeTransfers.UnloadData();
            return true;
        }

        private SparseTwinIndex<float> ReadFrequencies()
        {
            if ( TrainFrequency == null )
            {
                return null;
            }
            var frequencies = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<float>();
            foreach ( var point in TrainFrequency.Read() )
            {
                if ( frequencies.ContainsIndex( point.O, point.D ) )
                {
                    frequencies[point.O, point.D] = point.Data;
                }
                else
                {
                    throw new XTMFRuntimeException( "While processing the train frequencies for '" + Name
                        + "' we came across an invalid frequency from '" + point.O + "' to '" + point.D + "'." );
                }
            }
            return frequencies;
        }

        private void RebuildCache(Time time)
        {
            lock ( this )
            {
                Thread.MemoryBarrier();
                if ( lastIteration == Root.CurrentIteration ) return;
                Cache = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<CacheData>();
                foreach ( var child in Children )
                {
                    child.DumpCaches();
                }
                lastIteration = Root.CurrentIteration;
                CacheTime = time;
                Thread.MemoryBarrier();
            }
        }

        private void SetClosestTwoStationsPerLine(IZone[] zones, SortedList<int, List<int>> lineLookup, SparseTwinIndex<float> distances, int origin, float[][] bestSoFarDistance,
            int[][] bestSoFarStationIndex)
        {
            //find the closest stations from 2 different lines
            for ( int lineIndex = 0; lineIndex < lineLookup.Count; lineIndex++ )
            {
                var stationsInLine = lineLookup.Values[lineIndex];
                for ( int stationIndex = 0; stationIndex < stationsInLine.Count; stationIndex++ )
                {
                    var station = Children[stationsInLine[stationIndex]];
                    var stationDistance = distances[zones[origin].ZoneNumber, station.StationZone];
                    if ( stationDistance < bestSoFarDistance[lineIndex][0] )
                    {
                        // bump
                        bestSoFarDistance[lineIndex][1] = bestSoFarDistance[lineIndex][0];
                        bestSoFarStationIndex[lineIndex][1] = bestSoFarStationIndex[lineIndex][0];
                        // assign
                        bestSoFarDistance[lineIndex][0] = stationDistance;
                        bestSoFarStationIndex[lineIndex][0] = stationsInLine[stationIndex];
                    }
                    else if ( stationDistance < bestSoFarDistance[lineIndex][1] )
                    {
                        //just assign, there is no bump
                        bestSoFarDistance[lineIndex][1] = stationDistance;
                        bestSoFarStationIndex[lineIndex][1] = stationsInLine[stationIndex];
                    }
                }
            }
        }

        private class CacheData
        {
            internal int AccessStations;
            internal float[] AccessUtil;
            internal IZone[] AccessZone;
            internal float AverageLineHull;
            internal float AverageTravelTime;
            internal float AverageWaitTime;
            internal float AverageWalkTime;
            internal IZone[] EgressZone;
            internal bool Feasible;
            internal float Logsum;
            internal float Unions;
        }
    }
}