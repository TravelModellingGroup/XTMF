/*
    Copyright 2018 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tasha.Common;
using TMG;
using TMG.Input;
using XTMF;

namespace Tasha.Validation.NWS
{
    [ModuleInformation(Description = "This module produces the information to compare against the following file format." +
        " FROM,TO,ABB,HBM,HBO,NHB")]
    public class NWSTLFD : IPostHousehold
    {
        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter("Step Size", 2.0, "The number of KM to include within a bin.")]
        public float StepSize;

        [RunParameter("Number Of Bins" , 200, "The number of bins before storing the result to the remainder bin.")]
        public int NumberOfBins;

        public string Name { get; set; }

        public float Progress => 0f;

        public Tuple<byte, byte, byte> ProgressColour => new Tuple<byte, byte, byte>(50,150,50);

        [SubModelInformation(Required = true, Description = "The location to store the validation to.")]
        public FileLocation SaveTo;

        private sealed class Bin
        {
            public string Name { get; private set; }
            public float[] Bins { get; private set; }
            public float Intrazonal { get; private set; }
            public float BeyondMax { get; private set; }
            private readonly float _stride;
            public int NumberOfBins => Bins.Length;

            public Bin(string name, float stride, int numberOfBins)
            {
                Name = name;
                _stride = stride;
                Bins = new float[numberOfBins];
                Intrazonal = 0f;
                BeyondMax = 0f;
            }

            internal void Record(bool intraZonal, float distance, float expFactor)
            {
                if(intraZonal)
                {
                    Intrazonal += expFactor;
                }
                var index = (int)(distance / _stride);
                // if it is less than our max size
                if(index < Bins.Length)
                {
                    Bins[index] += expFactor;
                }
                else
                {
                    BeyondMax += expFactor;
                }
            }
        }

        private Bin _abb;
        private Bin _hbm;
        private Bin _hbo;
        private Bin _nhb;

        public void Load(int maxIterations)
        {
            
        }

        public void IterationStarting(int iteration)
        {
            _abb = new Bin("ABB", StepSize, NumberOfBins);
            _hbm = new Bin("HBM", StepSize, NumberOfBins);
            _hbo = new Bin("HBO", StepSize, NumberOfBins);
            _nhb = new Bin("NHB", StepSize, NumberOfBins);
        }

        private static float ComputeManhattanDistance(IZone origin, IZone destination)
        {
            var deltaX = Math.Abs(origin.X - destination.X);
            var deltaY = Math.Abs(origin.Y - destination.Y);
            // Convert it to KMs
            return (deltaX + deltaY)/1000f;
        }


        public void Execute(ITashaHousehold household, int iteration)
        {
            lock (this)
            {
                foreach (var person in household.Persons)
                {
                    var expFactor = person.ExpansionFactor;
                    foreach (var tripChain in person.TripChains)
                    {
                        var trips = tripChain.Trips;
                        for (int i = 0; i < trips.Count; i++)
                        {
                            Store(trips, i, expFactor);
                        }
                    }
                }
            }
        }

        private void Store(List<ITrip> trips, int i, float expFactor)
        {
            bool isHomeBased = (i == 0);
            var o = trips[i].OriginalZone;
            var d = trips[i].DestinationZone;
            float distance = ComputeManhattanDistance(o, d);
            bool intraZonal = (o == d);

            switch(trips[i].Purpose)
            {
                case Activity.IndividualOther:
                case Activity.JointOther:
                    (isHomeBased ? _hbo : _nhb).Record(intraZonal, distance, expFactor);
                    break;
                case Activity.JointMarket:
                case Activity.Market:
                    (isHomeBased ? _hbm : _nhb).Record(intraZonal, distance, expFactor);
                    break;
                case Activity.WorkBasedBusiness:
                    // We double count work based business trips if they are not home based
                    _abb.Record(intraZonal, distance, expFactor);
                    if (!isHomeBased)
                    {
                        _nhb.Record(intraZonal, distance, expFactor);
                    }
                    break;
            }
        }

        public void IterationFinished(int iteration)
        {
            using (var writer = new StreamWriter(SaveTo))
            {
                var bins = _abb.NumberOfBins;
                WriteHeader(writer);
                float from = 0f;
                // intrazonal
                writer.Write("intrazonal,0");
                writer.Write(',');
                writer.Write(_abb.Intrazonal);
                writer.Write(',');
                writer.Write(_hbm.Intrazonal);
                writer.Write(',');
                writer.Write(_hbo.Intrazonal);
                writer.Write(',');
                writer.WriteLine(_nhb.Intrazonal);
                // bins
                for (int i = 0; i < bins; i++)
                {
                    writer.Write(from);
                    writer.Write(',');
                    writer.Write(from + StepSize);
                    writer.Write(',');
                    writer.Write(_abb.Bins[i]);
                    writer.Write(',');
                    writer.Write(_hbm.Bins[i]);
                    writer.Write(',');
                    writer.Write(_hbo.Bins[i]);
                    writer.Write(',');
                    writer.WriteLine(_nhb.Bins[i]);
                    from += StepSize;
                }
                // over last bin
                writer.Write(from);
                writer.Write(",inf,");
                writer.Write(_abb.BeyondMax);
                writer.Write(',');
                writer.Write(_hbm.BeyondMax);
                writer.Write(',');
                writer.Write(_hbo.BeyondMax);
                writer.Write(',');
                writer.WriteLine(_nhb.BeyondMax);
            }
        }

        private void WriteHeader(StreamWriter writer)
        {
            writer.Write("from,to,");
            writer.Write(_abb.Name);
            writer.Write(',');
            writer.Write(_hbm.Name);
            writer.Write(',');
            writer.Write(_hbo.Name);
            writer.Write(',');
            writer.WriteLine(_nhb.Name);
        }

        public bool RuntimeValidation(ref string error)
        {
            if(StepSize <= 0)
            {
                error = "The step size must be greater than zero!";
                return false;
            }
            if(NumberOfBins <= 0)
            {
                error = "The number of bins must be greater than zero!";
                return false;
            }
            return true;
        }
    }
}
