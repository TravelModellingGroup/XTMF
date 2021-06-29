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
        @"This module is designed to implement the AutoDrive mode for GTAModel V4.0+.")]
    public sealed class AutoDrive : ITashaMode, IIterationSensitive
    {
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

        [RunParameter("SchoolFlag", 0f, "Added to the utility if the trip's purpose is 'school'.")]
        public float SchoolFlag;

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

        private float ProfessionalCost;
        private float GeneralCost;
        private float SalesCost;
        private float ManufacturingCost;
        private float StudentCost;
        private float NonWorkerStudentCost;

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

        [RunParameter("Vehicle Type", "Auto", "The name of the type of vehicle to use.")]
        public string VehicleTypeName;

        [RunParameter("LogOfAgeFactor", 0f, "The factor applied to the log of age.")]
        public float LogOfAgeFactor;

        [RunParameter("Over65", 0f, "The factor applied if the person is over the age of 65..")]
        public float Over65;

        [RunParameter("Over55", 0f, "The factor applied if the person is over the age of 55, but less than 65.")]
        public float Over55;

        [RunParameter("Maximum Hours For Parking", 4.0f, "The maximum hours to calculate the parking cost for.")]
        public float MaximumHoursForParking;

        private INetworkData Network;

        [Parameter("Feasible", 1f, "Is the mode feasible?(1)")]
        public float CurrentlyFeasible { get; set; }

        [Parameter("Mode Name", "Auto", "The name of the mode.")]
        public string ModeName { get; set; }

        public string Name { get; set; }

        [RunParameter("Network Name", "Auto", "The name of the network to use for times.")]
        public string NetworkType { get; set; }

        public bool NonPersonalVehicle
        {
            get { return RequiresVehicle == null; }
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
        public IVehicleType RequiresVehicle { get; set; }

        [RunParameter("Variance Scale", 1.0, "The factor applied to the error term.")]
        public double VarianceScale { get; set; }

        [SubModelInformation(Required = false, Description = "An optional source to gather parking costs from.")]
        public IDataSource<IParkingCost> ParkingModel;

        private IParkingCost _parkingModel;

        public double CalculateV(ITrip trip)
        {
            // compute the non human factors
            var zoneSystem = Root.ZoneSystem;
            var zoneArray = zoneSystem.ZoneArray;
            IZone originalZone = trip.OriginalZone;
            var o = zoneArray.GetFlatIndex(originalZone.ZoneNumber);
            IZone destinationZone = trip.DestinationZone;
            var d = zoneArray.GetFlatIndex(destinationZone.ZoneNumber);
            var chain = trip.TripChain;
            var p = chain.Person;
            GetPersonVariables(p, out float timeFactor, out float constant, out float costParameter);
            float v = constant;
            // if Intrazonal
            Time tripStartTime = trip.TripStartTime;
            if (o == d)
            {
                v += IntrazonalConstant;
                v += IntrazonalTripDistanceFactor * zoneSystem.Distances.GetFlatData()[o][d] * 0.001f;
            }
            else
            {
                var timeToNextTrip = TimeToNextTrip(trip);
                var parkingCosts = _parkingModel == null ? zoneArray.GetFlatData()[d].ParkingCost * Math.Min(MaximumHoursForParking, timeToNextTrip)
                    : _parkingModel.ComputeParkingCost(trip.ActivityStartTime, trip.ActivityStartTime + Time.FromMinutes(timeToNextTrip), d);
                Network.GetAllData(o, d, tripStartTime, out float ivtt, out float cost);
                v += timeFactor * ivtt + costParameter * (cost + parkingCosts);
            }
            // Apply personal factors
            if(p.Female)
            {
                v += FemaleFlag;
            }
            var age = p.Age;
            v += AgeUtilLookup[Math.Min(Math.Max(age - 15, 0), 15)];
            if(age >= 65)
            {
                v += Over65;
            }
            else if(age >= 55)
            {
                v += Over55;
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
                case Activity.School:
                    v += SchoolFlag;
                    break;
            }
            v += GetPlanningDistrictConstant(tripStartTime, originalZone.PlanningDistrict, destinationZone.PlanningDistrict);
            return v;
        }

        [SubModelInformation(Description = "Constants for time of day")]
        public TimePeriodSpatialConstant[] TimePeriodConstants;

        public float GetPlanningDistrictConstant(Time startTime, int pdO, int pdD)
        {
            for (int i = 0; i < TimePeriodConstants.Length; i++)
            {
                if (startTime >= TimePeriodConstants[i].StartTime && startTime < TimePeriodConstants[i].EndTime)
                {
                    return TimePeriodConstants[i].GetConstant(pdO, pdD);
                }
            }
            return 0f;
        }

        private float TimeToNextTrip(ITrip trip)
        {
            var tchain = trip.TripChain.Trips;
            for(int i = 0; i < tchain.Count - 1; i++)
            {
                if(tchain[i] == trip)
                {
                    // the number is (1/60)f
                    return (tchain[i + 1].ActivityStartTime - trip.ActivityStartTime).ToMinutes() * 0.01666666666666f;
                }
            }
            return 0f;
        }

        private void GetPersonVariables(ITashaPerson person, out float time, out float constant, out float cost)
        {
            if(person.EmploymentStatus == TTSEmploymentStatus.FullTime)
            {
                switch(person.Occupation)
                {
                    case Occupation.Professional:
                        cost = ProfessionalCost;
                        constant = ProfessionalConstant;
                        time = ProfessionalTimeFactor;
                        return;
                    case Occupation.Office:
                        cost = GeneralCost;
                        constant = GeneralConstant;
                        time = GeneralTimeFactor;
                        return;
                    case Occupation.Retail:
                        cost = SalesCost;
                        constant = SalesConstant;
                        time = SalesTimeFactor;
                        return;
                    case Occupation.Manufacturing:
                        cost = ManufacturingCost;
                        constant = ManufacturingConstant;
                        time = ManufacturingTimeFactor;
                        return;
                }
            }
            switch(person.StudentStatus)
            {
                case StudentStatus.FullTime:
                case StudentStatus.PartTime:
                    cost = StudentCost;
                    constant = StudentConstant;
                    time = StudentTimeFactor;
                    return;
            }
            if(person.EmploymentStatus == TTSEmploymentStatus.PartTime)
            {
                switch(person.Occupation)
                {
                    case Occupation.Professional:
                        cost = ProfessionalCost;
                        constant = ProfessionalConstant;
                        time = ProfessionalTimeFactor;
                        return;
                    case Occupation.Office:
                        cost = GeneralCost;
                        constant = GeneralConstant;
                        time = GeneralTimeFactor;
                        return;
                    case Occupation.Retail:
                        cost = SalesCost;
                        constant = SalesConstant;
                        time = SalesTimeFactor;
                        return;
                    case Occupation.Manufacturing:
                        cost = ManufacturingCost;
                        constant = ManufacturingConstant;
                        time = ManufacturingTimeFactor;
                        return;
                }
            }
            cost = NonWorkerStudentCost;
            constant = NonWorkerStudentConstant;
            time = NonWorkerStudentTimeFactor;
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
            var chain = trip.TripChain;
            var trips = chain.Trips;
            var person = chain.Person;
            return person.Licence && person.Household.Vehicles.Length > 0;
        }

        public bool Feasible(ITripChain tripChain)
        {
            IZone vehicleLeftAt = tripChain.Person.Household.HomeZone;
            var home = vehicleLeftAt;
            var trips = tripChain.Trips;
            bool noAutoTrips = true;
            bool first = false;
            bool lastMadeWithAuto = false;
            IVehicleType ourVehicle = RequiresVehicle;
            for (int i = 0; i < trips.Count; i++)
            {
                var trip = trips[i];
                if (trip.Mode.RequiresVehicle == ourVehicle)
                {
                    // it is only not feasible if we actually take the mode and we don't have a license
                    if((trip.OriginalZone != vehicleLeftAt))
                    {
                        return false;
                    }
                    vehicleLeftAt = trip.DestinationZone;
                    lastMadeWithAuto = true;
                    noAutoTrips = false;
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

        public bool Feasible(IZone origin, IZone destination, Time time)
        {
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            AssignRequiredVehicle();
            if(RequiresVehicle == null)
            {
                error = "We were unable to find an vehicle type to use for '" + ModeName + "'!";
                return false;
            }

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

        private void AssignRequiredVehicle()
        {
            if(string.IsNullOrWhiteSpace(VehicleTypeName))
            {
                RequiresVehicle = Root.AutoType;
            }
            else
            {
                if(Root.AutoType.VehicleName == VehicleTypeName)
                {
                    RequiresVehicle = Root.AutoType;
                }
                else if(Root.VehicleTypes != null)
                {
                    foreach(var v in Root.VehicleTypes)
                    {
                        if(v.VehicleName == VehicleTypeName)
                        {
                            RequiresVehicle = v;
                            break;
                        }
                    }
                }
            }
        }

        public void IterationEnding(int iterationNumber, int maxIterations)
        {

        }

        private float[] AgeUtilLookup;

        public void IterationStarting(int iterationNumber, int maxIterations)
        {
            // We do this here instead of the RuntimeValidation so that we don't run into issues with estimation
            AgeUtilLookup = new float[16];
            for(int i = 0; i < AgeUtilLookup.Length; i++)
            {
                AgeUtilLookup[i] = (float)Math.Log(i + 1, Math.E) * LogOfAgeFactor;
            }
            for (int i = 0; i < TimePeriodConstants.Length; i++)
            {
                TimePeriodConstants[i].BuildMatrix();
            }
            ProfessionalCost = ConvertCostFactor(ProfessionalCostFactor, ProfessionalTimeFactor);
            GeneralCost = ConvertCostFactor(GeneralCostFactor, GeneralTimeFactor);
            SalesCost = ConvertCostFactor(SalesCostFactor, SalesTimeFactor);
            ManufacturingCost = ConvertCostFactor(ManufacturingCostFactor, ManufacturingTimeFactor);
            StudentCost = ConvertCostFactor(StudentCostFactor, StudentTimeFactor);
            NonWorkerStudentCost = ConvertCostFactor(NonWorkerStudentCostFactor, NonWorkerStudentTimeFactor);
            if (ParkingModel != null)
            {
                if (!ParkingModel.Loaded)
                {
                    ParkingModel.LoadData();
                }
                _parkingModel = ParkingModel.GiveData();
            }
        }

        private float ConvertCostFactor(float costFactor, float timeFactor)
        {
            var ret = costFactor * timeFactor;
            if (ret > 0)
            {
                throw new XTMFRuntimeException(this, "In '" + Name + "' we ended up with a beta to apply to cost that was greater than 0! The value was '" + ret + "'");
            }
            return ret;
        }
    }
}