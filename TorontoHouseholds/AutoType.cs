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
using System.Linq;
using Tasha.Common;
using XTMF;

namespace TashaModes
{
    [ModuleInformation(Description = "This module represents a physical auto vehicle in a TASHA simulation.")]
    public class AutoType : IVehicleType
    {
        #region IVehicleType Members

        public bool Finite
        {
            get { return true; }
        }

        [RunParameter( "Vehicle Name", "Auto", "The name of the vehicle type" )]
        /// <summary>
        /// The name of the type of vehicle
        /// </summary>
        public string VehicleName
        {
            get;
            set;
        }

        /// <summary>
        /// Tests to see if a person can use a vehicle of this type
        /// </summary>
        /// <param name="person">The person to test for</param>
        /// <returns>If they can use it or not</returns>
        public bool CanUse(ITashaPerson person)
        {
            return person.Licence && person.Household.Vehicles.Count( n => n.VehicleType.Equals( this ) ) > 0;
        }

        #endregion IVehicleType Members

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}