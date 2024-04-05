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
// ReSharper disable CompareOfFloatsByEqualityOperator

namespace TMG.GTAModel.Modes;

public class AccessStation : IStationMode
{
    [RunParameter("Access", true, "Is this mode in access mode or egress mode?")]
    public bool Access;

    [RunParameter("Access Cost", 0f, "The cost of traveling from the origin to the access station.")]
    public float AccessCost;

    [RunParameter("Access IVTT", 0f, "The factor to apply to the general time of travel.")]
    public float AccessInVehicleTravelTime;

    [RunParameter("Access Network Name", "Auto", "The name of the network to use to get to the interchange.")]
    public string AccessModeName;

    [RunParameter("Alternative Access Network Name", "Transit", "The name of the network to use to get to the interchange if the first one fails.")]
    public string AlternativePrimaryModeName;

    [RunParameter("Bording Time", 0f, "The factor applied to the boarding time.")]
    public float BoardingTime;

    [RunParameter("Closest", 1.4437f, "The constant to be added if we are the closest station to the origin.")]
    public float Closest;

    [RunParameter("Closest Distance", 0f, "The factor to apply to the distance if this is the closest station between the origin and this station.")]
    public float ClosestDistance;

    [Parameter("Compute Egress Station", true, "Compute the station to egress to?")]
    public bool ComputeEgressStation;

    [RunParameter("Cost", 0f, "The factor applied to the cost after access.")]
    public float CostFactor;

    [RunParameter("Egress Cost To Minutes", 2.0f, "The factor to convert walking time into minutes for calculating the egress station.")]
    public float EgressWalkFactor;

    [RunParameter("Egress Wait To Minutes", 2.0f, "The factor to convert wait time into minutes for calculating the egress station.")]
    public float EgressWaitFactor;

    [RunParameter("Egress Network Name", "Transit", "The name of the network to use after going to the egress zone.")]
    public string EgressNetworkName;

    [RunParameter("Exclude Line Hull", false, "Don't include the line hull into the utility function.")]
    public bool ExcludeLineHull;

    [DoNotAutomate]
    public INetworkData First;

    [DoNotAutomate]
    public INetworkData FirstAlternative;

    [RunParameter("IVTT", 0f, "The factor to apply to the general time of travel.")]
    public float InVehicleTravelTime;

    [RunParameter("Log Parking Factor", 0f, "The factor applied to the log of the number of parking spots.")]
    public float LogParkingFactor;

    [RunParameter("Log Trains Factor", 0f, "The factor to apply to the log of the number of trains the occur during the peak period.")]
    public float LogTrainsFactor;

    [RunParameter("Max Access To Destination Time", 150f, "The maximum time in minutes that going from an access station to the destination.")]
    public float MaxAccessToDestinationTime;

    [ParentModel]
    public AccessMode Parent;

    [RunParameter("Parking Cost", 0f, "The factor applied to the cost of parking at the access station.")]
    public float ParkingCost;

    [RunParameter("Primary Network Name", "Transit", "The name of the network to use after the interchange.")]
    public string PrimaryModeName;

    [RootModule]
    public I4StepModel Root;

    [DoNotAutomate]
    public ITripComponentData Second;

    [RunParameter("All Station Zones", "7000", typeof(RangeSet), "The station numbers to check to make sure that we are the closest one.")]
    public RangeSet StationRanges;

    [DoNotAutomate]
    public ITripComponentData Third;

    [RunParameter("Trains Factor", 0f, "The factor to apply to the number of trains the occur during the peak period.")]
    public float TrainsFactor;

    [RunParameter("Wait Time", 0f, "The factor to apply to the wait time.")]
    public float WaitTime;

    [RunParameter("Walk Time", 0f, "The factor to apply to the general time of travel.")]
    public float WalkTime;

    internal SparseArray<bool> ClosestZone;

    private float _LogNumberOfTrains;

    private float _NumberOfTrains;

    private int _Parking;

    private bool CacheLoaded;

    private SparseArray<EgressZoneChoice> EgressChoiceCache;

    private IZone InterchangeZone;

    private bool LocalTransitCacheLoaded;

    private float LogOfParking;

    [Parameter("Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?")]
    public float CurrentlyFeasible { get; set; }

    [RunParameter("Mode Name", "DAS 7000", "The name of this mixed mode option")]
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

    [RunParameter("Number of Trains", 0.0f, "The number of trains that pass by during the peak period.")]
    public float NumberOfTrains
    {
        get { return _NumberOfTrains; }

        set
        {
            _NumberOfTrains = value;
            _LogNumberOfTrains = (float)Math.Log(value);
        }
    }

    [RunParameter("Parking Spots", 0, "The number of parking spots for this station.")]
    public int Parking
    {
        get
        {
            return _Parking;
        }

        set
        {
            _Parking = value;
            LogOfParking = value <= 0 ? float.NegativeInfinity : (float)Math.Log(Parking);
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

    [RunParameter("Interchange Zone", 7000, "The zone number to use as the point of interchange.")]
    public int StationZone { get; set; }

    public float CalculateV(IZone origin, IZone destination, Time time)
    {
        CheckInterchangeZone();
        var zoneArray = Root.ZoneSystem.ZoneArray;
        var flatOrigin = zoneArray.GetFlatIndex(origin.ZoneNumber);
        var flatDestination = zoneArray.GetFlatIndex(destination.ZoneNumber);
        var flatInterchange = zoneArray.GetFlatIndex(InterchangeZone.ZoneNumber);
        // calculate all of the terms in all equations first
        float v = ParkingCost * InterchangeZone.ParkingCost
        + LogParkingFactor * LogOfParking
        + TrainsFactor * _NumberOfTrains
        + LogTrainsFactor * _LogNumberOfTrains;
        if (ClosestZone.GetFlatData()[flatOrigin])
        {
            // make sure we are talking in km's
            v += Closest + ClosestDistance * (Root.ZoneSystem.Distances.GetFlatData()[flatOrigin][flatInterchange] / 1000f);
        }

        // now check to see what type of model we are
        if (ComputeEgressStation)
        {
            var egressZoneChoice = FindEgressStation(flatDestination, time);
            if (egressZoneChoice.Zones == null) return float.NaN;
            var egressUtility = egressZoneChoice.EgressUtility;
            float travelTime = MaxAccessToDestinationTime;
            if (Access)
            {
                if (First.ValidOd(flatOrigin, flatInterchange, time) &&
                    (travelTime = First.TravelTime(flatOrigin, flatInterchange, time).ToMinutes()) > 0)
                {
                    v += AccessInVehicleTravelTime * travelTime
                        + AccessCost * First.TravelCost(flatOrigin, flatInterchange, time);

                }
                else if (FirstAlternative != null && FirstAlternative.ValidOd(flatOrigin, flatInterchange, time)
                    && (travelTime = FirstAlternative.TravelTime(flatOrigin, flatInterchange, time).ToMinutes()) != 0)
                {
                    v += AccessInVehicleTravelTime * FirstAlternative.TravelTime(flatOrigin, flatInterchange, time).ToMinutes()
                        + AccessCost * FirstAlternative.TravelCost(flatOrigin, flatInterchange, time);
                }
                if (travelTime >= MaxAccessToDestinationTime)
                {
                    return float.NaN;
                }
                v += egressUtility;
            }
            else
            {
                if (First.ValidOd(flatOrigin, flatInterchange, time))
                {
                    v += AccessInVehicleTravelTime * First.TravelTime(flatInterchange, flatDestination, time).ToMinutes()
                           + AccessCost * First.TravelCost(flatInterchange, flatDestination, time);
                }
                else if (FirstAlternative != null)
                {
                    v += AccessInVehicleTravelTime * FirstAlternative.TravelTime(flatInterchange, flatDestination, time).ToMinutes()
                           + AccessCost * FirstAlternative.TravelCost(flatInterchange, flatDestination, time);
                }
                else
                {
                    return float.NaN;
                }
                v += egressUtility;
            }
        }
        else
        {
            if (Access)
            {
                var toDestinationTime = Second.InVehicleTravelTime(flatInterchange, flatDestination, time).ToMinutes();
                if ((toDestinationTime > MaxAccessToDestinationTime) | (toDestinationTime <= 0))
                {
                    return float.NaN;
                }
                v += AccessInVehicleTravelTime * First.TravelTime(flatOrigin, flatInterchange, time).ToMinutes()
                    + AccessCost * First.TravelCost(flatOrigin, flatInterchange, time)
                    + ComputeSubV(Second, flatInterchange, flatDestination, time, InVehicleTravelTime, WalkTime, WaitTime, BoardingTime, CostFactor);
            }
            else
            {
                var toDestinationTime = Second.InVehicleTravelTime(flatOrigin, flatInterchange, time).ToMinutes();
                if ((toDestinationTime > MaxAccessToDestinationTime) | (toDestinationTime <= 0))
                {
                    return float.NaN;
                }
                v += AccessInVehicleTravelTime * First.TravelTime(flatInterchange, flatDestination, time).ToMinutes()
                    + AccessCost * First.TravelCost(flatInterchange, flatDestination, time)
                    + ComputeSubV(Second, flatOrigin, flatInterchange, time, InVehicleTravelTime, WalkTime, WaitTime, BoardingTime, CostFactor);
            }
        }
        return v;
    }

    public float Cost(IZone origin, IZone destination, Time time)
    {
        CheckInterchangeZone();
        return First.TravelCost(origin, InterchangeZone, time) + Second.TravelCost(origin, InterchangeZone, time);
    }

    public void DumpCaches()
    {
        LocalTransitCacheLoaded = false;
        Thread.MemoryBarrier();
        lock (this)
        {
            EgressChoiceCache = Root.ZoneSystem.ZoneArray.CreateSimilarArray<EgressZoneChoice>();
            LocalTransitCacheLoaded = true;
            Thread.MemoryBarrier();
        }
    }

    public bool Feasible(IZone originZone, IZone destinationZone, Time time)
    {
        if (CurrentlyFeasible <= 0 | (Parent.RequireParking & _Parking == 0)) return false;
        CheckInterchangeZone();
        var zoneArray = Root.ZoneSystem.ZoneArray;
        var origin = zoneArray.GetFlatIndex(originZone.ZoneNumber);
        var destination = zoneArray.GetFlatIndex(destinationZone.ZoneNumber);
        var interchange = zoneArray.GetFlatIndex(InterchangeZone.ZoneNumber);
        if (First is ITripComponentData component)
        {
            // make sure that there is a valid walk time if we are walking/transit to the station
            if (Access)
            {
                if (component.WalkTime(origin, interchange, time).ToMinutes() <= 0
                    && ((ITripComponentData)FirstAlternative).WalkTime(origin, interchange, time).ToMinutes() <= 0)
                {
                    return false;
                }
            }
            else
            {
                if (component.WalkTime(interchange, destination, time).ToMinutes() <= 0
                    && ((ITripComponentData)FirstAlternative).WalkTime(interchange, destination, time).ToMinutes() <= 0)
                {
                    return false;
                }
            }
        }
        return ItermediateZoneCloserThanDestination(origin, destination, interchange);
    }

    public bool RuntimeValidation(ref string error)
    {
        foreach (var network in Root.NetworkData)
        {
            if (network.Name == AccessModeName)
            {
                First = network;
            }

            if (network.Name == PrimaryModeName)
            {
                Second = network as ITripComponentData ?? Second;
            }

            if (network.NetworkType == EgressNetworkName)
            {
                Third = network as ITripComponentData ?? Third;
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
        CheckInterchangeZone();
        return First.TravelTime(origin, InterchangeZone, time) + Second.TravelTime(InterchangeZone, destination, time);
    }

    private static float ComputeSubV(ITripComponentData data, int flatOrigin, int flatDestination, Time t, float ivttWeight, float walkWeight, float waitWeight, float boardingWeight, float costWeight)
    {
        data.GetAllData(flatOrigin, flatDestination, t, out float ivtt, out float walk, out float wait, out float boarding, out float cost);
        return ivttWeight * ivtt
            + walkWeight * walk
            + waitWeight * wait
            + boardingWeight * boarding
            + costWeight * cost;
    }

    private static bool ComputeThird(ITripComponentData data, int flatOrigin, int flatDestination, Time t, float walkTime, float waitTime, out float result)
    {
        data.GetAllData(flatOrigin, flatDestination, t, out float ivtt, out float walk, out float wait, out float boarding, out float cost);
        if (walk <= 0)
        {
            result = float.PositiveInfinity;
            return false;
        }
        result = walk * walkTime
                + wait * waitTime
                + ivtt;
        return true;
    }

    private bool AreWeClosest(IZone origin, SparseArray<IZone> zoneArray, SparseTwinIndex<float> distances)
    {
        var ourDistance = distances[origin.ZoneNumber, InterchangeZone.ZoneNumber];
        foreach (var range in StationRanges)
        {
            for (int i = range.Start; i <= range.Stop; i++)
            {
                if (i == StationZone) continue;
                var otherZone = zoneArray[i];
                if (otherZone != null)
                {
                    if (distances[origin.ZoneNumber, otherZone.ZoneNumber] < ourDistance) return false;
                }
            }
        }
        return true;
    }

    private float CalculateEgressUtility(int flatEgress, int flatDestination, Time time)
    {
        var flatInterchanceZone = Root.ZoneSystem.ZoneArray.GetFlatIndex(InterchangeZone.ZoneNumber);
        // If Access
        if (Access)
        {
            return ComputeSubV(Second, flatInterchanceZone, flatEgress, time, InVehicleTravelTime, WalkTime, WaitTime, BoardingTime, CostFactor)
            + ComputeSubV(Third, flatEgress, flatDestination, time, InVehicleTravelTime, WalkTime, WaitTime, BoardingTime, CostFactor);
        }
        return ComputeSubV(Second, flatEgress, flatInterchanceZone, time, InVehicleTravelTime, WalkTime, WaitTime, BoardingTime, CostFactor)
               + ComputeSubV(Third, flatDestination, flatEgress, time, InVehicleTravelTime, WalkTime, WaitTime, BoardingTime, CostFactor);
    }

    private void CheckInterchangeZone()
    {
        if (!CacheLoaded)
        {
            lock (this)
            {
                Thread.MemoryBarrier();
                if (!CacheLoaded)
                {
                    var zones = Root.ZoneSystem.ZoneArray;
                    var distances = Root.ZoneSystem.Distances;
                    var zone = zones[StationZone];
                    InterchangeZone = zone ?? throw new XTMFRuntimeException(this, "The zone " + StationZone + " does not exist!  Please check the mode '" + ModeName + "!");
                    ClosestZone = zones.CreateSimilarArray<bool>();
                    var flatZones = zones.GetFlatData();
                    for (int i = 0; i < flatZones.Length; i++)
                    {
                        ClosestZone[zones.GetSparseIndex(i)] = AreWeClosest(flatZones[i], zones, distances);
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
        if (!LocalTransitCacheLoaded)
        {
            lock (this)
            {
                Thread.MemoryBarrier();
                if (!LocalTransitCacheLoaded)
                {
                    DumpCaches();
                    Thread.MemoryBarrier();
                }
            }
        }
        var egressChoice = EgressChoiceCache.GetFlatData()[flatDestination];
        if (egressChoice != null)
        {
            return egressChoice;
        }
        int bestEgressZone = -1;
        bestTime = float.MaxValue;
        var zones = Root.ZoneSystem.ZoneArray;
        foreach (var set in StationRanges)
        {
            for (int i = set.Start; i <= set.Stop; i++)
            {
                if (i == StationZone) continue;
                var flatEgressZone = zones.GetFlatIndex(i);
                if (GetEgressTravelTime(flatEgressZone, flatDestination, time, bestTime, out float tt))
                {
                    if (tt < bestTime)
                    {
                        bestTime = tt;
                        bestEgressZone = flatEgressZone;
                    }
                }
            }
        }
        if (bestEgressZone < 0)
        {
            return (EgressChoiceCache.GetFlatData()[flatDestination] = new EgressZoneChoice { Zones = null, EgressUtility = float.NaN });
        }
        return (EgressChoiceCache.GetFlatData()[flatDestination] = new EgressZoneChoice { Zones = zones.GetFlatData()[bestEgressZone], EgressUtility = CalculateEgressUtility(bestEgressZone, flatDestination, time) });
    }

    private bool GetEgressTravelTime(int flatEgressZone, int flatDestinationZone, Time time, float bestTime, out float tt)
    {
        var flatInterchangeZone = Root.ZoneSystem.ZoneArray.GetFlatIndex(InterchangeZone.ZoneNumber);
        tt = float.MaxValue;
        // make sure that we can actually travel to the end station
        if (!Second.ValidOd(flatInterchangeZone, flatEgressZone, time))
        {
            return false;
        }
        // now that we know it is possible go and get that travel time
        var lineHaul = Second.InVehicleTravelTime(flatInterchangeZone, flatEgressZone, time).ToMinutes();
        // if the travel time is zero, then this is an invalid option
        if (lineHaul == 0) return false;
        if (!Parent.GetEgressUtility(flatEgressZone, flatDestinationZone, time, out float egressUtility))
        {
            return false;
        }
        tt = lineHaul + egressUtility;
        // if we are not already better than the best continue
        if (tt >= bestTime)
        {
            return false;
        }
        // now make sure that tt is actually smaller than just using transit all way
        float localAllWayTime;
        if (Third.ValidOd(flatInterchangeZone, flatDestinationZone, time))
        {
            if (ComputeThird(Third, flatInterchangeZone, flatDestinationZone, time, EgressWalkFactor, EgressWaitFactor, out float result))
            {
                localAllWayTime = result;
            }
            else
            {
                localAllWayTime = float.MaxValue;
            }
        }
        else
        {
            localAllWayTime = float.MaxValue;
        }
        return tt < localAllWayTime;
    }

    private bool ItermediateZoneCloserThanDestination(int origin, int destination, int flatInt)
    {
        var distances = Root.ZoneSystem.Distances.GetFlatData();
        return distances[origin][flatInt] < distances[origin][destination];
    }

    private sealed class EgressZoneChoice
    {
        internal float EgressUtility;
        internal IZone Zones;
    }
}