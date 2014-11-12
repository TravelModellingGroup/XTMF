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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms.DataVisualization.Charting;
using Tasha.Common;
using XTMF;

namespace Tasha.Validation.ValidateModeChoice
{
    public class ValidatePass : IPostHouseholdIteration
    {
        [RunParameter( "Chart Height", 768, "The height of the chart to make." )]
        public int CharHeight;

        [RunParameter( "Chart Width", 1024, "The width of the chart to make." )]
        public int CharWidth;

        [RunParameter( "Output File", "PassengerValidation.csv", "The file where we can store problems" )]
        public string OutputFile;

        [RunParameter( "Passenger Mode", "Passenger", "The name of the passenger mode, leave blank to not processes them specially." )]
        public string PassengerModeName;

        [RootModule]
        public ITashaRuntime Root;

        private ConcurrentDictionary<float, List<float>> Data = new ConcurrentDictionary<float, List<float>>();

        private int PassengerIndex;

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
            get;
            set;
        }

        public void HouseholdComplete(ITashaHousehold household, bool success)
        {
        }

        public void HouseholdIterationComplete(ITashaHousehold household, int hhldIteration, int totalHouseholdIterations)
        {
            for ( int i = 0; i < household.Persons.Length; i++ )
            {
                for ( int j = 0; j < household.Persons[i].TripChains.Count; j++ )
                {
                    if ( household.Persons[i].TripChains[j].JointTrip && !household.Persons[i].TripChains[j].JointTripRep )
                    {
                        continue;
                    }

                    for ( int k = 0; k < household.Persons[i].TripChains[j].Trips.Count; k++ )
                    {
                        var trip = household.Persons[i].TripChains[j].Trips[k];

                        if ( trip.Mode == this.Root.AllModes[PassengerIndex] )
                        {
                            using ( StreamWriter Writer = new StreamWriter( this.OutputFile, true ) )
                            {
                                var originalTrip = trip["Driver"] as ITrip;

                                var originalDistance = this.Root.ZoneSystem.Distances[originalTrip.OriginalZone.ZoneNumber, originalTrip.DestinationZone.ZoneNumber];
                                var passengerDistance = this.Root.ZoneSystem.Distances[trip.OriginalZone.ZoneNumber, trip.DestinationZone.ZoneNumber];

                                float firstLeg;
                                float secondLeg;

                                if ( originalTrip.OriginalZone == trip.OriginalZone )
                                {
                                    firstLeg = 0;
                                }
                                if ( originalTrip.DestinationZone == trip.DestinationZone )
                                {
                                    secondLeg = 0;
                                }

                                firstLeg = this.Root.ZoneSystem.Distances[originalTrip.OriginalZone.ZoneNumber, trip.OriginalZone.ZoneNumber];
                                secondLeg = this.Root.ZoneSystem.Distances[trip.DestinationZone.ZoneNumber, originalTrip.DestinationZone.ZoneNumber];

                                var newDistance = ( passengerDistance + firstLeg + secondLeg );

                                if ( Data.Keys.Contains( passengerDistance ) )
                                {
                                    Data[passengerDistance].Add( newDistance );
                                }
                                else
                                {
                                    Data.TryAdd( passengerDistance, new List<float>() );
                                    Data[passengerDistance].Add( newDistance );
                                }

                                //Writer.WriteLine( "{0}, {1}, {2}, {3}, {4}", household.HouseholdId, household.Persons[i].Id, originalTrip.TripChain.Person.Id, passengerDistance, newDistance );
                            }
                        }
                    }
                }
            }
        }

        public void HouseholdStart(ITashaHousehold household, int householdIterations)
        {
        }

        public bool RuntimeValidation(ref string error)
        {
            this.PassengerIndex = -1;
            if ( !String.IsNullOrWhiteSpace( this.PassengerModeName ) )
            {
                for ( int i = 0; i < this.Root.AllModes.Count; i++ )
                {
                    if ( this.Root.AllModes[i].ModeName == this.PassengerModeName )
                    {
                        this.PassengerIndex = i;
                        break;
                    }
                }
                if ( this.PassengerIndex <= 0 )
                {
                    error = "In '" + this.Name + "' we were unable to find any passenger mode with the name '" + this.PassengerModeName + "'.";
                    return false;
                }
            }
            return true;
        }

        private static void AddData(ConcurrentDictionary<float, List<float>> Data, Chart chart, Series secondSeries)
        {
            foreach ( var key in Data.Keys )
            {
                foreach ( var value in Data[key] )
                {
                    secondSeries.Points.Add( new DataPoint( key, value ) );
                }
            }
            chart.Series.Add( secondSeries );
        }

        private void GenerateChart(string fileName, ConcurrentDictionary<float, List<float>> values)
        {
            using ( Chart chart = new Chart() )
            {
                chart.Width = this.CharWidth;
                chart.Height = this.CharHeight;

                using ( ChartArea area = new ChartArea( "Passenger Distance vs New Driver Distance" ) )
                {
                    using ( Series firstSeries = new Series() )
                    {
                        AddData( values, chart, firstSeries );
                        firstSeries.ChartType = SeriesChartType.Point;
                        area.AxisX.Title = "Driver Distance Traveled (km)";// "Start Time";
                        area.AxisY.Title = "Passenger Distance Traveled (km)";// "#Episodes";
                        area.AxisX.Interval = 2;
                        area.Visible = true;
                        chart.ChartAreas.Add( area );
                        firstSeries.Name = "New Driver Distance";
                        firstSeries.Color = System.Drawing.Color.RoyalBlue;
                        firstSeries.BorderColor = System.Drawing.Color.Black;
                        firstSeries.BorderWidth = 1;
                        using ( Legend legend = new Legend() )
                        {
                            chart.Legends.Add( legend );
                            chart.SaveImage( fileName, ChartImageFormat.Png );
                        }
                    }
                }
            }
        }

        public void IterationStarting(int iteration, int totalIterations)
        {
            
        }

        public void IterationFinished(int iteration, int totalIterations)
        {
            
        }
    }
}