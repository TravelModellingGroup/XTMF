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
using System.Collections.Generic;
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Modes
{
    /// <summary>
    /// This defines the Auto mode
    /// </summary>
    public sealed class Auto : ITashaMode
    {
        [RunParameter("Constant", 0f, "The constant for this mode.")]
        public float Constant;

        [RunParameter("dpurp_oth_drive", 0f, "The weight for the cost of doing an other drive (ITashaRuntime only)")]
        public float dpurp_oth_drive;

        [RunParameter("dpurp_shop_drive", 0f, "The weight for the cost of doing a shopping drive (ITashaRuntime only)")]
        public float dpurp_shop_drive;

        [RunParameter("Intrazonal Constant", 0f, "The constant to use for intrazonal trips.")]
        public float IntrazonalConstantWeight;

        [RunParameter("Intrazonal Distance", 0f, "The parameter applied to the distance of an intrazonal trip.")]
        public float IntrazonalDistanceWeight;

        [RunParameter("pkcost", 0f, "The weight for cost of parking")]
        public float parking;

        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter("travelcost", 0f, "The weight for cost of travel")]
        public float travelCost;

        [RunParameter("atime", 0f, "The weight cost of time")]
        public float travelTime;

        [RunParameter("Use Intrazonal Regression", false, "Should we use a regression for intrazonal trips based on the interzonal distance?")]
        public bool UseIntrazonalRegression;

        [RunParameter("Vehicle Type Name", "", "If the mode is Auto, leave blank, otherwise, search other vehicle type.")]
        public string VehicleTypeName;

        [DoNotAutomate]
        private INetworkData AutoData;

        [DoNotAutomate]
        private IVehicleType AutoType;

        [DoNotAutomate]
        private ITashaRuntime TashaRuntime;

        /// <summary>
        /// Create a new Auto mode, this will be called when
        /// Tasha# loads us
        /// </summary>
        public Auto()
        {
        }

        [Parameter("Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?")]
        public float CurrentlyFeasible { get; set; }

        [RunParameter("Name", "Auto", "The name of the mode")]
        public string ModeName { get; set; }

        /// <summary>
        /// What is the name of this mode?
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        [RunParameter("Network Name", "Auto", "The name of the network that this mode uses.")]
        public string NetworkType
        {
            get;
            set;
        }

        /// <summary>
        /// We are a personal vehicle
        /// </summary>
        public bool NonPersonalVehicle
        {
            get { return false; }
        }

        /// <summary>
        /// The character that is to be written to the output file for the chosen mode
        /// </summary>
        [RunParameter("Output Signature", 'A', "The output signature for saving trips.")]
        public char OutputSignature
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
            get { return new Tuple<byte, byte, byte>(100, 200, 100); }
        }

        [DoNotAutomate]
        /// <summary>
        /// Does this require a vehicle
        /// </summary>
        public IVehicleType RequiresVehicle
        {
            get { return AutoType; }
        }

        [RunParameter("Variance Scale", 1.0f, "The scale for varriance used for variance testing.")]
        public double VarianceScale
        {
            get;
            set;
        }

        /// <summary>
        /// Calcualtes the V value for the given trip
        /// </summary>
        /// <param name="trip">The trip to calcualte for</param>
        /// <returns>The V for the trip</returns>
        public double CalculateV(ITrip trip)
        {
            double V = 0;
            IZone o = trip.OriginalZone, d = trip.DestinationZone;
            if((o == d) & UseIntrazonalRegression)
            {
                V += IntrazonalConstantWeight + o.InternalDistance * IntrazonalDistanceWeight;
            }
            else
            {
                V += this.Constant;
                V += AutoData.TravelTime(o, d, trip.ActivityStartTime).ToMinutes() * this.travelTime;
                V += AutoData.TravelCost(o, d, trip.ActivityStartTime) * this.travelCost;
            }
            V += d.ParkingCost * parking;
            if(trip.Purpose == Activity.Market | trip.Purpose == Activity.JointMarket)
            {
                V += this.dpurp_shop_drive;
            }
            else if(trip.Purpose == Activity.IndividualOther | trip.Purpose == Activity.JointOther)
            {
                V += this.dpurp_oth_drive;
            }
            return V;
        }

        /// <summary>
        /// Calculate the V for going between 2 zones at a given time
        /// </summary>
        /// <param name="o">The zone we start from</param>
        /// <param name="d">The zone we go to</param>
        /// <param name="time">The time we start travelling</param>
        /// <returns>The V of the utility function</returns>
        public float CalculateV(IZone o, IZone d, Time time)
        {
            float V = 0;
            V += TravelTime(o, d, time).ToMinutes() * this.travelTime;
            V += AutoData.TravelCost(o, d, time) * this.travelCost;
            V += d.ParkingCost * parking;
            return V;
        }

        public float Cost(IZone origin, IZone destination, Time time)
        {
            return AutoData.TravelCost(origin, destination, time);
        }

        public bool Feasible(IZone origin, IZone destination, Time timeOfDay)
        {
            return (origin == destination && UseIntrazonalRegression) || (CurrentlyFeasible > 0 & this.AutoData.ValidOD(origin, destination, timeOfDay));
        }

        /// <summary>
        /// Is this trip feasible?
        /// </summary>
        /// <param name="trip">The trip to test for</param>
        /// <returns>True if we can do this</returns>
        public bool Feasible(ITrip trip)
        {
            var person = trip.TripChain.Person;
            if(!person.Licence) return false;
            var vehicles = person.Household.Vehicles;
            var length = vehicles.Length;
            for(int i = 0; i < length; i++)
            {
                if(vehicles[i].VehicleType == this.AutoType)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks to see if a whole chain of trips is possible
        /// </summary>
        /// <param name="tripChain">The chain to check</param>
        /// <returns>If it is possible</returns>
        public bool Feasible(ITripChain tripChain)
        {
            int vehicleLeftAt = tripChain.Person.Household.HomeZone.ZoneNumber;
            var home = vehicleLeftAt;
            var trips = tripChain.Trips;
            var length = trips.Count;
            bool noAutoTrips = true;
            bool first = false;
            bool lastMadeWithAuto = false;
            for(int i = 0; i < length; i++)
            {
                var trip = trips[i];
                var mode = trip.Mode;
                if(!mode.NonPersonalVehicle)
                {
                    if(mode.RequiresVehicle == this.AutoType)
                    {
                        // it is only not feasible if we actually take the mode and we don't have a licence
                        if((trip.OriginalZone.ZoneNumber != vehicleLeftAt))
                        {
                            return false;
                        }
                        vehicleLeftAt = trip.DestinationZone.ZoneNumber;
                        lastMadeWithAuto = true;
                        noAutoTrips = false;
                    }
                    else
                    {
                        lastMadeWithAuto = false;
                    }
                }
                else
                {
                    lastMadeWithAuto = false;
                }
                if(i == 0)
                {
                    first = lastMadeWithAuto;
                }
            }
            return (noAutoTrips) | ((first) & (lastMadeWithAuto) & (vehicleLeftAt == home));
        }

        /// <summary>
        /// Load the network AutoData for this mode
        /// </summary>
        public void ReloadNetworkData()
        {
            this.AutoData.LoadData();
        }

        /// <summary>
        /// This is called before the start method as a way to pre-check that all of the parameters that are selected
        /// are in fact valid for this module.
        /// </summary>
        /// <param name="error">A string that should be assigned a detailed error</param>
        /// <returns>If the validation was successful or if there was a problem</returns>
        public bool RuntimeValidation(ref string error)
        {
            // if our deep ancestor is in fact a tasha runtime
            this.TashaRuntime = this.Root as ITashaRuntime;
            IList<INetworkData> networks;
            if(this.TashaRuntime == null)
            {
                // check for a 4Step model system template
                var tdm = this.Root as ITravelDemandModel;
                // if it isn't report the error
                if(tdm == null)
                {
                    error = "The Tasha.Modes.Auto Module only works with ITashaRuntime's and ITravelDemandModel's!";
                    return false;
                }
                networks = tdm.NetworkData;
            }
            else
            {
                if(String.IsNullOrWhiteSpace(this.VehicleTypeName))
                {
                    this.AutoType = this.TashaRuntime.AutoType;
                }
                else
                {
                    if(this.TashaRuntime.VehicleTypes != null)
                    {
                        foreach(var v in this.TashaRuntime.VehicleTypes)
                        {
                            if(v.VehicleName == this.VehicleTypeName)
                            {
                                this.AutoType = v;
                                break;
                            }
                        }
                    }
                }
                if(AutoType == null)
                {
                    error = "We were unable to find an vehicle type to use for '" + this.ModeName + "'!";
                    return false;
                }
                networks = this.TashaRuntime.NetworkData;
            }
            if(String.IsNullOrWhiteSpace(this.NetworkType))
            {
                error = "There was no network type selected for the " + (String.IsNullOrWhiteSpace(this.ModeName) ? "Auto" : this.ModeName) + " mode!";
                return false;
            }
            if(networks == null)
            {
                error = "There was no Auto Network loaded for the Auto Mode!";
                return false;
            }
            bool found = false;
            foreach(var network in networks)
            {
                if(network.NetworkType == this.NetworkType)
                {
                    this.AutoData = network;
                    found = true;
                    break;
                }
            }
            if(!found)
            {
                error = "In '" + Name + "' we were unable to find the network data with the name \"" + this.NetworkType + "\" in this Model System!";
                return false;
            }

            return true;
        }

        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return AutoData.TravelTime(origin, destination, time);
        }
    }
}