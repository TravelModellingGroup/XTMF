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
using System.Collections.Generic;
using TMG;

namespace Tasha.Common
{
    /// <summary>
    /// Defines how to interface with a household
    /// </summary>
    public interface ITashaHousehold : IAttachable
    {
        DwellingType DwellingType { get; }

        /// <summary>
        /// How many households this represents
        /// </summary>
        float ExpansionFactor { get; set; }

        HouseholdType HhType { get; }

        /// <summary>
        /// The zone this household is in
        /// </summary>
        IZone HomeZone { get; set; }

        /// <summary>
        /// Gives identification to the household
        /// This can be used for ordering of the output
        /// </summary>
        int HouseholdId { get; set; }

        Dictionary<int, List<ITripChain>> JointTours { get; }

        int NumberOfAdults { get; }

        int NumberOfChildren { get; }

        /// <summary>
        /// The people in this household
        /// </summary>
        ITashaPerson[] Persons { get; set; }

        /// <summary>
        /// The vehicles that belong to this household
        /// </summary>
        IVehicle[] Vehicles { get; }

        ITashaHousehold Clone();

        /// <summary>
        /// Gets a List of Persons associated wit the given tour ID
        /// </summary>
        /// <param name="tourID"></param>
        /// <returns>The List of Persons on the tour</returns>
        List<ITashaPerson> GetJointTourMembers(int tourID);

        /// <summary>
        /// Gets a List of Persons associated with the given tour ID
        /// </summary>
        /// <param name="tourID"></param>
        /// <param name="person"></param>
        /// <returns>The List of Persons on the tour</returns>
        ITripChain GetJointTourTripChain(int tourID, ITashaPerson person);

        void Recycle();
    }
}