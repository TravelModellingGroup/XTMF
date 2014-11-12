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
using TMG;

namespace Tasha.Common
{
    public interface IGoData : ITravelData
    {
        float EndTime { get; }

        float MinDistance { get; }

        float StartTime { get; }

        float GetAccessWaitTime(int zone, int station);

        float GetAccessWalkTime(int zone, int station);

        float GetAutoCost(int zone, int station);

        float GetAutoTime(int zone, int station);

        int[] GetClosestStations(int zone);

        float GetEgressWaitTime(int zone, int station);

        float GetEgressWalkTime(int zone, int station);

        float GetGoFair(int accessStation, int egressStation);

        float GetGoFrequency(int accessStation, int egressStation, float time);

        float GetLineHaulTime(int accessStation, int egressStation);

        ITransitStation GetStation(int stationNumber);

        float GetTotalTransitAccessTime(int originalZone, int accessStation);

        float GetTotalTransitEgressTime(int egressStation, int destinationZone);

        float GetTransitAccessTime(int originalZone, int accessStation);

        float GetTransitEgressTime(int egressStation, int destinationZone);

        float GetTransitFair(int zone, int station);
    }
}