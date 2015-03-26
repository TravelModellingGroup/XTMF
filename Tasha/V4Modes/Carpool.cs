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

namespace Tasha.V4Modes
{
    [ModuleInformation(Description =
        @"This module is designed to implement the Carpool mode for GTAModel V4.0+.")]
    public sealed class Carpool : ITashaMode
    {
        [RunParameter("Constant", 0f, "The mode constant.")]
        public float Constant;

        [RunParameter("Female Flag", 0f, "Added to the utility if the person is female.")]
        public float FemaleFlag;

        [RunParameter("IntrazonalConstant", 0f, "The mode constant.")]
        public float IntrazonalConstant;

        [RunParameter("IntrazonalTripDistanceFactor", 0f, "The factor to apply to the intrazonal trip distance.")]
        public float IntrazonalTripDistanceFactor;

        [RunParameter("MarketFlag", 0f, "Added to the utility if the trip's purpose is market.")]
        public float MarketFlag;

        [RunParameter("OtherFlag", 0f, "Added to the utility if the trip's purpose is 'other'.")]
        public float OtherFlag;

        [RootModule]
        public ITashaRuntime Root;

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

        private INetworkData Network;

        [Parameter("Feasible", 1f, "Is the mode feasible?(1)")]
        public float CurrentlyFeasible { get; set; }

        [Parameter("Mode Name", "Carpool", "The name of the mode.")]
        public string ModeName { get; set; }

        public string Name { get; set; }

        [RunParameter("Network Name", "Auto", "The name of the network to use for times.")]
        public string NetworkType { get; set; }

        public bool NonPersonalVehicle
        {
            get { return true; }
        }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        [DoNotAutomate]
        public IVehicleType RequiresVehicle { get { return null; } }

        [RunParameter("Variance Scale", 1.0, "The factor applied to the error term.")]
        public double VarianceScale { get; set; }

        public double CalculateV(ITrip trip)
        {
            // compute the non human factors
            var zoneSystem = Root.ZoneSystem;
            var zoneArray = zoneSystem.ZoneArray;
            var o = zoneArray.GetFlatIndex(trip.OriginalZone.ZoneNumber);
            var d = zoneArray.GetFlatIndex(trip.DestinationZone.ZoneNumber);
            var p = trip.TripChain.Person;
            float timeFactor, constant, costFactor;
            GetPersonVariables(p,out timeFactor, out constant, out costFactor);
            float v = constant;
            
            // if Intrazonal
            if(o == d)
            {
                v += IntrazonalConstant;
                v += IntrazonalTripDistanceFactor * zoneSystem.Distances.GetFlatData()[o][d] * 0.001f;
            }
            else
            {
                // if not intrazonal
                var startTime = trip.TripStartTime;
                float aivtt, cost;
                Network.GetAllData(o, d, startTime, out aivtt, out cost);
                v += timeFactor * aivtt + costFactor * cost;
            }
            // Apply personal factors
            if(p.Female)
            {
                v += FemaleFlag;
            }
            //Apply trip purpose factors
            switch(trip.Purpose)
            {
                case Activity.Market:
                    v += MarketFlag;
                    break;
                case Activity.IndividualOther:
                    v += OtherFlag;
                    break;
            }
            return (double)v;
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
            cost = NonWorkerStudentCostFactor;
            constant = NonWorkerStudentConstant;
            time = NonWorkerStudentTimeFactor;
            return;
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            return 0f;
        }

        public float Cost(IZone origin, IZone destination, Time time)
        {
            return Network.TravelCost(origin, destination, time);
        }

        public bool Feasible(ITrip trip)
        {
            return true;
        }

        public bool Feasible(ITripChain tripChain)
        {
            return true;
        }

        public bool Feasible(IZone origin, IZone destination, Time time)
        {
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            var networks = Root.NetworkData;

            if(string.IsNullOrWhiteSpace(NetworkType))
            {
                error = "There was no network type selected for the " + (string.IsNullOrWhiteSpace(ModeName) ? "Auto" : ModeName) + " mode!";
                return false;
            }
            if(networks == null)
            {
                error = "There was no Auto Network loaded for the Auto Mode!";
                return false;
            }
            if(!AssignNetwork(networks))
            {
                error = "We were unable to find the network data with the name \"" + NetworkType + "\" in this Model System!";
                return false;
            }
            return true;
        }

        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return Network.TravelTime(origin, destination, time);
        }

        private bool AssignNetwork(IList<INetworkData> networks)
        {
            foreach(var network in networks)
            {
                if(network.NetworkType == NetworkType)
                {
                    Network = network;
                    return true;
                }
            }
            return false;
        }
    }
}