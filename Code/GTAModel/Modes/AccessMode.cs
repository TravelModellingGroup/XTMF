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
using Datastructure;
using TMG.Input;
using TMG.Modes;
using XTMF;
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace TMG.GTAModel.Modes
{
    public class AccessMode : IStationCollectionMode, IUtilityComponentMode, IIterationSensitive
    {
        [RunParameter("Access Cost", 0f, "The cost of travelling from the origin to the access station.")]
        public float AccessCost;

        [RunParameter("Access IVTT", 0f, "The factor to apply to the general time of travel.")]
        public float AccessInVehicleTravelTime;

        [RunParameter("Access Network Name", "Auto", "The name of the network to use to get to the interchange.")]
        public string AccessModeName;

        [RunParameter("AgeConstant1", 0.0f, "An additive constant for persons for different ages.")]
        public float AgeConstant1;

        [RunParameter("AgeConstant2", 0.0f, "An additive constant for persons for different ages.")]
        public float AgeConstant2;

        [RunParameter("AgeConstant3", 0.0f, "An additive constant for persons for different ages.")]
        public float AgeConstant3;

        [RunParameter("AgeConstant4", 0.0f, "An additive constant for persons for different ages.")]
        public float AgeConstant4;

        [RunParameter("Alternative Access Network Name", "", "The name of the network to use to get to the interchange if the first one fails.")]
        public string AlternativePrimaryModeName;

        [RunParameter("Bording Time", 0f, "The factor applied to the boarding time.")]
        public float BoardingTime;

        [RunParameter("Closest", 1.4437f, "The constant to be added if we are the closest station to the origin.")]
        public float Closest;

        [RunParameter("Closest Distance", 0f, "The factor to apply to the distance if this is the closest station between the origin and this station.")]
        public float ClosestDistance;

        [RunParameter("Compute Egress Station", true, "Compute the station to egress to?")]
        public bool ComputeEgressStation;

        [RunParameter("Constant", 0.0f, "A base constant for the utility.")]
        public float Constant;

        [RunParameter("Cost", 0f, "The factor applied to the cost after access.")]
        public float CostFactor;

        [RunParameter("Destination Employment Density", 0.0f, "The weight to use for the employment density of the destination zone.")]
        public float DestinationEmploymentDensity;

        [RunParameter("Destination Population Density", 0.0f, "The weight to use for the population density of the destination zone.")]
        public float DestinationPopulationDensity;

        [RunParameter("Egress Walk Factor", 2.0f, "The factor to walking time into minutes when computing the egress station.")]
        public float EgressWalkFactor;

        [RunParameter("Egress Wait Factor", 2.0f, "The factor to waiting time into minutes when computing the egress station.")]
        public float EgressWaitFactor;

        [RunParameter("Egress Network Name", "Transit", "The name of the network to use after going to the egress zone.")]
        public string EgressNetworkName;

        [RunParameter("Exclude Line Hull", false, "Don't include the line hull into the utility function.")]
        public bool ExcludeLineHull;

        [SubModelInformation(Description = "An optional test for mode feasibility.", Required = false)]
        public ICalculation<Pair<IZone, IZone>, bool> FeasibilityTest;

        [DoNotAutomate]
        public INetworkData First;

        [DoNotAutomate]
        public INetworkData FirstAlternative;

        [RunParameter("General Time", -0.103420f, "The factor to apply to the general time of travel.")]
        public float GeneralTime;

        [RunParameter("Log Trains Factor", 0.013336f, "The factor to apply to the number of trains the occur during the peak period.")]
        public float LogTrainsFactor;

        [RunParameter("Max Access Stations", 5, "The maximum access stations to consider when computing utility.")]
        public int MaxAccessStations;

        [RunParameter("Max Access To Destination Time", 150f, "The maximum time in minutes that going from an access station to the destination.")]
        public float MaxAccessToDestinationTime;

        [RunParameter("Min Access Station LogsumValue", -10f, "The minimum utility that the logsum of access stations can produce and still have a feasible trip.")]
        public float MinAccessStationLogsumValue;

        [RunParameter("Origin Employment Density", 0.0f, "The weight to use for the employment density of the origin zone.")]
        public float OriginEmploymentDensity;

        [RunParameter("Origin Population Density", 0.0f, "The weight to use for the population density of the origin zone.")]
        public float OriginPopulationDensity;

        [RunParameter("Parking Cost", 0f, "The factor applied to the cost of parking at the access station.")]
        public float ParkingCost;

        [RunParameter("Parking Factor", 0.388380f, "The factor applied to the log of the number of parking spots.")]
        public float ParkingFactor;

        [RunParameter("Primary Network Name", "Transit", "The name of the network to use after the interchange.")]
        public string PrimaryModeName;

        [RunParameter("Require Parking", true, "Skip stations that do not have a parking spot.")]
        public bool RequireParking;

        [RootModule]
        public I4StepModel Root;

        [DoNotAutomate]
        public ITripComponentData Second;

        [SubModelInformation(Description = "(Origin = Station Number, Destination = Parking Spots, Data = Number Of Trains)", Required = true)]
        public IReadODData<float> StationInformationReader;

        [DoNotAutomate]
        public ITripComponentData Third;

        [RunParameter("Trains Factor", 0.013336f, "The factor to apply to the number of trains the occur during the peak period.")]
        public float TrainsFactor;

        [RunParameter("Wait Time", -0.086483f, "The factor to apply to the wait time.")]
        public float WaitTime;

        [RunParameter("Walk Time", -0.295330f, "The factor to apply to the general time of travel.")]
        public float WalkTime;

        private bool _Access;

        private float _CurrentlyFeasible;

        private SparseTwinIndex<CacheData> Cache;

        private Time CacheTime = new Time { Hours = -1 };

        private int LastIteration = -1;

        [RunParameter("Access", true, "Is this mode in access mode or egress mode?")]
        public bool Access
        {
            get { return _Access; }

            set
            {
                _Access = value;
                if (Children == null) return;
                foreach (var c in Children)
                {
                    c.Access = value;
                }
            }
        }

        [DoNotAutomate]
        public List<AccessStation> Children { get; set; }

        [RunParameter("Correlation", 1f, "The correlation between the alternatives.  1 means no correlation, 0 means perfect correlation.")]
        public float Correlation { get; set; }

        [Parameter("Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?")]
        public float CurrentlyFeasible
        {
            get
            {
                return _CurrentlyFeasible;
            }

            set
            {
                _CurrentlyFeasible = value;
                if (Children != null)
                {
                    foreach (var child in Children)
                    {
                        child.CurrentlyFeasible = value;
                    }
                }
            }
        }

        [RunParameter("Mode Name", "DAS", "The name of this mixed mode option")]
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

        public string NetworkType => null;

        public bool NonPersonalVehicle => false;

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => null;

        [SubModelInformation(Description = "Additional Utility Components, part of the combined value.", Required = false)]
        public List<IUtilityComponent> UtilityComponents { get; set; }

        public float CalculateCombinedV(IZone origin, IZone destination, Time time)
        {
            // convert the area to KM^2
            var v = Constant + AgeConstant1 + AgeConstant2 + AgeConstant3 + AgeConstant4;
            var uc = UtilityComponents;
            for (int i = 0; i < uc.Count; i++)
            {
                v += uc[i].CalculateV(origin, destination, time);
            }
            return v + (float)(
                  Math.Log((origin.Population / (origin.InternalArea / 1000f)) + 1) * OriginPopulationDensity
                + Math.Log((destination.Employment / (destination.InternalArea / 1000f)) + 1) * DestinationEmploymentDensity
                );
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            if ((LastIteration != Root.CurrentIteration) | (time != CacheTime))
            {
                RebuildCache(time);
            }
            var data = Cache[origin.ZoneNumber, destination.ZoneNumber];
            if (data == null)
            {
                int alternatives = 0;
                // make sure we clip the number of possible stations
                float[] childrenV = new float[MaxAccessStations];
                float[] childrenDistance = new float[MaxAccessStations];
                for (int i = 0; i < MaxAccessStations; i++)
                {
                    childrenV[i] = float.MinValue;
                    childrenDistance[i] = float.MaxValue;
                }
                var children = Children;
                for (int childIndex = 0; childIndex < children.Count; childIndex++)
                {
                    var child = children[childIndex];
                    if (child.Feasible(origin, destination, time))
                    {
                        var localV = child.CalculateV(origin, destination, time);
                        if (!float.IsNaN(localV))
                        {
                            //get the distance for this access station

                            int minChild = 0;
                            // find the option with the lowest value
                            for (int i = 1; i < childrenV.Length; i++)
                            {
                                if (childrenV[i] < childrenV[minChild])
                                {
                                    minChild = i;
                                }
                            }
                            // replace the least utility with this new station
                            if (childrenV[minChild] < localV)
                            {
                                childrenV[minChild] = localV;
                            }
                            alternatives++;
                        }
                    }
                }
                if (alternatives > 0)
                {
                    double logsum = 0f;
                    for (int i = 0; (i < alternatives) & (i < childrenV.Length); i++)
                    {
                        logsum += Math.Exp(childrenV[i]);
                    }
                    logsum = Math.Log(logsum);
                    if (logsum >= MinAccessStationLogsumValue)
                    {
                        data = new CacheData
                        {
                            Logsum = (float)logsum
                        };
                    }
                    else
                    {
                        data = new CacheData
                        {
                            Logsum = float.NaN
                        };
                    }
                }
                else
                {
                    data = new CacheData
                    {
                        Logsum = float.NaN
                    };
                }
                Cache[origin.ZoneNumber, destination.ZoneNumber] = data;
            }
            if (float.IsNaN(data.Logsum))
            {
                return data.Logsum;
            }
            return (Correlation * data.Logsum) + CalculateCombinedV(origin, destination, time);
        }

        public float Cost(IZone origin, IZone destination, Time time)
        {
            return 0f;
        }

        public bool Feasible(IZone origin, IZone destination, Time time)
        {
            if (FeasibilityTest != null)
            {
                return CurrentlyFeasible > 0 && FeasibilityTest.ProduceResult(new Pair<IZone, IZone>(origin, destination));
            }
            return CurrentlyFeasible > 0;
        }

        public Tuple<IZone[], IZone[], float[]> GetSubchoiceSplit(IZone origin, IZone destination, Time time)
        {
            var data = Cache[origin.ZoneNumber, destination.ZoneNumber];
            if (data == null)
            {
                return null;
            }
            List<IZone> possibleZones = new List<IZone>();
            List<float> splits = new List<float>();
            var zoneSystem = Root.ZoneSystem.ZoneArray;
            var v = 0.0;
            foreach (var child in Children)
            {
                if (child.Feasible(origin, destination, time))
                {
                    var localV = child.CalculateV(origin, destination, time);
                    if (!float.IsNaN(localV))
                    {
                        var ev = Math.Exp(localV);
                        v += ev;
                        splits.Add((float)ev);
                        possibleZones.Add(zoneSystem[child.StationZone]);
                    }
                }
            }
            // make sure at least one choice has been made
            if (splits.Count <= 0)
            {
                return null;
            }
            v = 1.0 / v;
            // apply the total to generate the split rates
            for (int i = 0; i < splits.Count; i++)
            {
                splits[i] *= (float)v;
            }
            return new Tuple<IZone[], IZone[], float[]>(possibleZones.ToArray(), null, splits.ToArray());
        }

        public bool RuntimeValidation(ref string error)
        {
            if (String.IsNullOrWhiteSpace(ModeName))
            {
                error = "In module '" + Name + "', please add in a 'Mode Name' for your nested choice!";
                return false;
            }
            if (Correlation > 1 || Correlation < 0)
            {
                error = "Correlation must be between 0 and 1 for " + ModeName + "!";
                return false;
            }
            if (MaxAccessStations <= 0)
            {
                error = "The number of feasible access stations must be greater than 0!";
                return false;
            }
            foreach (var network in Root.NetworkData)
            {
                if (network.NetworkType == AccessModeName)
                {
                    First = network;
                }

                if (network.NetworkType == PrimaryModeName)
                {
                    Second = network as ITripComponentData ?? Second;
                }

                if (network.NetworkType == EgressNetworkName)
                {
                    var temp = network as ITripComponentData;
                    Third = temp ?? Third;
                }

                if (network.NetworkType == AlternativePrimaryModeName)
                {
                    FirstAlternative = network;
                }
            }
            if (First == null)
            {
                error = "In '" + Name + "' the name of the access network data type was not found!";
                return false;
            }
            if (Second == null)
            {
                error = "In '" + Name + "' the name of the primary network data type was not found or does not contain trip component data!";
                return false;
            }
            if (Third == null && ComputeEgressStation)
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
            if (!Third.ValidOd(flatEgressZone, flatDestinationZone, time))
            {
                egressUtility = float.MinValue;
                return false;
            }
            Third.GetAllData(flatEgressZone, flatDestinationZone, time, out Time ivtt, out Time walk, out Time wait, out Time boarding, out float cost);
            egressUtility = ivtt.ToMinutes() + EgressWaitFactor * wait.ToMinutes() + EgressWalkFactor * walk.ToMinutes();
            return egressUtility != float.MaxValue;
        }

        private void CreateChild(int stationZone, int parkingSpots, float numberOfTrains)
        {
            AccessStation station = new AccessStation
            {
                Root = Root,
                Parent = this,
                Closest = Closest,
                ClosestDistance = ClosestDistance,
                MaxAccessToDestinationTime = MaxAccessToDestinationTime,
                Access = Access,
                ExcludeLineHull = ExcludeLineHull,
                AccessInVehicleTravelTime = AccessInVehicleTravelTime,
                AccessCost = AccessCost,
                EgressWalkFactor = EgressWalkFactor,
                EgressWaitFactor = EgressWaitFactor,
                InVehicleTravelTime = GeneralTime,
                ParkingCost = ParkingCost,
                LogParkingFactor = ParkingFactor,
                TrainsFactor = TrainsFactor,
                LogTrainsFactor = LogTrainsFactor,
                WaitTime = WaitTime,
                WalkTime = WalkTime,
                BoardingTime = BoardingTime,
                ComputeEgressStation = ComputeEgressStation,
                First = First,
                Second = Second,
                Third = Third,
                FirstAlternative = FirstAlternative,
                ModeName = $"{ModeName}:{stationZone}",
                StationZone = stationZone,
                Parking = parkingSpots,
                NumberOfTrains = numberOfTrains
            };
            //Setup the parameters
            // The constant for this option is not the same as for the station choice
            //station.Constant = this.Constant;

            // Setup the modes

            // Create all of the individual parameters
            // Add it to the list of children
            Children.Add(station);
        }

        private bool GenerateChildren()
        {
            Children = new List<AccessStation>();
            var rangeList = new List<Range>();
            var start = 0;
            int stop;
            var current = 0;
            bool first = true;
            var zoneArray = Root.ZoneSystem.ZoneArray;
            foreach (var record in StationInformationReader.Read())
            {
                // make sure the station is actually inside of the zone system
                if (zoneArray.ContainsIndex(record.O))
                {
                    var parkingSpots = record.D;
                    if (first)
                    {
                        start = record.O;
                        first = false;
                    }
                    else if (current + 1 != record.O)
                    {
                        stop = current;
                        rangeList.Add(new Range(start, stop));
                        start = record.O;
                    }
                    current = record.O;
                    CreateChild(record.O, parkingSpots, record.Data);
                }
            }
            if (!first)
            {
                stop = current;
                rangeList.Add(new Range(start, stop));
                var set = new RangeSet(rangeList);
                foreach (var child in Children)
                {
                    child.StationRanges = set;
                }
            }
            return true;
        }

        private void RebuildCache(Time time)
        {
            lock (this)
            {
                Thread.MemoryBarrier();
                if (LastIteration == Root.CurrentIteration) return;
                Cache = Root.ZoneSystem.ZoneArray.CreateSquareTwinArray<CacheData>();
                foreach (var child in Children)
                {
                    child.DumpCaches();
                }
                LastIteration = Root.CurrentIteration;
                CacheTime = time;
                Thread.MemoryBarrier();
            }
        }

        private class CacheData
        {
            internal float Logsum;
        }

        public void IterationEnding(int iterationNumber, int maxIterations)
        {

        }

        public void IterationStarting(int iterationNumber, int maxIterations)
        {
            if (iterationNumber == 0)
            {
                // If everything is fine we can now Generate our children
                if (!GenerateChildren())
                {
                    throw new XTMFRuntimeException(this, $"In {Name} we experienced an error when generating the access stations.");
                }
            }
        }
    }
}