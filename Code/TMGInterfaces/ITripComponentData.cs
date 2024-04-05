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
using XTMF;

namespace TMG;

public interface ITripComponentData : INetworkData
{
    Time BoardingTime(IZone origin, IZone destination, Time time);

    Time BoardingTime(int flatOrigin, int flatDestination, Time time);

    int[] ClosestStations(IZone zone);

    bool GetAllData(IZone origin, IZone destination, Time time, out Time ivtt, out Time walk, out Time wait, out Time boarding, out float cost);

    bool GetAllData(IZone origin, IZone destination, Time time, out float ivtt, out float walk, out float wait, out float boarding, out float cost);

    bool GetAllData(int flatOrigin, int flatDestination, Time time, out Time ivtt, out Time walk, out Time wait, out Time boarding, out float cost);

    bool GetAllData(int flatOrigin, int flatDestination, Time time, out float ivtt, out float walk, out float wait, out float boarding, out float cost);

    Time InVehicleTravelTime(IZone origin, IZone destination, Time time);

    Time InVehicleTravelTime(int flatOrigin, int flatDestination, Time time);

    ITransitStation Station(IZone stationZone);

    Time WaitTime(IZone origin, IZone destination, Time time);

    Time WaitTime(int flatOrigin, int flatDestination, Time time);

    Time WalkTime(IZone origin, IZone destination, Time time);

    Time WalkTime(int flatOrigin, int flatDestination, Time time);
}

public interface ITripComponentCompleteData : ITripComponentData
{
    /// <summary>
    /// Returns all of the data for the time period in an array with the following format.
    /// This method is not recommended unless performance is key.
    /// For each origin, for each destination, [time,wait,walk,cost,boarding]
    /// </summary>
    /// <returns></returns>
    float[] GetTimePeriodData(Time time);
}