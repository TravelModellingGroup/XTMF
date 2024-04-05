/*
    Copyright 2014-2024 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using System.Linq;
using System.Windows.Forms.DataVisualization.Charting;
using Tasha.Common;
using Tasha.Scheduler;
using XTMF;

namespace Tasha.Validation;

public class PersonScheduleAnalysis : IPostScheduler
{
    [RunParameter("Height", 600, "The height of the charts")]
    public int Height;

    [RunParameter("MarketPersonsFile", "MarketPersons.png", "The file name for the chart.")]
    public string MarketPersonsFile;

    [RunParameter("Bucket size", 30, "Minutes per bucket")]
    public int MinutesPerBucket;

    [RunParameter("OtherPersonsFile", "OtherPersons.png", "The file name for the chart.")]
    public string OtherPersonsFile;

    [RunParameter("Width", 800, "The width of the charts")]
    public int Width;

    [RunParameter("WorkPersonsFile", "WorkPersons.png", "The file name for the chart.")]
    public string WorkPersonsFile;

    private static Activity[] MarketActivities = new Activity[] { Activity.Market, Activity.JointMarket };
    private static Activity[] OtherActivities = new Activity[] { Activity.IndividualOther, Activity.JointOther };
    private static Activity[] WorkActivities = new Activity[] { Activity.PrimaryWork, Activity.SecondaryWork, Activity.WorkBasedBusiness, Activity.ReturnFromWork };
    private int[] MarketPersons;
    private int[] OtherPersons;
    private int[] WorkingPersons;

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
        foreach (var person in household.Persons)
        {
            if (person["SData"] is SchedulerPersonData data)
            {
                var sched = data.Schedule;
                CalculateWorkingPersons(sched, WorkingPersons, WorkActivities, true);
                CalculateWorkingPersons(sched, MarketPersons, MarketActivities, true);
                CalculateWorkingPersons(sched, OtherPersons, OtherActivities, true);
            }
        }
    }

    public void IterationFinished(int iterationNumber)
    {
        GenerateChart(WorkPersonsFile, WorkingPersons, "Time of Day", "#Working People");
        GenerateChart(MarketPersonsFile, MarketPersons, "Time of Day", "#Working People");
        GenerateChart(OtherPersonsFile, OtherPersons, "Time of Day", "#Working People");
    }

    public void Load(int iteration)
    {
        var numberOfBuckets = (60 * 24) / MinutesPerBucket;
        WorkingPersons = new int[numberOfBuckets];
        MarketPersons = new int[numberOfBuckets];
        OtherPersons = new int[numberOfBuckets];
    }

    public bool RuntimeValidation(ref string error)
    {
        if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            error = "This module requires at least Windows NT 6.1 to run.";
            return false;
        }
        if (MinutesPerBucket <= 0)
        {
            error = "The bucket size must be greater than zero";
            return false;
        }
        return true;
    }

    public void IterationStarting(int iterationNumber)
    {
        for (int i = 0; i < WorkingPersons.Length; i++)
        {
            WorkingPersons[i] = 0;
        }
    }

    private void CalculateWorkingPersons(Schedule sched, int[] data, Activity[] filter, bool include)
    {
        var episodes = sched.Episodes;
        for (int i = 0; i < sched.EpisodeCount; i++)
        {
            if (episodes[i] != null && (((!include) ^ filter.Contains(episodes[i].ActivityType))))
            {
                int start = GetBucketIndex(episodes[i].StartTime);
                int end = GetBucketIndex(episodes[i].EndTime);
                for (int j = start; j < end; j++)
                {
                    if (j >= 0 && j < data.Length)
                    {
                        System.Threading.Interlocked.Increment(ref data[j]);
                    }
                }
            }
        }
    }

    private void GenerateChart(string fileName, int[] values, string xAxisName, string yAxisName)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            return;
        }

        using Chart chart = new Chart()
        {
            Width = Width,
            Height = Height,
        };

        using ChartArea area = new ChartArea("Start Times");
        using Series series = new Series();
        series.ChartType = SeriesChartType.Column;
        for (int i = 0; i < values.Length; i++)
        {
            series.Points.Add(new DataPoint(i, values[i]) { AxisLabel = (Time.FromMinutes((60 * 4) + i * MinutesPerBucket)).ToString() });
        }
        series.BorderColor = System.Drawing.Color.Black;
        area.AxisX.Title = xAxisName;// "Start Time";
        area.AxisX.IntervalAutoMode = IntervalAutoMode.FixedCount;
        area.AxisX.Interval = 2;
        area.AxisX.IsStartedFromZero = false;
        area.AxisY.Title = yAxisName;// "#Episodes";
        chart.Series.Add(series);
        chart.ChartAreas.Add(area);
        area.Visible = true;
        chart.SaveImage(fileName, ChartImageFormat.Png);
    }

    private int GetBucketIndex(Time time)
    {
        // find the time in minutes starting from 4 AM
        var minutes = (int)time.ToMinutes() - (60 * 4);
        return minutes / MinutesPerBucket;
    }
}