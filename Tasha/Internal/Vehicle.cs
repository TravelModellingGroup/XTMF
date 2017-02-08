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

using Tasha.Common;

namespace Tasha.Internal
{
    /// <summary>
    /// This represents a vehicle in the simulation
    /// </summary>
    internal sealed class TashaVehicle : Attachable, IVehicle
    {
        private TashaVehicle(IVehicleType type)
        {
            VehicleType = type;
        }

        /// <summary>
        /// The household this vehical belongs to
        /// </summary>
        public ITashaHousehold Household
        {
            get
            {
                throw new System.NotImplementedException();
            }
        }

        /// <summary>
        /// The modes this vehicle uses
        /// </summary>
        public ICollection<ITashaMode> Modes
        {
            get { return null; }
        }

        /// <summary>
        /// Gets the type of vehicle this is
        /// </summary>
        public IVehicleType VehicleType { get; private set; }

        public static TashaVehicle MakeVehicle(IVehicleType type)
        {
            return new TashaVehicle( type );
        }

        public void Recycle()
        {
        }
    }
}