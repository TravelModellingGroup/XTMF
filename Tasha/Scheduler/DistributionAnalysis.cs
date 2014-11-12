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
using System.IO;
using System.Linq;
using System.Windows.Forms.DataVisualization.Charting;
using Tasha.Common;
using Tasha.Internal;
using XTMF;

namespace Tasha.Scheduler
{
    public class DistributionAnalysis : IModelSystemTemplate, IDisposable
    {
        public static string FrequencyDistributionsFile;
        public static bool Gender;
        public static int MaxFrequency;
        public static int MinWorkingAge;
        public static int NumberOfAdultDistributions;
        public static int NumberOfAdultFrequencies;

        [RunParameter( "AdultDistributionFile", "AdultDistributions.zfc", "The file containing all of the adult distributions." )]
        public string AdultDistributionsFileLocal;

        [RunParameter( "Distributions", "Distributions.csv", "For Zoey" )]
        public string Distributions1;

        [RunParameter( "Frequency Distribution File", "FrequencyDistributions.zfc", "The location of the frequency distribution file." )]
        public string FrequencyDistributionsFileLocal;

        [RunParameter( "Is the person male?", true, "Is the person male (true or false)?" )]
        public bool GenderLocal;

        [RunParameter( "Max Frequency", 10, "The highest frequency number." )]
        public int MaxFrequencyLocal;

        [RunParameter( "MinWorkingAge", 11, "The youngest a person is allowed to work at." )]
        public int MinWorkingAgeLocal;

        [RunParameter( "NumberOfAdultDistributions", 6, "The total number of distributions for adults." )]
        public int NumberOfAdultDistributionsLocal;

        [RunParameter( "NumberOfAdultFrequencies", 9, "The total number of frequencies for adults." )]
        public int NumberOfAdultFrequenciesLocal;

        [RunParameter( "#OfDistributions", 262, "The number of distributions" )]
        public int NumberOfDistributionsLocal;

        [RunParameter( "Start Time Quantums", 96, "The number of different discreet time options" )]
        public int StartTimeQuantums;

        private StreamWriter Writer;

        [RunParameter( "Input Directory", "../../TashaInput", "The root input directory" )]
        public string InputBaseDirectory
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public string OutputBaseDirectory
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
            get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); }
        }

        public bool ExitRequest()
        {
            return false;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            if ( !File.Exists( Distributions1 ) )
            {
                Writer = new StreamWriter( Distributions1 );
            }

            this.SimulateScheduler();
            Distribution.InitializeDistributions();
            var distributionData = Distribution.Distributions.GetFlatData();
            var adultData = Distribution.AdultDistributions.GetFlatData();

            GraphTripPurpose( distributionData );
            this.Dispose( true );
        }

        public override string ToString()
        {
            return "Analysing Distribution Data";
        }

        private static void LoadDistributioNumbers(TashaPerson person, List<int> primaryWork, Occupation[] Occupations)
        {
            foreach ( Occupation Current in Occupations )
            {
                person.Occupation = Current;
                for ( person.Age = 11; person.Age < 100; person.Age++ )
                {
                    if ( person.Age >= 16 && person.Licence == false )
                    {
                        person.Licence = true;
                    }

                    if ( primaryWork.Contains( Distribution.GetDistributionID( person, Activity.PrimaryWork ) ) )
                    {
                        continue;
                    }

                    else
                    {
                        primaryWork.Add( Distribution.GetDistributionID( person, Activity.PrimaryWork ) );
                    }
                }
            }
        }

        private void GenerateChart(string fileName, float[] values, string xAxisName, string yAxisName)
        {
            using ( Chart chart = new Chart() )
            {
                chart.Width = 1024;
                chart.Height = 728;
                using ( ChartArea area = new ChartArea( "Start Times" ) )
                {
                    using ( Series series = new Series() )
                    {
                        series.ChartType = SeriesChartType.Bar;
                        for ( int i = 0; i < values.Length; i++ )
                        {
                            series.Points.Add( new DataPoint( i, values[i] ) { AxisLabel = ( Time.FromMinutes( ( 60 * 4 ) + i * ( 1440 / this.StartTimeQuantums ) ) ).ToString() } );
                        }
                        area.AxisX.Title = xAxisName;// "Start Time ";
                        area.AxisY.Title = yAxisName;// "#Episodes";
                        area.AxisX.Interval = 3;
                        chart.Series.Add( series );
                        chart.ChartAreas.Add( area );
                        area.Visible = true;
                        chart.SaveImage( fileName, ChartImageFormat.Png );
                    }
                }
            }
        }

        private int GetBucketIndex(Time time)
        {
            // find the time in minutes starting from 4 AM
            var minutes = (int)time.ToMinutes() - ( 60 * 4 );
            // 1440 is the number of minutes in 24 hours
            return minutes / ( 1440 / this.StartTimeQuantums );
        }

        private string GetFullPath(string localPath)
        {
            if ( !System.IO.Path.IsPathRooted( localPath ) )
            {
                return System.IO.Path.Combine( this.InputBaseDirectory, localPath );
            }
            return localPath;
        }

        private void GraphTripPurpose(Distribution.DistributionInformation[] distributionData)
        {
            TashaHousehold Household = new TashaHousehold();
            TashaPerson person = new TashaPerson();
            List<int> primaryWork = new List<int>();
            person.Licence = false;
            person.Male = GenderLocal;

            SchedulerHousehold.CreateHouseholdProjects( Household );
            SchedulerPerson.InitializePersonalProjects( person );
            SchedulerPerson.GenerateWorkSchoolSchedule( person, null );
            SchedulerTripChain.GetTripChain( person );
            var trip = SchedulerTrip.GetTrip(0);

            Occupation[] Occupations = { Occupation.Professional, Occupation.Manufacturing, Occupation.Retail, Occupation.Office, Occupation.Unknown, Occupation.NotEmployed };

            LoadDistributioNumbers( person, primaryWork, Occupations );

            float[] data = new float[this.StartTimeQuantums];
            foreach ( int ID in primaryWork )
            {
                var table = distributionData[ID].StartTimeFrequency;
                for ( int i = 0; i < this.StartTimeQuantums; i++ )
                {
                    for ( int j = 0; j < this.MaxFrequencyLocal; j++ )
                    {
                        data[i] += table[i][j];
                    }
                }
            }

            // Make all data in terms of percentages of total.

            float sum = data.Sum();
            for ( int number = 0; number < data.Length; number++ )
            {
                data[number] = data[number] / sum * 100;
                Writer.WriteLine( "{0}, {1}", ( Time.FromMinutes( ( 60 * 4 ) + number * ( 1440 / this.StartTimeQuantums ) ) ), data[number] );
            }

            GenerateChart( String.Format( "OfficeDur.png" ), data, "Time of Day", "Probability" );
        }

        private void SimulateScheduler()
        {
            Scheduler.MaxFrequency = this.MaxFrequencyLocal;
            Scheduler.NumberOfAdultDistributions = this.NumberOfAdultDistributionsLocal;
            Scheduler.NumberOfAdultFrequencies = this.NumberOfAdultFrequenciesLocal;
            Scheduler.NumberOfDistributions = this.NumberOfDistributionsLocal;
            Scheduler.StartTimeQuanta = this.StartTimeQuantums;
            Scheduler.FrequencyDistributionsFile = this.GetFullPath( this.FrequencyDistributionsFileLocal );
            Scheduler.AdultDistributionsFile = this.GetFullPath( this.AdultDistributionsFileLocal );
        }

        public void Dispose()
        {
            this.Dispose( true );
            GC.SuppressFinalize( this );
        }

        protected virtual void Dispose(bool all)
        {
            if ( this.Writer != null )
            {
                this.Writer.Dispose();
                this.Writer = null;
            }
        }
    }
}