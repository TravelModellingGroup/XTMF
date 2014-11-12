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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Datastructure;
using XTMF;
using TMG.Input;
using TMG;
using Tasha.Common;
using Tasha.Internal;
using System.Threading.Tasks;

namespace Tasha.Estimation.AccessStation
{
    public class TourLoader : IDataLoader<ITripChain>
    {
        private ITashaMode TripMode;

        [RunParameter("Trip Mode Name", "DAT", "The mode to attach to the trips")]
        public string TripModeName;

        [RootModule]
        public ITashaRuntime Root;

        public int Count { get; set; }

        private ITripChain[] Data;

        [SubModelInformation(Required = true, Description = "The location of the tour file.")]
        public FileLocation FileLocation;

        [Parameter("Access Station Tag", "AccessStation", "The name of the tag to attach to the trip chains that contains the access station zone.")]
        public string AccessStationTag;

        public bool IsSynchronized
        {
            get
            {
                return false;
            }
        }

        public string Name { get; set; }

        public bool OutOfData
        {
            get
            {
                return false;
            }
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

        public object SyncRoot
        {
            get
            {
                return null;
            }
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(ITripChain[] array, int index)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<ITripChain> GetEnumerator()
        {
            return null;
        }

        public void LoadData()
        {
            foreach(var mode in Root.AllModes)
            {
                if(mode.ModeName == TripModeName)
                {
                    this.TripMode = mode;
                    break;
                }
            }
            if(TripMode == null)
            {
                throw new XTMFRuntimeException("In '" + Name + "' we were unable to find a mode with the name '" + TripModeName + "'.");
            }
            // if the data has already been loaded we are done
            if(this.Data != null)
            {
                return;
            }
            var zones = this.Root.ZoneSystem.ZoneArray;
            List<AccessTourData> tours = new List<AccessTourData>();
            // we only need to load the data once so lets do that now
            using (CsvReader reader = new CsvReader(FileLocation))
            {
                //burn the header
                reader.LoadLine();
                // after that read in the rest of the lines
                while(reader.LoadLine(out int columns))
                {
                    // if they have less than the number of columns we need, skip them
                    if(columns < 7) continue;

                    tours.Add(new AccessTourData(
                        // first origin
                        GetZoneFromColumn(zones, reader, 0),
                        // first destination
                        GetZoneFromColumn(zones, reader, 1),
                        // the time for the first trip
                        GetTimeFromColumn(reader, 2),
                        // second origin
                        GetZoneFromColumn(zones, reader, 3),
                        // second destination
                        GetZoneFromColumn(zones, reader, 4),
                        // the time for the second trip
                        GetTimeFromColumn(reader, 5),
                        // access station
                        GetZoneFromColumn(zones, reader, 6)));

                }
            }
            this.Data = ConvertToursToTripChains(tours);
        }

        private ITripChain[] ConvertToursToTripChains(List<AccessTourData> tours)
        {
            var ret = new ITripChain[tours.Count];
            Parallel.For(0, ret.Length, (int i) =>
            {
                var tc = new AuxiliaryTripChain();
                tc.Trips.Add(CreateTrip(tours[i].FirstOrigin, tours[i].FirstDestination, tours[i].FirstTime));
                tc.Trips.Add(CreateTrip(tours[i].SecondOrigin, tours[i].SecondDestination, tours[i].SecondTime));
                tc[this.AccessStationTag] = tours[i].AccessStation;
                ret[i] = tc;
            });
            return ret;
        }

        [RunParameter("Household Iterations", 100, "The number of household iterations.")]
        public int HouseholdIterations;

        private ITrip CreateTrip(IZone origin, IZone destination, Time time)
        {
            var trip = Scheduler.SchedulerHomeTrip.GetTrip(HouseholdIterations);
            trip.OriginalZone = origin;
            trip.DestinationZone = destination;
            trip.TripStartTime = time;
            trip.Mode = this.TripMode;
            return trip;
        }

        private Time GetTimeFromColumn(CsvReader reader, int column)
        {
            reader.Get(out string data, column);
            return new Time(data);
        }

        private static IZone GetZoneFromColumn(SparseArray<IZone> zones, CsvReader reader, int column)
        {
            reader.Get(out int zone, column);
            return zones[zone];
        }

        public void Reset()
        {

        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public ITripChain[] ToArray()
        {
            return this.Data;
        }

        public bool TryAdd(ITripChain item)
        {
            return false;
        }

        public bool TryTake(out ITripChain item)
        {
            item = null;
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return null;
        }
    }
}
