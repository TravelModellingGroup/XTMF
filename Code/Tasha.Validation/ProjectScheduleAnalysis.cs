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
using System.Windows.Forms.DataVisualization.Charting;
using Tasha.Common;
using Tasha.Scheduler;
using TMG.Input;
using XTMF;

namespace Tasha.Validation
{
    public class ProjectScheduleAnalysis : IPostScheduler
    {
        [RunParameter("Height", 600, "The height of the charts")]
        public int Height;

        [RunParameter("Bucket size", 30, "Minutes per bucket")]
        public int MinutesPerBucket;

        [SubModelInformation(Required = false, Description = "The location to save the chart.")]
        public FileLocation SchoolEndTimeChartFile;

        [SubModelInformation(Required = false, Description = "The location to save the chart.")]
        public FileLocation SchoolStartTimeChartFile;

        [RunParameter("Width", 800, "The width of the charts")]
        public int Width;

        [SubModelInformation(Required = false, Description = "The location to save the chart.")]
        public FileLocation WorkDurationChartFile;

        [SubModelInformation(Required = false, Description = "The location to save the chart.")]
        public FileLocation WorkEndTimeChartFile;

        [SubModelInformation(Required = false, Description = "The location to save the chart.")]
        public FileLocation WorkPersonsChartFile;

        [SubModelInformation(Required = false, Description = "The location to save the chart.")]
        public FileLocation WorkStartTimeChartFile;

        [SubModelInformation(Required = false, Description = "The location to save the data.")]
        public FileLocation WorkStartTimeDataFile;

        private float[] SchoolEndTime;
        private float[] SchoolStartTime;
        private float[] WorkDuration;
        private float[] WorkEndTime;
        private float[] WorkingPersons;
        private float[] WorkStartTime;

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
            get { return new Tuple<byte, byte, byte>(50, 100, 50); }
        }

        public void Execute(ITashaHousehold household)
        {
            lock (this)
            {
                foreach(var person in household.Persons)
                {
                    var expFactor = person.ExpansionFactor;
                    var data = person["SData"] as SchedulerPersonData;
                    if(data != null)
                    {
                        var workSched = data.WorkSchedule.Schedule;
                        var schoolSched = data.SchoolSchedule.Schedule;
                        GatherData(workSched, WorkStartTime, true, expFactor);
                        GatherData(workSched, WorkEndTime, false, expFactor);
                        GatherData(schoolSched, SchoolStartTime, true, expFactor);
                        GatherData(schoolSched, SchoolEndTime, false, expFactor);
                        CalculateWorkingPersons(workSched, expFactor);
                        //GatherDuration( workSched, this.WorkDuration, false );
                        if(workSched.EpisodeCount > 0)
                        {
                            Time duration = workSched.Episodes[workSched.EpisodeCount - 1].EndTime - workSched.Episodes[0].StartTime;
                            int index = GetBucketIndex(duration);
                            if(index >= 0 && index < WorkStartTime.Length)
                            {
                                WorkDuration[index] += expFactor;
                            }
                        }
                    }
                }
            }
        }

        public void IterationFinished(int iterationNumber)
        {
            if(WorkStartTimeChartFile != null) GenerateChart(WorkStartTimeChartFile, WorkStartTime, "Work Start Time", "#Episodes");
            if(WorkEndTimeChartFile != null) GenerateChart(WorkEndTimeChartFile, WorkEndTime, "Work End Time", "#Episodes");
            if(SchoolStartTimeChartFile != null) GenerateChart(SchoolStartTimeChartFile, SchoolStartTime, "School Start Time", "#Episodes");
            if(SchoolEndTimeChartFile != null) GenerateChart(SchoolEndTimeChartFile, SchoolEndTime, "School End Time", "#Episodes");
            if(WorkDurationChartFile != null) GenerateChart(WorkDurationChartFile, WorkDuration, "Work Duration", "#Episodes");
            if(WorkPersonsChartFile != null) GenerateChart(WorkPersonsChartFile, WorkingPersons, "Time of Day", "#Working People");

            if(WorkStartTimeDataFile != null)
            {
                SaveData(WorkStartTimeDataFile, WorkStartTime);
            }
        }

        private void SaveData(FileLocation file, float[] data)
        {
            using (StreamWriter writer= new StreamWriter(file))
            {
                writer.WriteLine("Bin,Data");
                for(int i = 0; i < data.Length; i++)
                {
                    writer.Write(i);
                    writer.Write(',');
                    writer.WriteLine(data[i]);
                }
            }
        }

        public void Load(int iteration)
        {
            var numberOfBuckets = (60 * 24) / MinutesPerBucket;
            WorkStartTime = new float[numberOfBuckets];
            WorkEndTime = new float[numberOfBuckets];
            WorkDuration = new float[numberOfBuckets];
            WorkingPersons = new float[numberOfBuckets];
            SchoolStartTime = new float[numberOfBuckets];
            SchoolEndTime = new float[numberOfBuckets];
        }

        public bool RuntimeValidation(ref string error)
        {
            if(MinutesPerBucket <= 0)
            {
                error = "The bucket size must be greater than zero";
                return false;
            }
            return true;
        }

        public void IterationStarting(int iterationNumber)
        {
            for(int i = 0; i < WorkStartTime.Length; i++)
            {
                WorkStartTime[i] = 0;
            }
        }

        private void CalculateWorkingPersons(Schedule sched, float expFactor)
        {
            var episodes = sched.Episodes;
            for(int i = 0; i < sched.EpisodeCount; i++)
            {
                if(episodes[i] != null)
                {
                    int start = GetBucketIndex(episodes[i].StartTime);
                    int end = GetBucketIndex(episodes[i].EndTime);
                    for(int j = start; j < end; j++)
                    {
                        if(j >= 0 && j < WorkStartTime.Length)
                        {
                            WorkingPersons[j] += expFactor;
                        }
                    }
                }
            }
        }

        private void GatherData(Schedule sched, float[] data, bool startTime, float expFactor)
        {
            var episodes = sched.Episodes;
            for(int i = 0; i < sched.EpisodeCount; i++)
            {
                if(episodes[i] != null)
                {
                    int index = GetBucketIndex(startTime ? episodes[i].StartTime : episodes[i].EndTime);
                    if(index >= 0 && index < WorkStartTime.Length)
                    {
                        data[index] += expFactor;
                    }
                }
            }
        }

        private void GenerateChart(string fileName, float[] values, string xAxisName, string yAxisName)
        {
            using (Chart chart = new Chart())
            {
                chart.Width = Width;
                chart.Height = Height;
                using (ChartArea area = new ChartArea("Start Times"))
                {
                    using (Series series = new Series())
                    {
                        using (series.Points)
                        {
                            series.ChartType = SeriesChartType.Column;
                            for(int i = 0; i < values.Length; i++)
                            {
                                series.Points.Add(new DataPoint(i, values[i]) { AxisLabel = (Time.FromMinutes((60 * 4) + i * MinutesPerBucket)).ToString() });
                            }
                            series.BorderWidth = 1;
                            series.BorderColor = System.Drawing.Color.Black;
                            area.AxisX.Title = xAxisName;// "Start Time";
                            area.AxisY.Title = yAxisName;// "#Episodes";
                            area.AxisX.Interval = 2;
                            chart.Series.Add(series);
                            chart.ChartAreas.Add(area);
                            area.Visible = true;
                            chart.SaveImage(fileName, ChartImageFormat.Png);
                        }
                    }
                }
            }
        }

        private int GetBucketIndex(Time time)
        {
            // find the time in minutes starting from 4 AM
            var minutes = (int)time.ToMinutes() - (60 * 4);
            return minutes / MinutesPerBucket;
        }
    }
}