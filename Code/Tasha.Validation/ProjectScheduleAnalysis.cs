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
using System.Windows.Forms.DataVisualization.Charting;
using Tasha.Common;
using Tasha.Scheduler;
using XTMF;

namespace Tasha.Validation
{
    public class ProjectScheduleAnalysis : IPostScheduler
    {
        [RunParameter( "Height", 600, "The height of the charts" )]
        public int Height;

        [RunParameter( "Bucket size", 30, "Minutes per bucket" )]
        public int MinutesPerBucket;

        [RunParameter( "SchoolEndTimeFile", "SchoolEndTime.png", "The file name for the chart." )]
        public string SchoolEndTimeFile;

        [RunParameter( "SchoolStartTimeFile", "SchoolStartTime.png", "The file name for the chart." )]
        public string SchoolStartTimeFile;

        [RunParameter( "Width", 800, "The width of the charts" )]
        public int Width;

        [RunParameter( "WorkDurationFile", "WorkDuration.png", "The file name for the chart." )]
        public string WorkDurationFile;

        [RunParameter( "WorkEndTimeFile", "WorkEndTime.png", "The file name for the chart." )]
        public string WorkEndTimeFile;

        [RunParameter( "WorkPersonsFile", "WorkPersons.png", "The file name for the chart." )]
        public string WorkPersonsFile;

        [RunParameter( "WorkStartTimeFile", "WorkStartTimes.png", "The file name for the chart." )]
        public string WorkStartTimeFile;

        private int[] SchoolEndTime;
        private int[] SchoolStartTime;
        private int[] WorkDuration;
        private int[] WorkEndTime;
        private int[] WorkingPersons;
        private int[] WorkStartTime;

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
            get { return new Tuple<byte, byte, byte>( 50, 100, 50 ); }
        }

        public void Execute(ITashaHousehold household)
        {
            foreach ( var person in household.Persons )
            {
                var data = person["SData"] as SchedulerPersonData;
                if ( data != null )
                {
                    var workSched = data.WorkSchedule.Schedule;
                    var schoolSched = data.SchoolSchedule.Schedule;
                    GatherData( workSched, this.WorkStartTime, true );
                    GatherData( workSched, this.WorkEndTime, false );
                    GatherData( schoolSched, this.SchoolStartTime, true );
                    GatherData( schoolSched, this.SchoolEndTime, false );
                    CalculateWorkingPersons( workSched );
                    //GatherDuration( workSched, this.WorkDuration, false );
                    if ( workSched.EpisodeCount > 0 )
                    {
                        Time duration = workSched.Episodes[workSched.EpisodeCount - 1].EndTime - workSched.Episodes[0].StartTime;
                        int index = this.GetBucketIndex( duration );
                        if ( index >= 0 && index < this.WorkStartTime.Length )
                        {
                            System.Threading.Interlocked.Increment( ref this.WorkDuration[index] );
                        }
                    }
                }
            }
        }

        public void IterationFinished(int iterationNumber)
        {
            this.GenerateChart( this.WorkStartTimeFile, this.WorkStartTime, "Work Start Time", "#Episodes" );
            this.GenerateChart( this.WorkEndTimeFile, this.WorkEndTime, "Work End Time", "#Episodes" );
            this.GenerateChart( this.SchoolStartTimeFile, this.SchoolStartTime, "School Start Time", "#Episodes" );
            this.GenerateChart( this.SchoolEndTimeFile, this.SchoolEndTime, "School End Time", "#Episodes" );
            this.GenerateChart( this.WorkDurationFile, this.WorkDuration, "Work Duration", "#Episodes" );
            this.GenerateChart( this.WorkPersonsFile, this.WorkingPersons, "Time of Day", "#Working People" );
        }

        public void Load(int iteration)
        {
            var numberOfBuckets = ( 60 * 24 ) / this.MinutesPerBucket;
            this.WorkStartTime = new int[numberOfBuckets];
            this.WorkEndTime = new int[numberOfBuckets];
            this.WorkDuration = new int[numberOfBuckets];
            this.WorkingPersons = new int[numberOfBuckets];
            this.SchoolStartTime = new int[numberOfBuckets];
            this.SchoolEndTime = new int[numberOfBuckets];
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( this.MinutesPerBucket <= 0 )
            {
                error = "The bucket size must be greater than zero";
                return false;
            }
            return true;
        }

        public void IterationStarting(int iterationNumber)
        {
            for ( int i = 0; i < this.WorkStartTime.Length; i++ )
            {
                this.WorkStartTime[i] = 0;
            }
        }

        private void CalculateWorkingPersons(Schedule sched)
        {
            var episodes = sched.Episodes;
            for ( int i = 0; i < sched.EpisodeCount; i++ )
            {
                if ( episodes[i] != null )
                {
                    int start = this.GetBucketIndex( episodes[i].StartTime );
                    int end = this.GetBucketIndex( episodes[i].EndTime );
                    for ( int j = start; j < end; j++ )
                    {
                        if ( j >= 0 && j < this.WorkStartTime.Length )
                        {
                            System.Threading.Interlocked.Increment( ref this.WorkingPersons[j] );
                        }
                    }
                }
            }
        }

        private void GatherData(Schedule sched, int[] data, bool startTime)
        {
            var episodes = sched.Episodes;
            for ( int i = 0; i < sched.EpisodeCount; i++ )
            {
                if ( episodes[i] != null )
                {
                    int index = this.GetBucketIndex( startTime ? episodes[i].StartTime : episodes[i].EndTime );
                    if ( index >= 0 && index < this.WorkStartTime.Length )
                    {
                        System.Threading.Interlocked.Increment( ref data[index] );
                    }
                }
            }
        }

        private void GatherDuration(Schedule sched, int[] data, bool original)
        {
            var episodes = sched.Episodes;
            for ( int i = 0; i < sched.EpisodeCount; i++ )
            {
                if ( episodes[i] != null )
                {
                    int index = this.GetBucketIndex( original ? episodes[i].OriginalDuration : episodes[i].Duration );
                    if ( index >= 0 && index < this.WorkStartTime.Length )
                    {
                        System.Threading.Interlocked.Increment( ref data[index] );
                    }
                }
            }
        }

        private void GenerateChart(string fileName, int[] values, string xAxisName, string yAxisName)
        {
            using ( Chart chart = new Chart() )
            {
                chart.Width = Width;
                chart.Height = Height;
                using ( ChartArea area = new ChartArea( "Start Times" ) )
                {
                    using ( Series series = new Series() )
                    {
                        using ( series.Points )
                        {
                            series.ChartType = SeriesChartType.Column;
                            for ( int i = 0; i < values.Length; i++ )
                            {
                                series.Points.Add( new DataPoint( i, values[i] ) { AxisLabel = ( Time.FromMinutes( ( 60 * 4 ) + i * this.MinutesPerBucket ) ).ToString() } );
                            }
                            series.BorderWidth = 1;
                            series.BorderColor = System.Drawing.Color.Black;
                            area.AxisX.Title = xAxisName;// "Start Time";
                            area.AxisY.Title = yAxisName;// "#Episodes";
                            area.AxisX.Interval = 2;
                            chart.Series.Add( series );
                            chart.ChartAreas.Add( area );
                            area.Visible = true;
                            chart.SaveImage( fileName, ChartImageFormat.Png );
                        }
                    }
                }
            }
        }

        private int GetBucketIndex(Time time)
        {
            // find the time in minutes starting from 4 AM
            var minutes = (int)time.ToMinutes() - ( 60 * 4 );
            return minutes / this.MinutesPerBucket;
        }
    }
}