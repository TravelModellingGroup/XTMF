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
using XTMF;

namespace TMG
{
    public interface INetworkData : IDataSource<INetworkData>
    {
        string NetworkType { get; }

        float TravelCost(IZone start, IZone end, Time time);

        float TravelCost(int flatOrigin, int flatDestination, Time time);

        Time TravelTime(IZone start, IZone end, Time time);

        Time TravelTime(int flatOrigin, int flatDestination, Time time);

        bool GetAllData(IZone start, IZone end, Time time, out Time ivtt, out float cost);

        bool GetAllData(int start, int end, Time time, out float ivtt, out float cost);

        bool ValidOd(IZone start, IZone end, Time time);

        bool ValidOd(int flatOrigin, int flatDestination, Time time);
    }

    public interface INetworkCompleteData : INetworkData
    {
        /// <summary>
        /// Returns all of the data for the time period in an array with the following format.
        /// This method is not recommended unless performance is key.
        /// For each origin, for each destination, [time,cost]
        /// </summary>
        /// <returns></returns>
        float[] GetTimePeriodData(Time time);
    }
}