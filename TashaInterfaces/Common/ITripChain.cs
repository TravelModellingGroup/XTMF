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
using XTMF;

namespace Tasha.Common
{
    /// <summary>
    /// The interface for how to interact with a
    /// Trip Chain
    /// </summary>
    public interface ITripChain : IAttachable
    {
        /// <summary>
        /// When does this trip chain end?
        /// </summary>
        Time EndTime { get; }

        /// <summary>
        ///
        /// </summary>
        ITripChain GetRepTripChain { get; }

        /// <summary>
        /// Is this a joint trip?
        /// </summary>
        bool JointTrip { get; }

        /// <summary>
        /// Returns a list of all trip chains on the the same joint tour as this one
        /// (if it is on a joint tour)
        /// </summary>
        List<ITripChain> JointTripChains { get; }

        /// <summary>
        /// What is the ID for this joint trip
        /// </summary>
        int JointTripID { get; }

        /// <summary>
        /// Is this person the representative of the joint trip?
        /// </summary>
        bool JointTripRep { get; }

        List<ITashaPerson> Passengers { get; }

        /// <summary>
        /// The person this chain belongs to
        /// </summary>
        ITashaPerson Person { get; set; }

        List<IVehicleType> RequiresVehicle { get; }

        /// <summary>
        /// When does this trip chain start?
        /// </summary>
        Time StartTime { get; }

        /// <summary>
        /// Trip Chain Requires Personal Vehicle
        /// </summary>
        bool TripChainRequiresPV { get; }

        /// <summary>
        /// The trips that belong in this chain
        /// </summary>
        List<ITrip> Trips { get; set; }

        ITripChain Clone();

        ITripChain DeepClone();

        void Recycle();
    }
}