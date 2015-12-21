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
using System.Linq;
using System.Text;
using Datastructure;
using Tasha.Common;
using TMG;
using XTMF;
using TMG.Input;
using TMG.Functions;
using TMG.DataUtility;
using System.Threading.Tasks;

namespace Tasha.External
{
    [ModuleInformation(Description =
        @"This module is used for applying external trips to our network assignment in the GTAModel V4.0 model.")]
    public class RateBasedExternalGenerator : IModeAggregationTally
    {
        [RootModule]
        public ITashaRuntime Root;

        public IResource BaseYearTrips;
        //public IDataSource<List<ITripChain>> BaseYearTrips;

        public IResource BaseYearPopulation;

        [RunParameter("Start Time", "6:00AM", "The earliest time of the trips to include.")]
        public Time StartTime;

        [RunParameter("End Time", "9:00AM", "The latest (non inclusive) time of the trips to include.")]
        public Time EndTime;

        [RunParameter("Mode Name", "Auto", "The name of the mode to filter for,")]
        public string ModeName;

        [RunParameter("Implement External Transit", false, "Should we consider external transit.")]
        public bool ImplementExternalTransit;

        [RunParameter("Record Access", false, "Should we record the access to station or the primary transit trip?")]
        public bool RecordAccess;

        [RunParameter("External Transit Modes", "DAT,WAT", typeof(StringList), "Comma separated modes to load in for transit.")]
        public StringList ExternalTransitMode;

        [RunParameter("External Zones", "6000-6999", typeof(RangeSet), "The external zones of the model.")]
        public RangeSet ExternalZones;

        [RunParameter("Station Zones", "9000-9999", typeof(RangeSet), "The zones that represent access stations.")]
        public RangeSet AccessStationZones;

        private ITashaMode PrimaryMode;

        private HashSet<ITashaMode> ExternalTransit;

        public string Name { get; set; }

        public float Progress { get { return 0f; } }

        public Tuple<byte, byte, byte> ProgressColour { get { return null; } }

        [SubModelInformation(Required = false, Description = "Save the results of this generation to file.")]
        public FileLocation SaveResults;

        private int[] ClosestStationIndex;

        public void IncludeTally(float[][] currentTally)
        {
            var zoneSystem = Root.ZoneSystem.ZoneArray;
            var zones = zoneSystem.GetFlatData();
            BuildData(zones);
            var tripChains = BaseYearTrips.AquireResource<List<ITripChain>>();
            var basePopulation = BaseYearPopulation.AquireResource<SparseArray<float>>().GetFlatData();
            var ratio = new float[zones.Length];
            for (int i = 0; i < ratio.Length; i++)
            {
                ratio[i] = zones[i].Population / basePopulation[i];
                if (float.IsInfinity(ratio[i]) | float.IsNaN(ratio[i]))
                {
                    ratio[i] = 1;
                }
            }
            // Use the current tally if we don't care to save the results.
            // Otherwise we should create a replica so we can save those results then
            // recombine them at the end
            var tallyToUse = currentTally;
            if (SaveResults != null)
            {
                tallyToUse = new float[currentTally.Length][];
                for (int i = 0; i < tallyToUse.Length; i++)
                {
                    tallyToUse[i] = new float[currentTally[i].Length];
                }
            }
            for (int i = 0; i < tripChains.Count; i++)
            {
                var chain = tripChains[i];
                if ((chain.StartTime >= StartTime) | (chain.EndTime < EndTime))
                {
                    var person = chain.Person;
                    var homeZone = zoneSystem.GetFlatIndex(person.Household.HomeZone.ZoneNumber);
                    var expansionFactor = person.ExpansionFactor * ratio[homeZone];
                    foreach (var trip in chain.Trips)
                    {
                        var tripStart = trip.TripStartTime;
                        if ((tripStart >= StartTime) & (tripStart < EndTime))
                        {
                            var originZone = trip.OriginalZone.ZoneNumber;
                            var destinationZone = trip.DestinationZone.ZoneNumber;
                            var originZoneIndex = zoneSystem.GetFlatIndex(originZone);
                            var destinationZoneIndex = zoneSystem.GetFlatIndex(destinationZone);
                            if (ImplementExternalTransit && ExternalTransit.Contains(trip.Mode))
                            {
                                var originExternal = ExternalZones.Contains(originZone);
                                var destinationExternal = ExternalZones.Contains(destinationZone);
                                if (originExternal && destinationExternal)
                                {
                                    // if the transit trip is external ignore it since we don't model the service
                                    continue;
                                }
                                else
                                {
                                    if (RecordAccess)
                                    {
                                        // if we are recording the auto side of the trip
                                        if (originExternal)
                                        {
                                            tallyToUse[originZoneIndex][ClosestStationIndex[destinationZoneIndex]] += expansionFactor;
                                        }
                                        else if (destinationExternal)
                                        {
                                            tallyToUse[ClosestStationIndex[originZoneIndex]][destinationZoneIndex] += expansionFactor;
                                        }
                                        else
                                        {
                                            tallyToUse[originZoneIndex][destinationZoneIndex] += expansionFactor;
                                        }
                                    }
                                    else
                                    {
                                        // if we are recording the transit side of the trip
                                        if (originExternal)
                                        {
                                            tallyToUse[ClosestStationIndex[originZoneIndex]][destinationZoneIndex] += expansionFactor;
                                        }
                                        else if (destinationExternal)
                                        {
                                            tallyToUse[originZoneIndex][ClosestStationIndex[destinationZoneIndex]] += expansionFactor;
                                        }
                                        else
                                        {
                                            tallyToUse[originZoneIndex][destinationZoneIndex] += expansionFactor;
                                        }
                                    }
                                }
                            }
                            else if (trip.Mode == PrimaryMode)
                            {
                                tallyToUse[originZoneIndex][destinationZoneIndex] += expansionFactor;
                            }
                        }
                    }
                }
            }
            if (SaveResults != null)
            {
                // save the results then combine them into the current tally
                SaveData.SaveMatrix(zoneSystem.GetFlatData(), tallyToUse, SaveResults);
                // now that the data is saved we need to recombine the data
                for (int i = 0; i < tallyToUse.Length; i++)
                {
                    VectorHelper.Add(currentTally[i], 0, currentTally[i], 0, tallyToUse[i], 0, tallyToUse[i].Length);
                }
            }
        }

        private void BuildData(IZone[] zones)
        {
            var temp = new int[zones.Length];
            Parallel.For(0, temp.Length, (int i) =>
            {
                var origin = zones[i];
                int bestIndex = 0;
                double bestDistance = double.MaxValue;
                for (int j = 0; j < zones.Length; j++)
                {
                    if (AccessStationZones.Contains(zones[j].ZoneNumber))
                    {
                        double dist;
                        if ((dist = Distance(origin, zones[j])) < bestDistance)
                        {
                            bestIndex = j;
                            bestDistance = dist;
                        }
                    }
                }
                temp[i] = bestIndex;
            });
            ClosestStationIndex = temp;
        }

        private static double Distance(IZone origin, IZone accessZone)
        {
            double originX = origin.X, originY = origin.Y;
            double accessX = accessZone.X, accessY = accessZone.Y;
            return Math.Sqrt((originX - accessX) * (originX - accessX)
                            + (originY - accessY) * (originY - accessY));
        }

        public bool RuntimeValidation(ref string error)
        {
            if (!BaseYearTrips.CheckResourceType<List<ITripChain>>())
            {
                error = "In '" + this.Name + "' the resource for Base Year Trips was not of type List<ITripChain>!";
                return false;
            }
            if (!BaseYearPopulation.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + this.Name + "' the resource for Base Year Population was not of type SparseArray<float>!";
                return false;
            }
            ExternalTransit = new HashSet<ITashaMode>();
            foreach (var mode in this.Root.AllModes)
            {
                var modeName = mode.ModeName;
                if (ModeName == modeName)
                {
                    PrimaryMode = mode;
                }
                if(ExternalTransitMode.Contains(modeName))
                {
                    ExternalTransit.Add(mode);
                }
            }
            if (PrimaryMode == null)
            {
                error = "In '" + this.Name + "' we were unable to find a mode with the name '" + ModeName + "'!";
                return false;
            }
            return true;
        }
    }
}
