/*
    Copyright 2015 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.Text;
using System.IO;
using TMG.Input;
using Datastructure;
using TMG;
using Tasha.Common;
using XTMF;
using Tasha.XTMFModeChoice;


namespace Tasha.Validation.SmartTrackPerformance
{
    public class VKTV2 : IPostHousehold
    {
        [SubModelInformation(Required = true, Description = "Where do you want to save the Purpose Results. Must be in .CSV format.")]
        public FileLocation VKT_Output;

        [SubModelInformation(Required = true, Description = "Which modes do you want to count the VKTs for?")]
        public Tasha.EMME.CreateEmmeBinaryMatrix.ModeLink[] AnalyzeModes;

        List<string> ValidModeNames = new List<string>();

        [RootModule]
        public ITashaRuntime Root;
      

        SparseTriIndex<float> VKTCounter;

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
            var houseData = household["ModeChoiceData"] as ModeChoiceHouseholdData;
            if (houseData == null)
            {
                Console.WriteLine("{0}", household.HouseholdId);
            }

            else if (iteration == Root.Iterations - 1)
            {               
                var resource = household["ResourceAllocator"] as HouseholdResourceAllocator;
                var modes = this.Root.AllModes;
                var homeZone = household.HomeZone.ZoneNumber;
                
                if (household.Vehicles.Length > 0)
                {
                    for (int i = 0; i < household.Persons.Length; i++)
                    {
                        for (int j = 0; j < household.Persons[i].TripChains.Count; j++)
                        {
                            var personalExp = household.Persons[i].ExpansionFactor;
                            for (int k = 0; k < household.Persons[i].TripChains[j].Trips.Count; k++)
                            {
                                var trip = household.Persons[i].TripChains[j].Trips[k];
                                var tripMode = trip.Mode;

                                if (ValidModeNames.Contains(tripMode.ModeName))
                                {
                                    AddData(homeZone, trip.OriginalZone.ZoneNumber, trip.DestinationZone.ZoneNumber, personalExp);
                                }                               
                            }
                        }                        
                    }
                }
            }
        }

        public void AddData(int householdZone, int origin, int destination, float expFactor)
        {                       
            lock(this)
            {
                VKTCounter[householdZone, origin, destination] += expFactor;
            }
        }

        public void IterationFinished(int iteration)
        {
            if (iteration == Root.Iterations - 1)
            {
                var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
                lock (this)
                {
                    var writeHeader = !File.Exists(VKT_Output);
                    using (StreamWriter writer = new StreamWriter(VKT_Output, true))
                    {
                        if (writeHeader)
                        {
                            writer.WriteLine("Home Zone,Origin,Destination,Trips");
                        }

                        var results = VKTCounter.GetFlatData();

                        for (var i = 0; i < results.Length; i++)
                        {
                            for (var j = 0; j < results[i].Length; j++)
                            {
                                for (var k = 0; k < results[i][j].Length; k++)
                                {
                                    if (results[i][j][k] > 0)
                                    {
                                        writer.WriteLine("{0},{1},{2},{3}", zones[i].ZoneNumber, zones[j].ZoneNumber, zones[k].ZoneNumber, results[i][j][k]);
                                    }                                        
                                }
                            }
                        }                       
                    }
                }
            }
        }

        public void IterationStarting(int iteration)
        {
            SparseArray<IZone> zoneSystem = Root.ZoneSystem.ZoneArray;
            VKTCounter = SparseTriIndex<float>.CreateSimilarArray(zoneSystem, zoneSystem, zoneSystem);
            
            for (int i = 0; i < AnalyzeModes.Length; i++)
            {
                ValidModeNames.Add(AnalyzeModes[i].ModeName);
            }
            
        }

        public void Load(int maxIterations)
        {

        }

        public bool RuntimeValidation(ref string error)
        {            
            return true;
        }
    }
}
