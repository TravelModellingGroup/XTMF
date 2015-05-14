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

namespace Tasha.V4Modes
{
    [ModuleInformation(Description =
        @"This module is designed to implement the Passenger mode for GTAModel V4.0+.")]
    public sealed class Passenger : ITashaPassenger, IIterationSensitive
    {
        [DoNotAutomate]
        public INetworkData AutoData;

        [RootModule]
        public ITashaRuntime Root;

        [RunParameter("ProfessionalConstant", 0f, "The constant applied to the person type.")]
        public float ProfessionalConstant;
        [RunParameter("GeneralConstant", 0f, "The constant applied to the person type.")]
        public float GeneralConstant;
        [RunParameter("SalesConstant", 0f, "The constant applied to the person type.")]
        public float SalesConstant;
        [RunParameter("ManufacturingConstant", 0f, "The constant applied to the person type.")]
        public float ManufacturingConstant;
        [RunParameter("StudentConstant", 0f, "The constant applied to the person type.")]
        public float StudentConstant;
        [RunParameter("NonWorkerStudentConstant", 0f, "The constant applied to the person type.")]
        public float NonWorkerStudentConstant;

        [RunParameter("Female Flag", 0f, "Added to the utility if the person is female.")]
        public float FemaleFlag;

        [RunParameter("IntrazonalDriverConstant", 0f, "The mode constant if all of the trip legs are intrazonal.")]
        public float IntrazonalConstant;

        [RunParameter("IntrazonalDriverTripDistanceFactor", 0f, "The factor to apply to the intrazonal trip distance.")]
        public float IntrazonalDriverTripDistanceFactor;

        [RunParameter("IntrazonalPassengerTripDistanceFactor", 0f, "The factor to apply to the intrazonal trip distance.")]
        public float IntrazonalPassengerTripDistanceFactor;

        [RootModule]
        public ITashaRuntime TashaRuntime;

        [RunParameter("MarketFlag", 0f, "Added to the utility if the trip's purpose is market.")]
        public float MarketFlag;

        [RunParameter("OtherFlag", 0f, "Added to the utility if the trip's purpose is 'other'.")]
        public float OtherFlag;

        [RunParameter("SchoolFlag", 0f, "Added to the utility if the trip's purpose is 'School'.")]
        public float SchoolFlag;

        [RunParameter("ProfessionalTravelCostFactor", 0f, "The factor applied to the travel cost ($'s).")]
        public float ProfessionalCostFactor;

        [RunParameter("GeneralTravelCostFactor", 0f, "The factor applied to the travel cost ($'s).")]
        public float GeneralCostFactor;

        [RunParameter("SalesTravelCostFactor", 0f, "The factor applied to the travel cost ($'s).")]
        public float SalesCostFactor;

        [RunParameter("ManufacturingTravelCostFactor", 0f, "The factor applied to the travel cost ($'s).")]
        public float ManufacturingCostFactor;

        [RunParameter("StudentTravelCostFactor", 0f, "The factor applied to the travel cost ($'s).")]
        public float StudentCostFactor;

        [RunParameter("NonWorkerStudentTravelCostFactor", 0f, "The factor applied to the travel cost ($'s).")]
        public float NonWorkerStudentCostFactor;

        [RunParameter("ParkingCost", 0f, "The factor applied to the cost of parking for the destination zone.")]
        public float ParkingCost;

        [RunParameter("ProfessionalTimeFactor", 0f, "The TimeFactor applied to the person type.")]
        public float ProfessionalTimeFactor;
        [RunParameter("GeneralTimeFactor", 0f, "The TimeFactor applied to the person type.")]
        public float GeneralTimeFactor;
        [RunParameter("SalesTimeFactor", 0f, "The TimeFactor applied to the person type.")]
        public float SalesTimeFactor;
        [RunParameter("ManufacturingTimeFactor", 0f, "The TimeFactor applied to the person type.")]
        public float ManufacturingTimeFactor;
        [RunParameter("StudentTimeFactor", 0f, "The TimeFactor applied to the person type.")]
        public float StudentTimeFactor;
        [RunParameter("NonWorkerStudentTimeFactor", 0f, "The TimeFactor applied to the person type.")]
        public float NonWorkerStudentTimeFactor;

        [RunParameter("LogOfAgeFactor", 0f, "The factor applied to the log of age.")]
        public float LogOfAgeFactor;

        [RunParameter("Over65", 0f, "The factor applied if the person is over the age of 65..")]
        public float Over65;

        [RunParameter("Over55", 0f, "The factor applied if the person is over the age of 55, but less than 65.")]
        public float Over55;

        [RunParameter("PassengerHasLicenseFlag", 0f, "Added to the utility if the passenger has a license.")]
        public float PassengerHasLicenseFlag;

        [RunParameter("ShareBothPointsFlag", 0f, "Added if the passenger and driver share both origin and destination zones.")]
        public float ShareBothPointsFlag;

        [RunParameter("ShareAPointFlag", 0f, "Added if the passenger and driver share one of their origin and destination zones.")]
        public float ShareAPointFlag;

        [RunParameter("Max Passenger Time", "45 minutes", typeof(Time), "The amount of time that the passenger can shift their trip by.")]
        public Time MaxPassengerTimeThreshold;

        [RunParameter("Max Driver Time", "15 minutes", typeof(Time), "The amount of time that the driver can shift their trip by.")]
        public Time MaxDriverTimeThreshold;

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

        [RunParameter("Mode Name", "Passenger", "The name of the mode")]
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

        public char ObservedMode
        {
            get;
            set;
        }

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
            get { return TashaRuntime.AutoType; }
        }

        [RunParameter("Variance Scale", 1.0f, "The scale for variance used for variance testing.")]
        public double VarianceScale
        {
            get;
            set;
        }

        public bool CalculateV(ITrip driverOriginalTrip, ITrip passengerTrip, out float v)
        {
            Time dToPTime;
            Time tToPD;
            Time tToDD;
            v = float.NegativeInfinity;
            if(!IsThereEnoughTime(driverOriginalTrip, passengerTrip, out dToPTime, out tToPD, out tToDD))
            {
                return false;
            }
            var zoneDistances = Root.ZoneSystem.Distances;
            // Since this is going to be valid, start building a real utility!
            v = 0f;
            IZone passengerOrigin = passengerTrip.OriginalZone;
            IZone driverOrigin = driverOriginalTrip.OriginalZone;
            var sameOrigin = passengerOrigin == driverOrigin;
            IZone passengerDestination = passengerTrip.DestinationZone;
            var sameDestination = passengerDestination == driverOriginalTrip.DestinationZone;
            var passenger = passengerTrip.TripChain.Person;
            // we are going to add in the time of the to passenger destination twice
            var zeroTime = Time.Zero;
            int same = 0;
            // from driver's origin to passenger's origin
            if(dToPTime == zeroTime)
            {
                v += zoneDistances[driverOrigin.ZoneNumber,
                    passengerOrigin.ZoneNumber] * 0.001f * IntrazonalDriverTripDistanceFactor;
                same++;
            }
            //from passenger origin to passenger destination
            if(tToPD == zeroTime)
            {
                v += zoneDistances[passengerOrigin.ZoneNumber,
                    passengerDestination.ZoneNumber] * 0.001f * IntrazonalPassengerTripDistanceFactor;
                same++;
            }
            // time to driver destination
            if(tToDD == zeroTime)
            {
                v += zoneDistances[passengerDestination.ZoneNumber,
                    driverOriginalTrip.DestinationZone.ZoneNumber] * 0.001f * IntrazonalDriverTripDistanceFactor;
                same++;
            }
            float timeFactor, passengerConstant, costFactor;
            GetPersonVariables(passenger, out timeFactor, out passengerConstant, out costFactor);
            // if all three are the same then apply the intrazonal constant, otherwise use the regular constant
            v += passengerConstant;
            if(same == 3)
            {
                v += IntrazonalConstant;
            }
            // apply the travel times (don't worry if we have intrazonals because they will be 0's).
            v += ((dToPTime + tToPD + tToDD) + tToPD).ToMinutes() * timeFactor;
            // Add in the travel cost
            v += (
                (AutoData.TravelCost(driverOrigin, passengerOrigin, passengerTrip.ActivityStartTime)
                + AutoData.TravelCost(passengerOrigin, passengerDestination, passengerTrip.ActivityStartTime)
                + AutoData.TravelCost(passengerDestination, driverOriginalTrip.DestinationZone, passengerTrip.ActivityStartTime))
                ) * costFactor;
            switch(passengerTrip.Purpose)
            {
                case Activity.School:
                    v += SchoolFlag;
                    break;
                case Activity.IndividualOther:
                case Activity.JointOther:
                    v += OtherFlag;
                    break;
                case Activity.Market:
                case Activity.JointMarket:
                    v += MarketFlag;
                    break;
            }

            if(passenger.Female) v += FemaleFlag;
            if(passenger.Licence) v += PassengerHasLicenseFlag;
            if(sameOrigin | sameDestination)
            {
                v += (sameOrigin & sameDestination) ? ShareBothPointsFlag : ShareAPointFlag;
            }
            var age = passenger.Age;
            v += AgeUtilLookup[Math.Min(Math.Max(age - 15, 0), 15)];
            if(age >= 65)
            {
                v += Over65;
            }
            else if(age >= 55)
            {
                v += Over55;
            }
            return true;
        }

        private void GetPersonVariables(ITashaPerson person, out float time, out float constant, out float cost)
        {
            if(person.EmploymentStatus == TTSEmploymentStatus.FullTime)
            {
                switch(person.Occupation)
                {
                    case Occupation.Professional:
                        cost = ProfessionalCostFactor;
                        constant = ProfessionalConstant;
                        time = ProfessionalTimeFactor;
                        return;
                    case Occupation.Office:
                        cost = GeneralCostFactor;
                        constant = GeneralConstant;
                        time = GeneralTimeFactor;
                        return;
                    case Occupation.Retail:
                        cost = SalesCostFactor;
                        constant = SalesConstant;
                        time = SalesTimeFactor;
                        return;
                    case Occupation.Manufacturing:
                        cost = ManufacturingCostFactor;
                        constant = ManufacturingConstant;
                        time = ManufacturingTimeFactor;
                        return;
                }
            }
            switch(person.StudentStatus)
            {
                case StudentStatus.FullTime:
                case StudentStatus.PartTime:
                    cost = StudentCostFactor;
                    constant = StudentConstant;
                    time = StudentTimeFactor;
                    return;
            }
            if(person.EmploymentStatus == TTSEmploymentStatus.PartTime)
            {
                switch(person.Occupation)
                {
                    case Occupation.Professional:
                        cost = ProfessionalCostFactor;
                        constant = ProfessionalConstant;
                        time = ProfessionalTimeFactor;
                        return;
                    case Occupation.Office:
                        cost = GeneralCostFactor;
                        constant = GeneralConstant;
                        time = GeneralTimeFactor;
                        return;
                    case Occupation.Retail:
                        cost = SalesCostFactor;
                        constant = SalesConstant;
                        time = SalesTimeFactor;
                        return;
                    case Occupation.Manufacturing:
                        cost = ManufacturingCostFactor;
                        constant = ManufacturingConstant;
                        time = ManufacturingTimeFactor;
                        return;
                }
            }
            cost = NonWorkerStudentCostFactor;
            constant = NonWorkerStudentConstant;
            time = NonWorkerStudentTimeFactor;
            return;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="trip"></param>
        /// <returns></returns>
        public double CalculateV(ITrip trip)
        {
            throw new NotImplementedException("Please use a mode choice algorithm that knows about passenger mode.");
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            // This doesn't even make sense without having households
            throw new NotImplementedException("Please use a mode choice algorithm that knows about passenger mode.");
        }

        /// <summary>
        /// Basic auto gas price from origin zone to destination zone
        /// </summary>return trip.TripChain.Person.Licence
        /// <param name="origin"></param>
        /// <param name="destination"></param>
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
            throw new NotImplementedException("Please use a mode choice algorithm that knows about passenger mode.");
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
            if(networks == null)
            {
                error = "There was no Auto Network loaded for the Passenger Mode!";
                return false;
            }
            bool found = false;
            foreach(var network in networks)
            {
                if(network.NetworkType == NetworkType)
                {
                    AutoData = network;
                    found = true;
                    break;
                }
            }
            if(!found)
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

        private bool IsThereEnoughTime(ITrip driverOriginalTrip, ITrip passengerTrip, out Time dToPTime, out Time tToPD, out Time tToDD)
        {
            // Check to see if the driver is able to get there
            Time earliestPassenger = passengerTrip.ActivityStartTime - MaxPassengerTimeThreshold;
            Time latestPassenger = passengerTrip.ActivityStartTime + MaxPassengerTimeThreshold;
            Time originalDriverTime = driverOriginalTrip.ActivityStartTime - driverOriginalTrip.TripStartTime;
            IZone passengerOrigin = passengerTrip.OriginalZone;
            IZone driverOrigin = driverOriginalTrip.OriginalZone;
            // check to see if the driver is able to get to their destination
            var timeToPassenger = dToPTime = TravelTime(driverOrigin, passengerOrigin, driverOriginalTrip.TripStartTime);
            var driverArrivesAt = driverOriginalTrip.TripStartTime + timeToPassenger;
            var earliestDriver = driverArrivesAt - MaxDriverTimeThreshold;
            var latestDriver = driverArrivesAt + MaxDriverTimeThreshold;
            Time overlapStart, overlapEnd;
            if(!Time.Intersection(earliestPassenger, latestPassenger, earliestDriver, latestDriver, out overlapStart, out overlapEnd))
            {
                tToPD = Time.Zero;
                tToDD = Time.Zero;
                return false;
            }

            IZone passengerDestination = passengerTrip.DestinationZone;
            var midLegTravelTime = tToPD = TravelTime(passengerOrigin, passengerDestination, latestDriver);
            IZone driverDestination = driverOriginalTrip.DestinationZone;
            if(passengerDestination != driverDestination)
            {
                tToDD = TravelTime(passengerDestination, driverDestination, latestDriver + midLegTravelTime);
            }
            else
            {
                tToDD = Time.Zero;
            }
            if(overlapStart + timeToPassenger + midLegTravelTime + tToDD > driverOriginalTrip.ActivityStartTime + MaxDriverTimeThreshold)
            {
                return false;
            }
            return true;
        }


        public void IterationEnding(int iterationNumber, int maxIterations)
        {

        }

        private float[] AgeUtilLookup;
        public void IterationStarting(int iterationNumber, int maxIterations)
        {
            AgeUtilLookup = new float[16];
            for(int i = 0; i < AgeUtilLookup.Length; i++)
            {
                AgeUtilLookup[i] = (float)Math.Log(i + 1, Math.E) * LogOfAgeFactor;
            }
        }
    }
}