using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.IO;
using TMG.Input;
using TMG;
using Tasha.Common;
using XTMF;
using Tasha.XTMFModeChoice;

namespace Tasha.Validation.SmartTrackPerformance
{
    public class VKT_Measure : IPostHousehold
    {
        [SubModelInformation(Required = true, Description = "Where do you want to save the Purpose Results. Must be in .CSV format.")]
        public FileLocation VKT_Output;

        [RunParameter("Passenger Mode", "Passenger", "The name of the passenger mode, leave blank to not processes them specially.")]
        public string PassengerModeName;

        [RunParameter("RideShare Mode", "RideShare", "The name of the passenger mode, leave blank to not processes them specially.")]
        public string RideshareModeName;

        [RootModule]
        public ITashaRuntime Root;

        private int PassengerIndex;
        private int RideShareIndex;

        private ConcurrentDictionary<int, float[]> VKT = new ConcurrentDictionary<int, float[]>();

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;         
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>(120, 25, 100); }
        }

        public void Execute(ITashaHousehold household, int iteration)
        {
            var houseData1 = household["ModeChoiceData"] as ModeChoiceHouseholdData;
            if (houseData1 == null)
            {
                Console.WriteLine("{0}", household.HouseholdId);                
            }

            else if (iteration == Root.Iterations - 1)
            {
                var houseData = household["ModeChoiceData"] as ModeChoiceHouseholdData;
                var resource = household["ResourceAllocator"] as HouseholdResourceAllocator;
                var modes = this.Root.AllModes;

                float totalVKT = 0;
                if (household.Vehicles.Length > 0)
                {
                    for (int i = 0; i < household.Persons.Length; i++)
                    {
                        var personData = houseData.PersonData[i];
                        for (int j = 0; j < household.Persons[i].TripChains.Count; j++)
                        {
                            var tripChainData = personData.TripChainData[j];

                            if (tripChainData.TripChain.JointTrip && !tripChainData.TripChain.JointTripRep)
                            {
                                continue;
                            }

                            for (int k = 0; k < household.Persons[i].TripChains[j].Trips.Count; k++)
                            {
                                var currentTrip = tripChainData.TripChain.Trips[k];

                                if (currentTrip.Mode.RequiresVehicle != null)
                                {
                                    if (currentTrip.Mode == modes[PassengerIndex])
                                    {
                                        float firstLeg;
                                        float secondLeg;
                                        var originalTrip = currentTrip["Driver"] as ITrip;
                                        var passengerDistance = this.Root.ZoneSystem.Distances[currentTrip.OriginalZone.ZoneNumber, currentTrip.DestinationZone.ZoneNumber];
                                        if (originalTrip.OriginalZone == currentTrip.OriginalZone)
                                        {
                                            firstLeg = 0;
                                        }
                                        else
                                        {
                                            firstLeg = this.Root.ZoneSystem.Distances[originalTrip.OriginalZone.ZoneNumber, currentTrip.OriginalZone.ZoneNumber];
                                        }

                                        if (originalTrip.DestinationZone == currentTrip.DestinationZone)
                                        {
                                            secondLeg = 0;
                                        }
                                        else
                                        {
                                            secondLeg = this.Root.ZoneSystem.Distances[currentTrip.DestinationZone.ZoneNumber, originalTrip.DestinationZone.ZoneNumber];
                                        }
                                        // Subtract out the driver's VKT only if the purpose of this trip is not to facilitate passenger
                                        if (originalTrip.TripChain.Trips.Count > 1)
                                        {
                                            totalVKT -= this.Root.ZoneSystem.Distances[originalTrip.OriginalZone.ZoneNumber, originalTrip.DestinationZone.ZoneNumber];
                                        }
                                        totalVKT += (passengerDistance + firstLeg + secondLeg);
                                    }
                                    else if (currentTrip.Mode == modes[this.RideShareIndex])
                                    {
                                        totalVKT += this.Root.ZoneSystem.Distances[currentTrip.OriginalZone.ZoneNumber, currentTrip.DestinationZone.ZoneNumber] / 2;
                                    }
                                    else
                                    {
                                        totalVKT += this.Root.ZoneSystem.Distances[currentTrip.OriginalZone.ZoneNumber, currentTrip.DestinationZone.ZoneNumber];
                                    }
                                }
                            }
                        }
                    }

                    AddDataToResults(household.HomeZone.ZoneNumber, (float)household.Vehicles.Length, totalVKT);

                }                
            }            
        }

        public void AddDataToResults(int zoneID, float numberOfVehicles, float totalVKT)
        {
            VKT.TryAdd(zoneID, new float[2]);
            VKT[zoneID][0] += numberOfVehicles;
            VKT[zoneID][1] += totalVKT;          
        }


        public void IterationFinished(int iteration)
        {
            if (iteration == Root.Iterations - 1)
            {
                
                lock (this)
                {
                    var writeHeader = !File.Exists(VKT_Output);
                    using (StreamWriter writer = new StreamWriter(VKT_Output, true))
                    {
                        if (writeHeader)
                        {
                            writer.WriteLine("Home Zone, Total Number of Vehicles, Total VKT, Average VKT");
                        }

                        foreach (var pair in VKT)
                        {
                            float avgVKT = pair.Value[1] / pair.Value[0];
                            writer.WriteLine("{0}, {1}, {2}, {3}", pair.Key, pair.Value[0], pair.Value[1], avgVKT);
                        }
                    }
                }
            }
        }

        public void IterationStarting(int iteration)
        {            
        }

        public void Load(int maxIterations)
        {            
        }

        public bool RuntimeValidation(ref string error)
        {
            this.PassengerIndex = -1;
            this.RideShareIndex = -1;
            if (!String.IsNullOrWhiteSpace(this.PassengerModeName))
            {
                for (int i = 0; i < this.Root.AllModes.Count; i++)
                {
                    if (this.Root.AllModes[i].ModeName == this.PassengerModeName)
                    {
                        this.PassengerIndex = i;
                    }
                    if (this.Root.AllModes[i].ModeName == this.RideshareModeName)
                    {
                        this.RideShareIndex = i;
                    }
                }
                if (this.PassengerIndex <= 0)
                {
                    error = "In '" + this.Name + "' we were unable to find any passenger mode with the name '" + this.PassengerModeName + "'.";
                    return false;
                }
                if (this.RideShareIndex <= 0)
                {
                    error = "In '" + this.Name + "' we were unable to find any RideShare mode with the name '" + this.RideShareIndex + "'.";
                    return false;
                }
            }
            return true;
        }
    }
}
