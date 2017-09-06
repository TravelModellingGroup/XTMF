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
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Modes
{
    /// <summary>
    /// Essentially the same ride share except
    /// </summary>
    public sealed class Passenger : ITashaPassenger
    {
        [DoNotAutomate]
        public INetworkData AutoData;

        [RunParameter("ConnectingFacil", 0f, "The constant factor applied if there is a connecting chain")]
        public float ConnectingFacil;

        [RunParameter("cpass", 0f, "The constant factor applied to the passenger mode")]
        public float Cpass;

        [RunParameter("croundtrip_facil", 0f, "The constant factor applied if the trip is passenger all the way around")]
        public float CroundtripFacil;

        [RunParameter("dpurp_oth_drive", 0f, "The constant factor applied if the purpse of the trip is 'Other'")]
        public float DpurpOthDrive;

        [RunParameter("dpurp_sch_passenger", 0f, "The constant factor applied if the purpose is to facilitate the passenger")]
        public float DpurpSchPassenger;

        [RunParameter("dpurp_shop_drive", 0f, "The constant factor applied if the purpose of the trip is shopping")]
        public float DpurpShopDrive;

        [RunParameter("Max Driver Time", "15 minutes", typeof(Time), "The maximum ammount of duration that an activity for the driver can change.")]
        public Time MaxDriverTimeThreshold;

        [RunParameter("Max Passenger Time", "30 minutes", typeof(Time), "The maximum ammount of duration that an activity for the passenger can change.")]
        public Time MaxPassengerTimeThreshold;

        [RunParameter("pass_w_license", 0f, "The constant factor applied if the person has a license")]
        public float PassWLicense;

        [RunParameter("sex_f_passenger", 0f, "The constant factor applied if the passenger is female")]
        public float SexFPassenger;

        [RootModule]
        public ITashaRuntime TashaRuntime;

        [RunParameter("travelCost", 0f, "The factor applied to the travel cost")]
        public float TravelCost;

        [RunParameter("TravelTimeBeta", 0f, "The factor applied to the travel time")]
        public float TravelTimeBeta;

        public Passenger(float dpurpOthDrive)
        {
            DpurpOthDrive = dpurpOthDrive;
        }

        [DoNotAutomate]
        public ITashaMode AssociatedMode
        {
            get { return TashaRuntime.AutoMode; }
        }

        [Parameter("Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?")]
        public float CurrentlyFeasible { get; set; }

        /// <summary>
        ///
        /// </summary>
        public byte ModeChoiceArrIndex { get; set; }

        [RunParameter("Name", "Passenger", "The name of the mode")]
        public string ModeName { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        [RunParameter("Network Type", "Auto", "The name of the network data to use.")]
        public string NetworkType
        {
            get;
            set;
        }

        /// <summary>
        /// This does not require a personal vehicle
        /// </summary>
        public bool NonPersonalVehicle
        {
            get { return true; }
        }

        [RunParameter("Observed Mode Character Code", 'A', "The character code used for model estimation.")]
        public char ObservedMode
        {
            get;
            set;
        }

        /// <summary>
        /// Output signature for Passenger Type
        /// </summary>
        [RunParameter("Observed Signature Code", 'A', "The character code used for model output.")]
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

        /// <summary>
        /// Does this require a vehicle
        /// </summary>
        [DoNotAutomate]
        public IVehicleType RequiresVehicle
        {
            get { return TashaRuntime.AutoType; }
        }

        [RunParameter("Variance Scale", 1.0f, "The scale for varriance used for variance testing.")]
        public double VarianceScale
        {
            get;
            set;
        }

        public bool CalculateV(ITrip driverOriginalTrip, ITrip passengerTrip, out float v)
        {
            v = float.NegativeInfinity;
            if (!IsThereEnoughTime(driverOriginalTrip, passengerTrip, out Time toPassengerOrigin, out Time toPassengerDestination, out Time toDriverDestination))
            {
                return false;
            }
            // Since this is going to be valid, start building a real utility!
            v = Cpass;
            // we are going to add in the time of the to passenger destination twice
            v += ((toPassengerOrigin + toPassengerDestination + toDriverDestination).ToMinutes() + toPassengerDestination.ToMinutes()) * TravelTimeBeta;
            // Add in the travel cost
            v += Cost(passengerTrip.OriginalZone, passengerTrip.DestinationZone, passengerTrip.ActivityStartTime) * TravelCost;
            if (passengerTrip.Purpose == Activity.Market | passengerTrip.Purpose == Activity.JointMarket) v += DpurpShopDrive;
            if (passengerTrip.Purpose == Activity.IndividualOther | passengerTrip.Purpose == Activity.JointOther) v += DpurpOthDrive;
            if (passengerTrip.Purpose == Activity.School) v += DpurpSchPassenger;
            if (passengerTrip.TripChain.Person.Female) v += SexFPassenger;
            if (passengerTrip.TripChain.Person.Licence) v += PassWLicense;
            if (passengerTrip.OriginalZone == driverOriginalTrip.OriginalZone && passengerTrip.DestinationZone == driverOriginalTrip.DestinationZone)
            {
                v += CroundtripFacil;
            }
            else
            {
                if (passengerTrip.OriginalZone == driverOriginalTrip.OriginalZone) v += ConnectingFacil;
                if (passengerTrip.DestinationZone == driverOriginalTrip.DestinationZone) v += ConnectingFacil;
            }
            return true;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="trip"></param>
        /// <returns></returns>
        public double CalculateV(ITrip trip)
        {
            throw new NotImplementedException();
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            // This doesn't even make sense without having households
            throw new NotImplementedException();
        }

        /// <summary>
        /// Basic auto gas price from origin zone to destination zone
        /// </summary>return trip.TripChain.Person.Licence
        /// <param name="origin"></param>
        /// <param name="destination"></param>
        /// <param name="time"></param>
        /// <returns></returns>
        public float Cost(IZone origin, IZone destination, Time time)
        {
            return AutoData.TravelCost(origin, destination, time);
        }

        public bool Feasible(IZone origin, IZone destination, Time timeOfDay)
        {
            return CurrentlyFeasible > 0;
        }

        /// <summary>
        /// Checking if the trip is feasible
        /// </summary>
        /// <param name="trip">the trip to test feasibility on</param>
        /// <returns>Is this trip feasible?</returns>
        public bool Feasible(ITrip trip)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="tripChain"></param>
        /// <returns></returns>
        public bool Feasible(ITripChain tripChain)
        {
            //passenger mode does not handle joint trips
            return !tripChain.JointTrip;
        }

        /// <summary>
        ///
        /// </summary>
        public bool IsObservedMode(char observedMode)
        {
            return (observedMode == ObservedMode);
        }

        /// <summary>
        /// This is called before the start method as a way to pre-check that all of the parameters that are selected
        /// are in fact valid for this module.
        /// </summary>
        /// <param name="error">A string that should be assigned a detailed error</param>
        /// <returns>If the validation was successful or if there was a problem</returns>
        public bool RuntimeValidation(ref string error)
        {
            var networks = TashaRuntime.NetworkData;
            if (networks == null)
            {
                error = "There was no Auto Network loaded for the Passenger Mode!";
                return false;
            }
            bool found = false;
            foreach (var network in networks)
            {
                if (network.NetworkType == NetworkType)
                {
                    AutoData = network;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                error = "We were unable to find the network data with the name \"Auto\" in this Model System!";
                return false;
            }
            return true;
        }

        /// <summary>
        /// This gets the travel time between zones
        /// </summary>
        /// <param name="origin">Where to start</param>
        /// <param name="destination">Where to go</param>
        /// <param name="time">What time of day is it? (hhmm.ss)</param>
        /// <returns>The amount of time it will take</returns>
        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return AutoData.TravelTime(origin, destination, time);
        }

        private bool IsThereEnoughTime(ITrip driverOriginalTrip, ITrip passengerTrip, out Time toPassengerOrigin, out Time toPassengerDestination, out Time toDriverDestination)
        {
            // Check to see if the driver is able to get there
            Time earliestPassenger = passengerTrip.ActivityStartTime - MaxPassengerTimeThreshold;
            Time latestPassenger = passengerTrip.ActivityStartTime + MaxPassengerTimeThreshold;
            // check to see if the driver is able to get to their destination
            var timeToPassenger = toPassengerOrigin = TravelTime(driverOriginalTrip.OriginalZone, passengerTrip.OriginalZone, driverOriginalTrip.TripStartTime);
            var driverArrivesAt = driverOriginalTrip.TripStartTime + timeToPassenger;
            var earliestDriver = driverArrivesAt - MaxDriverTimeThreshold;
            var latestDriver = driverArrivesAt + MaxDriverTimeThreshold;
            if (!Time.Intersection(earliestPassenger, latestPassenger, earliestDriver, latestDriver, out Time overlapStart, out Time overlapEnd))
            {
                toPassengerDestination = Time.Zero;
                toDriverDestination = Time.Zero;
                return false;
            }
            var midLegTravelTime = toPassengerDestination = TravelTime(passengerTrip.OriginalZone, passengerTrip.DestinationZone, latestDriver);
            Time finalLegTravelTime = toDriverDestination = Time.Zero;
            if (passengerTrip.DestinationZone != driverOriginalTrip.DestinationZone)
            {
                finalLegTravelTime = TravelTime(passengerTrip.DestinationZone, driverOriginalTrip.DestinationZone, latestDriver + midLegTravelTime);
            }
            var totalDriverTime = timeToPassenger + midLegTravelTime + finalLegTravelTime;
            if (overlapStart + totalDriverTime > driverOriginalTrip.ActivityStartTime + MaxDriverTimeThreshold)
            {
                return false;
            }
            return true;
        }
    }
}