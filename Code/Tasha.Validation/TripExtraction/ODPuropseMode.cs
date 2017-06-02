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
using TMG.Input;
using Datastructure;
using TMG;
using Tasha.Common;
using XTMF;
using Tasha.XTMFModeChoice;

namespace Tasha.Validation.TripExtraction
{
    [ModuleInformation(Description = "This module outputs OD Matrices for trips by type of trip and by mode")]

    public sealed class ODPurposeMode : IPostHouseholdIteration
    {
        [SubModelInformation(Required = true, Description = "Where do you want to save the binary matrix?")]
        public FileLocation SaveTo;

        [SubModelInformation(Required = true, Description = "Which modes do you want to capture?")]
        public EMME.CreateEmmeBinaryMatrix.ModeLink[] AnalyzeModes;

        [SubModelInformation(Required = true, Description = "The activities to capture and store")]
        public ActivityLink[] ActivitiesToCapture;

        [RunParameter("Start Time", "6:00", typeof(Time), "The start time for this scenario")]
        public Time StartTime;

        [RunParameter("End Time", "9:00", typeof(Time), "The end time for this scenario")]
        public Time EndTime;

        [RunParameter("Minimum Age", 11, "The youngest a person can be and still be recorded.")]
        public int MinimumAge;

        List<string> ValidModeNames = new List<string>();
        private Activity[] Activities;

        [RootModule]
        public ITashaRuntime Root;

        SparseArray<IZone> ZoneSystem;
        float[][] RecordedData;


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

        public enum SaveAs
        {
            EMMEBinaryMatrix,
            ThirdNormalizedCSV,
            SquareCSV
        }

        [RunParameter("Format", "EMMEBinaryMatrix", typeof(SaveAs), "The type to save the matrix as.")]
        public SaveAs Format;

        public void Load(int maxIterations)
        {

        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void HouseholdComplete(ITashaHousehold household, bool success)
        {
        }

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {
            if (Iteration == Root.TotalIterations - 1)
            {
                var houseData = household["ModeChoiceData"] as ModeChoiceHouseholdData;
                if (houseData == null)
                {
                    return;
                }
                lock (this)
                {
                    for (int i = 0; i < household.Persons.Length; i++)
                    {
                        if (household.Persons[i].Age < MinimumAge)
                        {
                            continue;
                        }

                        var personalExp = household.Persons[i].ExpansionFactor;

                        for (int j = 0; j < household.Persons[i].TripChains.Count; j++)
                        {
                            for (int k = 0; k < household.Persons[i].TripChains[j].Trips.Count; k++)
                            {
                                var trip = household.Persons[i].TripChains[j].Trips[k];
                                var tripMode = trip.Mode;
                                var tripActivity = trip.Purpose;
                                var origin = ZoneSystem.GetFlatIndex(trip.OriginalZone.ZoneNumber);
                                var destination = ZoneSystem.GetFlatIndex(trip.DestinationZone.ZoneNumber);

                                if (trip.ActivityStartTime >= StartTime && trip.ActivityStartTime < EndTime)
                                {
                                    if (ValidModeNames.Contains(tripMode.ModeName))
                                    {
                                        if (Array.IndexOf(Activities, tripActivity) >= 0)
                                        {
                                            RecordedData[origin][destination] += personalExp;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void HouseholdStart(ITashaHousehold household, int householdIterations)
        {
        }

        private int Iteration;

        public void IterationStarting(int iteration, int totalIterations)
        {
            Iteration = iteration;
            lock (this)
            {
                ZoneSystem = Root.ZoneSystem.ZoneArray;

                if (RecordedData == null)
                {
                    RecordedData = ZoneSystem.CreateSquareTwinArray<float>().GetFlatData();
                }
                else
                {
                    for (int i = 0; i < RecordedData.Length; i++)
                    {
                        Array.Clear(RecordedData[i], 0, RecordedData[i].Length);
                    }
                }

                for (int i = 0; i < AnalyzeModes.Length; i++)
                {
                    ValidModeNames.Add(AnalyzeModes[i].ModeName);
                }
                Activities = ActivitiesToCapture.Select(a => a.Activity).ToArray();
            }
        }

        public void IterationFinished(int iteration, int totalIterations)
        {
            if (iteration == Root.TotalIterations - 1)
            {
                lock (this)
                {
                    switch (Format)
                    {
                        case SaveAs.EMMEBinaryMatrix:
                            new TMG.Emme.EmmeMatrix(ZoneSystem, RecordedData).Save(SaveTo, false);
                            break;
                        case SaveAs.ThirdNormalizedCSV:
                            TMG.Functions.SaveData.SaveMatrixThirdNormalized(ZoneSystem.GetFlatData(), RecordedData, SaveTo);
                            break;
                        case SaveAs.SquareCSV:
                            TMG.Functions.SaveData.SaveMatrix(ZoneSystem.GetFlatData(), RecordedData, SaveTo);
                            break;
                    }

                }
            }
        }

        public class ActivityLink : IModule
        {
            [RunParameter("Activity", "IndividualOther", typeof(Activity), "The activity to filter for.")]
            public Activity Activity;
            public string Name { get; set; }

            public float Progress { get; set; }

            public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

    }
}
