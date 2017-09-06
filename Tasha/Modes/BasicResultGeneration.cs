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
using System.IO;
using System.Text;
using System.Threading;
using Tasha.Common;
using XTMF;

namespace Tasha.Modes
{
    public sealed class BasicResultGeneration : IPostHousehold, IDisposable
    {
        [RunParameter("Process Observed data", false, "Process Observed data.")]
        public bool IsObserved;

        [RunParameter("Observed Mode Attachment", "ObservedMode", "The name of the attachment string from the loader")]
        public string ObservedMode;

        [RunParameter("File Name", "BasicResults.csv", "The file that we will store the results into.")]
        public string ResultsFileName;

        [RootModule]
        public ITashaRuntime Root;

        private StreamWriter ModesChosen;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        [ThreadStatic]
        private static StringBuilder Builder;

        private SpinLock WriteLock = new SpinLock(false);

        public void Execute(ITashaHousehold household, int iteration)
        {
            StringBuilder builder = Builder;
            if(builder == null)
            {
                Builder = builder = new StringBuilder();
            }
            foreach(var person in household.Persons)
            {
                float expansionFactor = person.ExpansionFactor;
                int age = person.Age;
                foreach(var tripChain in person.TripChains)
                {
                    for(int j = 0; j < tripChain.Trips.Count; j++)
                    {
                        var trip = tripChain.Trips[j];
                        var nextTrip = j < tripChain.Trips.Count - 1 ? tripChain.Trips[j + 1] : null;
                        if(trip.ActivityStartTime > Time.EndOfDay && trip.Purpose != Activity.Home)
                        {
                            throw new XTMFRuntimeException(this, "PAST END OF DAY! " + trip.ActivityStartTime + "\r\n " + trip.Purpose + "\r\n " + "household ID is " + household.HouseholdId + " person ID is " + person.Id);
                        }
                        var householdIterations = (trip.ModesChosen == null || trip.ModesChosen.Length == 0) ? 1 : trip.ModesChosen.Length;
                        for(int i = 0; i < householdIterations; i++)
                        {
                            ITashaMode mode = (ITashaMode)trip[ObservedMode];
                            builder.Append(trip.TripChain.Person.Household.HouseholdId);
                            builder.Append(',');
                            builder.Append(trip.TripChain.Person.Id);
                            builder.Append(',');
                            builder.Append(trip.TripNumber);
                            builder.Append(',');
                            builder.Append(trip.TripStartTime);
                            builder.Append(',');
                            builder.Append(trip.ActivityStartTime);
                            builder.Append(',');
                            builder.Append((nextTrip != null ? nextTrip.TripStartTime - trip.ActivityStartTime : Time.Zero));
                            builder.Append(',');
                            builder.Append(trip.Purpose);
                            builder.Append(',');
                            builder.Append(age);
                            builder.Append(',');
                            builder.Append(trip.OriginalZone.ZoneNumber);
                            builder.Append(',');
                            builder.Append(trip.DestinationZone.ZoneNumber);
                            builder.Append(',');
                            builder.Append(IsObserved ? mode.ModeName : (trip.Mode?.ModeName));
                            builder.Append(',');
                            builder.Append((trip.ModesChosen == null || trip.ModesChosen.Length <= i || trip.ModesChosen[i] == null) ? "NONE" : trip.ModesChosen[i].ModeName);
                            builder.Append(',');
                            builder.Append(expansionFactor);
                            builder.Append(',');
                            builder.Append((trip.TripStartTime.Hours * 100 + (trip.TripStartTime.Minutes / 30) * 30));
                            builder.Append(',');
                            builder.Append(StraightLineDistance(trip.OriginalZone, trip.DestinationZone));
                            builder.Append(',');
                            builder.Append(ManhattanDistance(trip.OriginalZone, trip.DestinationZone));
                            builder.AppendLine();
                        }
                    }
                }
            }
            if(builder.Length > 0)
            {
                var builderData = builder.ToString();
                bool taken = false;
                lock (ModesChosen)
                {
                    WriteLock.Enter(ref taken);
                    ModesChosen.Write(builderData);
                    if (taken) WriteLock.Exit(false);
                }
                builder.Clear();
            }
        }

        public void IterationFinished(int iteration)
        {
            lock(this)
            {
                ModesChosen.Close();
            }
        }

        public void Load(int maxIterations)
        {
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void IterationStarting(int iteration)
        {
            lock(this)
            {
                var alreadyExists = File.Exists(ResultsFileName);
                ModesChosen = new StreamWriter(ResultsFileName, true);
                if(!alreadyExists)
                {
                    ModesChosen.WriteLine("HouseholdID,PersonID,TripNumber,TripStartTime,ActivityStartTime,ActivityDuration,TripPurpose,Age,Origin,Destination,Mode,ModeChoice,ExpansionFactor,RoundedTripStartTime,StraightLineDistance,ManhattanDistance");
                }
            }
        }

        private float ManhattanDistance(TMG.IZone zone1, TMG.IZone zone2)
        {
            if(zone1 == zone2) return zone1.InternalDistance;
            var deltaX = zone1.X - zone2.X;
            var deltaY = zone1.Y - zone2.Y;
            return Math.Abs(deltaX) + Math.Abs(deltaY);
        }

        private float StraightLineDistance(TMG.IZone zone1, TMG.IZone zone2)
        {
            if(zone1 == zone2) return zone1.InternalDistance;
            var deltaX = zone1.X - zone2.X;
            var deltaY = zone1.Y - zone2.Y;
            return (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        public void Dispose()
        {
            if(ModesChosen != null)
            {
                ModesChosen.Dispose();
                ModesChosen = null;
            }
        }
    }
}