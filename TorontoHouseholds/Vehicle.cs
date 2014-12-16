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
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Tasha.Common
{
    /// <summary>
    /// This represents a vehicle in the simulation
    /// </summary>
    [Serializable()]
    public sealed class Vehicle : Attachable, IVehicle
    {
        private static ConcurrentBag<Vehicle> Vehicles = new ConcurrentBag<Vehicle>();

        private Vehicle(IVehicleType type)
        {
            this.VehicleType = type;
        }

        /// <summary>
        /// The household this vehical belongs to
        /// </summary>
        public ITashaHousehold Household
        {
            get
            {
                return null;
            }

            set
            {
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

        public static Vehicle MakeVehicle(IVehicleType type)
        {
            Vehicle v;
            if ( Vehicle.Vehicles.TryTake( out v ) )
            {
                v.VehicleType = type;
                return v;
            }
            return new Vehicle( type );
        }

        public void Recycle()
        {
            this.VehicleType = null;
            this.Release();
            if(Vehicles.Count < 100)
            {
                Vehicle.Vehicles.Add(this);
            }
        }
    }
}