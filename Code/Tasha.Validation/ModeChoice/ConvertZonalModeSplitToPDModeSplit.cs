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
using XTMF;
using Datastructure;
using TMG;
using TMG.Input;
using Tasha.Common;
using System.IO;

namespace Tasha.Validation.ModeChoice
{
    [ModuleInformation(Description = "This module reads in the output of ZonalModeSplits and aggregates it to the planning district level.")]
    public class ConvertZonalModeSplitToPDModeSplit : IPostIteration
    {
        public string Name { get; set; }

        public float Progress
        {
            get
            {
                return 0f;
            }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get
            {
                return null;
            }
        }

        [SubModelInformation(Required = true, Description = "The location of the previously saved zonal mode split file.")]
        public FileLocation ZonalModeSplitFile;

        [SubModelInformation(Required = true, Description = "The location to save the pd mode split data.")]
        public FileLocation PDModeSplitFile;

        public void Execute(int iterationNumber, int totalIterations)
        {
            var zoneSystem = Root.ZoneSystem.ZoneArray;
            var regions = TMG.Functions.ZoneSystemHelper.CreatePdArray<int>(zoneSystem);
            float[][][] data = BuildData(Root.AllModes.Select(m => m.ModeName).ToArray(), zoneSystem, regions);
            SaveData(data, regions);
        }

        private void SaveData(float[][][] data, SparseArray<int> regions)
        {
            var modes = Root.AllModes.ToArray();
            var regionNumbers = regions.ValidIndexArray();
            using StreamWriter writer = new(PDModeSplitFile);
            writer.WriteLine("Mode,Origin,Destination,ExpandedTrips");
            for (int m = 0; m < data.Length; m++)
            {
                string modeName = modes[m].ModeName + ",";
                var oRow = data[m];
                for (int o = 0; o < oRow.Length; o++)
                {
                    var dRow = oRow[o];
                    for (int d = 0; d < dRow.Length; d++)
                    {
                        if (dRow[d] > 0)
                        {
                            // this includes the comma already
                            writer.Write(modeName);
                            writer.Write(regionNumbers[o]);
                            writer.Write(',');
                            writer.Write(regionNumbers[d]);
                            writer.Write(',');
                            writer.WriteLine(dRow[d]);
                        }
                    }
                }
            }
        }

        [RootModule]
        public ITashaRuntime Root;


        private float[][][] BuildData(string[] modeNames, SparseArray<IZone> zoneSystem, SparseArray<int> regions)
        {
            var modes = Root.AllModes.ToArray();
            var data = new float[modes.Length][][];
            var numberOfRegions = regions.GetFlatData().Length;
            for(int i = 0; i < data.Length; i++)
            {
                var row = data[i] = new float[numberOfRegions][];
                for(int j = 0; j < row.Length; j++)
                {
                    row[j] = new float[numberOfRegions];
                }
            }
            using (CsvReader reader = new(ZonalModeSplitFile))
            {
                // burn header
                reader.LoadLine();
                while (reader.LoadLine(out int columns))
                {
                    // ignore lines without the right number of columns
                    if (columns == 4)
                    {
                        reader.Get(out string modeName, 0);
                        reader.Get(out int originZone, 1);
                        reader.Get(out int destinationZone, 2);
                        reader.Get(out float expandedPersons, 3);
                        data[ModeIndex(modeName, modeNames)][regions.GetFlatIndex(zoneSystem[originZone].PlanningDistrict)][regions.GetFlatIndex(zoneSystem[destinationZone].PlanningDistrict)]
                            += expandedPersons;
                    }
                }
            }
            return data;
        }

        private int ModeIndex(string modeName, string[] modeNames)
        {
            for(int i = 0; i < modeNames.Length; i++)
            {
                if(modeNames[i] == modeName)
                {
                    return i;
                }
            }
            throw new XTMFRuntimeException(this, "In '" + Name + "' we were unable to find a mode called '" + modeName + "'");
        }

        public void Load(IConfiguration config, int totalIterations)
        {

        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
