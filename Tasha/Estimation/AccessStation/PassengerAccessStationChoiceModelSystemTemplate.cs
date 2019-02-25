using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XTMF;
using TMG.Estimation;
using TMG;
using Tasha.Common;
using System.IO;
using TMG.Input;
using Datastructure;

namespace Tasha.Estimation.AccessStation
{
    [ModuleInformation(Description = "The model system template for estimating Passenger Access Transit and Passenger Egress Transit.")]
    public class PassengerAccessStationChoiceModelSystemTemplate : ITravelDemandModel
    {
        [RunParameter("Input Base Directory", "../../Input", "The base directory for input.")]
        public string InputBaseDirectory { get; set; }
        public string OutputBaseDirectory { get; set; }
        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50, 150, 50);

        public IList<INetworkData> NetworkData { get; set; }

        public IZoneSystem ZoneSystem { get; set; }

        [RootModule]
        public IEstimationClientModelSystem Root;

        [SubModelInformation(Required = true, Description = "(Origin,Destination,StartTime,AccessStation,ExpansionFactor)")]
        public FileLocation TruthData;

        public bool ExitRequest()
        {
            return true;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        struct Record
        {
            internal readonly ITrip Trip;
            internal readonly int FlatTrueZone;
            internal readonly float ExpansionFactor;

            public Record(ITrip trip, int flatTrueZone, float expansionFactor)
            {
                Trip = trip;
                FlatTrueZone = flatTrueZone;
                ExpansionFactor = expansionFactor;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is Record record))
                {
                    return false;
                }
                return EqualityComparer<ITrip>.Default.Equals(Trip, record.Trip) &&
                       FlatTrueZone == record.FlatTrueZone &&
                       ExpansionFactor == record.ExpansionFactor;
            }

            public override int GetHashCode()
            {
                var hashCode = 733537538;
                hashCode = hashCode * -1521134295 + EqualityComparer<ITrip>.Default.GetHashCode(Trip);
                hashCode = hashCode * -1521134295 + FlatTrueZone.GetHashCode();
                return hashCode;
            }
        }

        private List<Record> _records;

        private SparseArray<IZone> _zones;

        [SubModelInformation(Required = true, Description = "Access choice model to estimate")]
        public ICalculation<ITrip, Pair<IZone[], float[]>> PassengerAccessModel;

        [RunParameter("Min Negative Value", -10.0f, "The minimum negative value for a record's fitness allowed.")]
        public float MinNegativeValue;

        public void Start()
        {
            LoadDataIfNecessary();
            PassengerAccessModel.Load();
            float result = 0.0f;
            Parallel.For(0, _records.Count,
                () => 0f,
                (int i, ParallelLoopState _, float local) =>
                {
                    var record = _records[i];
                    var probabilities = PassengerAccessModel.ProduceResult(record.Trip);
                    // some trips will be outside of our allowed time periods
                    if (probabilities != null)
                    {
                        var indexOfTruth = Array.IndexOf(probabilities.First, _zones.GetFlatData()[record.FlatTrueZone]);
                        // some stations are considered invalid even if they are chosen by the TTS
                        if (indexOfTruth >= 0)
                        {
                            local += (float)(Math.Max(Math.Log(probabilities.Second[indexOfTruth]) / _records.Count, MinNegativeValue));
                        }
                    }
                    return local;
                },
                (float local) =>
                {
                    lock (this)
                    {
                        result += local;
                    }
                }
            );
            PassengerAccessModel.Unload();
            Root.RetrieveValue = () => result;
        }

        private void LoadDataIfNecessary()
        {
            if (!ZoneSystem.Loaded)
            {
                ZoneSystem.LoadData();
                _zones = ZoneSystem.ZoneArray;
            }
            Parallel.ForEach(NetworkData, (network) =>
            {
                if (!network.Loaded)
                {
                    network.LoadData();
                }
            });
            if (_records == null)
            {
                LoadRecords();
            }
        }

        private ITrip CreateTrip(IZone origin, IZone destination, Time time)
        {
            var trip = Scheduler.SchedulerHomeTrip.GetTrip(0);
            trip.OriginalZone = origin ?? throw new XTMFRuntimeException(this, "Origin was null");
            trip.DestinationZone = destination ?? throw new XTMFRuntimeException(this, "Destination was null");
            trip.TripStartTime = time;
            trip.Mode = null;
            return trip;
        }

        private void LoadRecords()
        {
            _records = new List<Record>();
            try
            {
                using (var reader = new CsvReader(TruthData, true))
                {
                    // burn the header
                    reader.LoadLine();
                    while (reader.LoadLine(out var columns))
                    {
                        // make sure there are enough columns
                        if (columns >= 5)
                        {
                            // Origin,Destination,StartTime,AccessStation,ExpansionFactor
                            reader.Get(out int origin, 0);
                            reader.Get(out int destination, 1);
                            reader.Get(out int _timeAsInt, 2);
                            reader.Get(out int choice, 3);
                            reader.Get(out float expFactor, 4);
                            _records.Add(new Record(CreateTrip(GetZone(origin), GetZone(destination), new Time(_timeAsInt / 100f)), GetFlatZoneIndex(choice), expFactor));
                        }
                    }
                }
            }
            catch (IOException e)
            {
                throw new XTMFRuntimeException(this, e);
            }
        }

        private int GetFlatZoneIndex(int zoneNumber)
        {
            var index = _zones.GetFlatIndex(zoneNumber);
            if (index < 0)
            {
                throw new XTMFRuntimeException(this, $"Unable to find a zone number {zoneNumber} within the zone system!");
            }
            return index;
        }

        private IZone GetZone(int zoneNumber)
        {
            var index = _zones.GetFlatIndex(zoneNumber);
            if (index < 0)
            {
                throw new XTMFRuntimeException(this, $"Unable to find a zone number {zoneNumber} within the zone system!");
            }
            return _zones.GetFlatData()[index];
        }
    }
}
