using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Tasha.Common;
using TMG.Input;
using XTMF;

namespace Tasha.Validation.ModeChoice
{
    public class StartTimeDistributions : IPostHousehold, IPostHouseholdIteration
    {
        public string Name
        {
            get;
            set;
        }

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

        SpinLock WriteLock = new SpinLock(false);
        Dictionary<Activity, float[][]> TimeBin = new Dictionary<Activity, float[][]>();

        [SubModelInformation(Required = true, Description = "The place to save the report.")]
        public FileLocation OutputFile;

        [RunParameter("Minimum Age", 11, "The minimum age for the trips to be recorded in the total.")]
        public int MinimumAge;

        [RunParameter("Trip Start Time", true, "Set to true to get the trip's start time.  Setting this to false will give the activity start times.")]
        public bool TripStartTime;

        [RunParameter("Use Origin Purpose", false, "Use the origin purpose instead of the destination purpose.  This will be similar to getting the end time for trips.")]
        public bool UseOriginPurpose;

        private const int NumberOfTimeBins = 48;
        private ITashaMode[] Modes;


        public void HouseholdComplete(ITashaHousehold household, bool success)
        {
            
        }

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {
            Execute(household, hhldIteration);
        }

        public void HouseholdStart(ITashaHousehold household, int householdIterations)
        {
            
        }

        public void IterationStarting(int iteration, int totalIterations)
        {
            IterationStarting(iteration);
        }

        public void IterationFinished(int iteration, int totalIterations)
        {
            IterationFinished(iteration);
        }

        public void Execute(ITashaHousehold household, int iteration)
        {
            var persons = household.Persons;
            for (int i = 0; i < persons.Length; i++)
            {
                if (persons[i].Age >= MinimumAge)
                {
                    var expansionFactor = persons[i].ExpansionFactor;
                    var tripChains = persons[i].TripChains;
                    for (int j = 0; j < tripChains.Count; j++)
                    {
                        var tripChain = tripChains[j].Trips;
                        for (int k = 0; k < tripChain.Count; k++)
                        {
                            int index = (((int)(TripStartTime ? tripChain[k].TripStartTime : tripChain[k].ActivityStartTime).ToMinutes()) / 30);
                            var tripStartIndex = index < 0 ? (index % NumberOfTimeBins + NumberOfTimeBins) % NumberOfTimeBins : index % NumberOfTimeBins;
                            var array = GetPurposeCount(tripChain, k);
                            var tripModeIndex = GetTripModeIndex(tripChain[k].Mode);
                            if (tripModeIndex >= 0)
                            {
                                var row = array[tripStartIndex];
                                lock (array[tripStartIndex])
                                {
                                    row[tripModeIndex] += expansionFactor;
                                }
                            }
                        }
                    }
                }
            }
        }

        private int GetTripModeIndex(ITashaMode mode)
        {
            for (int i = 0; i < Modes.Length; i++)
            {
                if (mode == Modes[i])
                {
                    return i;
                }
            }
            return -1;
        }

        private float[][] GetPurposeCount(Activity purpose)
        {
            float[][] ret;
            if (!TimeBin.TryGetValue(purpose, out ret))
            {
                lock (TimeBin)
                {
                    Thread.MemoryBarrier();
                    if (!TimeBin.TryGetValue(purpose, out ret))
                    {
                        ret = new float[NumberOfTimeBins][];
                        for (int i = 0; i < ret.Length; i++)
                        {
                            ret[i] = new float[Modes.Length];
                        }
                        TimeBin.Add(purpose, ret);
                    }
                }
            }
            return ret;
        }

        private float[][] GetPurposeCount(List<ITrip> chain, int index)
        {
            Activity purpose;
            if (UseOriginPurpose)
            {
                purpose = index > 0 ? chain[index - 1].Purpose : Activity.Home;
            }
            else
            {
                purpose = chain[index].Purpose;
            }
            return GetPurposeCount(purpose);
        }

        public void IterationFinished(int iteration)
        {
            using (var writer = new StreamWriter(OutputFile, true))
            {
                writer.Write("Iteration: ");
                writer.WriteLine(iteration + 1);
                writer.Write("Time");
                foreach (var key in TimeBin.Keys)
                {
                    var name = Enum.GetName(typeof(Activity), key);
                    for (int i = 0; i < Modes.Length; i++)
                    {
                        writer.Write(',');
                        writer.Write(name);
                        writer.Write(':');
                        writer.Write(Modes[i].ModeName);
                    }
                }
                writer.WriteLine();
                Time thirtyMinutes = new Time() { Minutes = 30 };
                Time currentTime = new Time();
                for (int i = 0; i < NumberOfTimeBins; i++)
                {
                    writer.Write(currentTime);
                    foreach (var key in TimeBin.Keys)
                    {
                        var purposeData = GetPurposeCount(key)[i];
                        for (int j = 0; j < purposeData.Length; j++)
                        {
                            writer.Write(',');
                            writer.Write(purposeData[j]);
                        }
                    }
                    writer.WriteLine();
                    currentTime += thirtyMinutes;
                }
            }
        }

        public void Load(int maxIterations)
        {

        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        [RootModule]
        public ITashaRuntime Root;

        public void IterationStarting(int iteration)
        {
            TimeBin.Clear();
            if (Modes == null)
            {
                Modes = Root.AllModes.ToArray();
            }
        }
    }
}
