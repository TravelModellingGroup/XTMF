using Datastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.V4Modes.PerceivedTravelTimes
{
    public class PassengerAccessTransit : ITashaMode, IIterationSensitive, ITourDependentMode
    {

        [RootModule]
        public ITashaRuntime Root;

        private SparseArray<IZone> _zones;

        [RunParameter("Auto Network", "Auto", "The name of the auto network.")]
        public string AutoNetworkName;

        [RunParameter("Transit Network", "Transit", "The name of the transit network.")]
        public string TransitNetworkName;

        private INetworkCompleteData AutoNetwork;
        private ITripComponentCompleteData TransitNetwork;

        [RunParameter("MarketFlag", 0f, "Added to the utility if the trip's purpose is market.")]
        public float MarketFlag;

        [RunParameter("OtherFlag", 0f, "Added to the utility if the trip's purpose is 'other'.")]
        public float OtherFlag;

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

        [RunParameter("ProfessionalTimeFactor-Auto", 0f, "The TimeFactor applied to the person type.")]
        public float ProfessionalTimeFactorAuto;
        [RunParameter("GeneralTimeFactor-Auto", 0f, "The TimeFactor applied to the person type.")]
        public float GeneralTimeFactorAuto;
        [RunParameter("SalesTimeFactor-Auto", 0f, "The TimeFactor applied to the person type.")]
        public float SalesTimeFactorAuto;
        [RunParameter("ManufacturingTimeFactor-Auto", 0f, "The TimeFactor applied to the person type.")]
        public float ManufacturingTimeFactorAuto;
        [RunParameter("StudentTimeFactor-Auto", 0f, "The TimeFactor applied to the person type.")]
        public float StudentTimeFactorAuto;
        [RunParameter("NonWorkerStudentTimeFactor-Auto", 0f, "The TimeFactor applied to the person type.")]
        public float NonWorkerStudentTimeFactorAuto;

        [RunParameter("ProfessionalTimeFactor-Transit", 0f, "The TimeFactor applied to the person type.")]
        public float ProfessionalTimeFactorTransit;
        [RunParameter("GeneralTimeFactor-Transit", 0f, "The TimeFactor applied to the person type.")]
        public float GeneralTimeFactorTransit;
        [RunParameter("SalesTimeFactor-Transit", 0f, "The TimeFactor applied to the person type.")]
        public float SalesTimeFactorTransit;
        [RunParameter("ManufacturingTimeFactor-Transit", 0f, "The TimeFactor applied to the person type.")]
        public float ManufacturingTimeFactorTransit;
        [RunParameter("StudentTimeFactor-Transit", 0f, "The TimeFactor applied to the person type.")]
        public float StudentTimeFactorTransit;
        [RunParameter("NonWorkerStudentTimeFactor-Transit", 0f, "The TimeFactor applied to the person type.")]
        public float NonWorkerStudentTimeFactorTransit;

        [DoNotAutomate]
        public IVehicleType RequiresVehicle => null;

        [RunParameter("Variance Scale", 1.0, "The factor applied to the error term.")]
        public double VarianceScale { get; set; }

        public string NetworkType => null;

        public bool NonPersonalVehicle => true;

        public float CurrentlyFeasible { get; set; }

        [Parameter("Mode Name", "PAT", "The name of the mode.")]
        public string ModeName { get; set; }

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

        [RunParameter("AutoAccess", true, "Should this mode use access or egress with auto?")]
        public bool AutoAccess;

        [RunParameter("AccessStationZoneTag", "AccessStation", "The attachment tag to use for assigning the zone that is going to be used" +
            " between origin and destination.  This must be unique between access and egress passenger choice.")]
        public string StationChoiceTag;

        [RunParameter("StationChoiceProbabilityTag", "AccessStationProbabilities", "The attachment tag to use for holding the probability of assigning" +
            "a particular zone for access / egress.  This must be unique between access and egress passenger choice.")]
        public string StationChoiceProbabilityTag;

        public double CalculateV(ITrip trip)
        {
            var choices = StationChoiceModel.ProduceResult(trip);
            // make sure at least one station has a probability of being used
            if (!HasAChoice(choices))
            {
                return float.NegativeInfinity;
            }
            GetPersonVariables(trip.TripChain.Person, out float constant);
            GetPersonVariables(trip.TripChain.Person, out float bAutoTime, out float bTransitTime, out float costFactor);
            var v = constant;
            var o = _zones.GetFlatIndex(trip.OriginalZone.ZoneNumber);
            var d = _zones.GetFlatIndex(trip.DestinationZone.ZoneNumber);
            switch (trip.Purpose)
            {
                case Activity.Market:
                case Activity.JointMarket:
                    v += MarketFlag;
                    break;
                case Activity.IndividualOther:
                case Activity.JointOther:
                    v += OtherFlag;
                    break;
            }
            if (!ComputeExpectedTravelTimes(choices, o, d, trip.ActivityStartTime, out float autoTime, out float tppt, out float cost))
            {
                return float.NegativeInfinity;
            }
            trip.Attach(StationChoiceProbabilityTag, choices);
            v += bAutoTime * (autoTime + costFactor * cost) + bTransitTime * tppt;
            return v;
        }

        private bool HasAChoice(Pair<IZone[], float[]> choices)
        {
            if (choices != null)
            {
                var p = choices.Second;
                for (int i = 0; i < p.Length; i++)
                {
                    if (p[i] > 0f)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool ComputeExpectedTravelTimes(Pair<IZone[], float[]> choices, int o, int d, Time activityStartTime,
            out float autoTime, out float tppt, out float cost)
        {
            var stnZones = choices.First;
            var stnProb = choices.Second;
            float a = 0f, t = 0f, c = 0f;
            var auto = AutoNetwork.GetTimePeriodData(activityStartTime);
            var transit = TransitNetwork.GetTimePeriodData(activityStartTime);
            var zones = _zones.GetFlatData();
            if (auto == null || transit == null)
            {
                autoTime = 0;
                tppt = 0;
                cost = 0;
                return false;
            }
            if (AutoAccess)
            {
                for (int stn = 0; stn < stnZones.Length; stn++)
                {
                    var s = _zones.GetFlatIndex(stnZones[stn].ZoneNumber);
                    var aIndex = (o * zones.Length + s) * 2;
                    var tIndex = (s * zones.Length + d) * 5;
                    a += auto[aIndex] * stnProb[stn];
                    t += transit[tIndex + 3] * stnProb[stn];
                    c += (auto[aIndex + 1] + transit[tIndex + 4]) * stnProb[stn];
                }
            }
            else
            {
                for (int stn = 0; stn < stnZones.Length; stn++)
                {
                    var s = _zones.GetFlatIndex(stnZones[stn].ZoneNumber);
                    var tIndex = (o * zones.Length + s) * 5;
                    var aIndex = (s * zones.Length + d) * 2;
                    a += auto[aIndex] * stnProb[stn];
                    t += transit[tIndex + 3] * stnProb[stn];
                    c += (auto[aIndex + 1] + transit[tIndex + 4]) * stnProb[stn];
                }
            }
            autoTime = a;
            tppt = t;
            cost = c;
            return true;
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            return float.NegativeInfinity;
        }

        public float Cost(IZone origin, IZone destination, Time time)
        {
            return 0f;
        }

        public bool Feasible(ITrip trip)
        {
            // only allow PAT/PET between planning districts
            var originPD = _zones[trip.OriginalZone.ZoneNumber].PlanningDistrict;
            var destinationPD = _zones[trip.DestinationZone.ZoneNumber].PlanningDistrict;
            return originPD != destinationPD;
        }

        public bool Feasible(ITripChain tripChain)
        {
            return true;
        }

        public bool Feasible(IZone origin, IZone destination, Time time)
        {
            var originPD = _zones[origin.ZoneNumber].PlanningDistrict;
            var destinationPD = _zones[destination.ZoneNumber].PlanningDistrict;
            return originPD != destinationPD;
        }

        public bool RuntimeValidation(ref string error)
        {
            foreach (var network in Root.NetworkData)
            {
                if (network.NetworkType == AutoNetworkName)
                {
                    AutoNetwork = network as INetworkCompleteData;
                }
                else if (network.NetworkType == TransitNetworkName)
                {
                    TransitNetwork = network as ITripComponentCompleteData;
                }
            }
            if (AutoNetwork == null)
            {
                error = "In '" + Name + "' we were unable to find an auto network called '" + AutoNetworkName + "'";
                return false;
            }
            if (TransitNetwork == null)
            {
                error = "In '" + Name + "' we were unable to find a transit network called '" + TransitNetworkName + "'";
                return false;
            }
            return true;
        }

        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return Time.Zero;
        }

        private void GetPersonVariables(ITashaPerson person, out float constant)
        {
            var empStat = person.EmploymentStatus;
            if (empStat == TTSEmploymentStatus.FullTime)
            {
                switch (person.Occupation)
                {
                    case Occupation.Professional:
                        constant = ProfessionalConstant;
                        return;
                    case Occupation.Office:
                        constant = GeneralConstant;
                        return;
                    case Occupation.Retail:
                        constant = SalesConstant;
                        return;
                    case Occupation.Manufacturing:
                        constant = ManufacturingConstant;
                        return;
                }
            }
            switch (person.StudentStatus)
            {
                case StudentStatus.FullTime:
                case StudentStatus.PartTime:
                    constant = StudentConstant;
                    return;
            }
            if (empStat == TTSEmploymentStatus.PartTime)
            {
                switch (person.Occupation)
                {
                    case Occupation.Professional:
                        constant = ProfessionalConstant;
                        return;
                    case Occupation.Office:
                        constant = GeneralConstant;
                        return;
                    case Occupation.Retail:
                        constant = SalesConstant;
                        return;
                    case Occupation.Manufacturing:
                        constant = ManufacturingConstant;
                        return;
                }
            }
            constant = NonWorkerStudentConstant;
        }

        private void GetPersonVariables(ITashaPerson person,
            out float autoTimeFactor, out float transitTimeFactor, out float costFactor)
        {
            var empStat = person.EmploymentStatus;
            if (empStat == TTSEmploymentStatus.FullTime)
            {
                switch (person.Occupation)
                {
                    case Occupation.Professional:
                        autoTimeFactor = ProfessionalTimeFactorAuto;
                        transitTimeFactor = ProfessionalTimeFactorTransit;
                        costFactor = ProfessionalCostFactor;
                        return;
                    case Occupation.Office:
                        autoTimeFactor = GeneralTimeFactorAuto;
                        transitTimeFactor = GeneralTimeFactorTransit;
                        costFactor = GeneralCostFactor;
                        return;
                    case Occupation.Retail:
                        autoTimeFactor = SalesTimeFactorAuto;
                        transitTimeFactor = SalesTimeFactorTransit;
                        costFactor = SalesCostFactor;
                        return;
                    case Occupation.Manufacturing:
                        autoTimeFactor = ManufacturingTimeFactorAuto;
                        transitTimeFactor = ManufacturingTimeFactorTransit;
                        costFactor = ManufacturingCostFactor;
                        return;
                }
            }
            switch (person.StudentStatus)
            {
                case StudentStatus.FullTime:
                case StudentStatus.PartTime:
                    autoTimeFactor = StudentTimeFactorAuto;
                    transitTimeFactor = StudentTimeFactorTransit;
                    costFactor = StudentCostFactor;
                    return;
            }
            if (empStat == TTSEmploymentStatus.PartTime)
            {
                switch (person.Occupation)
                {
                    case Occupation.Professional:
                        autoTimeFactor = ProfessionalTimeFactorAuto;
                        transitTimeFactor = ProfessionalTimeFactorTransit;
                        costFactor = ProfessionalCostFactor;
                        return;
                    case Occupation.Office:
                        autoTimeFactor = GeneralTimeFactorAuto;
                        transitTimeFactor = GeneralTimeFactorTransit;
                        costFactor = GeneralCostFactor;
                        return;
                    case Occupation.Retail:
                        autoTimeFactor = SalesTimeFactorAuto;
                        transitTimeFactor = SalesTimeFactorTransit;
                        costFactor = SalesCostFactor;
                        return;
                    case Occupation.Manufacturing:
                        autoTimeFactor = ManufacturingTimeFactorAuto;
                        transitTimeFactor = ManufacturingTimeFactorTransit;
                        costFactor = ManufacturingCostFactor;
                        return;
                }
            }
            autoTimeFactor = NonWorkerStudentTimeFactorAuto;
            transitTimeFactor = NonWorkerStudentTimeFactorTransit;
            costFactor = NonWorkerStudentCostFactor;
        }

        public void IterationEnding(int iterationNumber, int maxIterations)
        {
            if (UnloadAccessStationModelEachIteration)
            {
                StationChoiceModel.Unload();
                _stationChoiceLoaded = false;
            }
        }


        [SubModelInformation(Required = true, Description = "Output must be probability per zone.")]
        public ICalculation<ITrip, Pair<IZone[], float[]>> StationChoiceModel;

        public void IterationStarting(int iterationNumber, int maxIterations)
        {
            _zones = Root.ZoneSystem.ZoneArray;
            if (!_stationChoiceLoaded)
            {
                StationChoiceModel.Load();
                _stationChoiceLoaded = true;
            }
        }

        [RunParameter("Unload Access Station Per Iteration", true, "Should we unload the access station choice model or keep it between iterations?")]
        public bool UnloadAccessStationModelEachIteration;

        private bool _stationChoiceLoaded = false;

        [RunParameter("Random Seed", 12345, "A fixed seed to control the randomness of the station selection process.")]
        public int RandomSeed;

        public bool CalculateTourDependentUtility(ITripChain chain, int tripIndex, out float dependentUtility, out Action<ITripChain> onSelection)
        {
            // Select the access station to use, the change in utility is always 0
            dependentUtility = 0f;
            var probabilities = chain.Trips[tripIndex][StationChoiceProbabilityTag] as Pair<IZone[],float[]>;
            int householdIteration = 0;
            onSelection = (tripChain) =>
            {
                var person = tripChain.Person;
                var household = person.Household;
                householdIteration++;
                tripChain.Attach(StationChoiceTag, SelectAccessStation(
                        new Random(household.HouseholdId * person.Id * person.TripChains.IndexOf(tripChain) * RandomSeed * householdIteration),
                        probabilities));
            };
            return true;
        }

        private IZone SelectAccessStation(Random random, Pair<IZone[], float[]> accessData)
        {
            var rand = (float)random.NextDouble();
            var utils = accessData.Second;
            var runningTotal = 0.0f;
            for (int i = 0; i < utils.Length; i++)
            {
                runningTotal += utils[i];
                if (runningTotal >= rand)
                {
                    return accessData.First[i];
                }
            }
            // if we didn't find the right utility, just take the first one we can find
            for (int i = 0; i < utils.Length; i++)
            {
                if (utils[i] > 0)
                {
                    return accessData.First[i];
                }
            }
            return null;
        }
    }
}
