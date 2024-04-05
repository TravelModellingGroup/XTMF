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
using Datastructure;
using XTMF;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Tasha.Validation;

[ModuleInformation(
    Description = "This module is used to compare TASHA start-times to the real data." +
                    "As an input, the module takes in both files and asks which two variables should be " +
                    "be used as comparison. When the module concludes, it outputs a bar chart which " +
                    "compares the real and TASHA data and allows for visual inspection."
    )]
public class ComparisonOfFiles : IModelSystemTemplate
{
    [RunParameter( "Chart Height", 768, "The height of the chart to make." )]
    public int CharHeight;

    [RunParameter( "Chart Width", 1024, "The width of the chart to make." )]
    public int CharWidth;

    [RunParameter( "First Column", 0, "The first column to compare (zero based index)." )]
    public int FirstColumn;

    [RunParameter( "First Series Name", "Base Data", "First series name" )]
    public string FirstSeriesName;

    [RunParameter( "Second Column", 1, "The second column to compare (zero based index)." )]
    public int SecondColumn;

    [RunParameter( "Second Series Name", "Run Data", "Second series name" )]
    public string SecondSeriesName;

    [RunParameter( "First file to Compare", "PrimaryWorkStartTimes.csv", "The first of two files that you want to compare" )]
    public string FirstFile { get; set; }

    [RunParameter( "First File Header", true, "Does the first file have a header?" )]
    public bool FirstHeader { get; set; }

    [RunParameter( "Input base Directory", "../../Compare", "The input Directory" )]
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

    [RunParameter( "Output File Name", "StartTimeComparison.png", "The name of the output file" )]
    public string OutputFile { get; set; }

    public float Progress
    {
        get;
        set;
    }

    public Tuple<byte, byte, byte> ProgressColour
    {
        get { return new Tuple<byte, byte, byte>( 32, 76, 169 ); }
    }

    [RunParameter( "Second file to Compare", "PrimaryWorkStartTimesTasha.csv", "The second of two files that you want to compare" )]
    public string SecondFile { get; set; }

    [RunParameter( "Second File Header", true, "Does the second file have a header?" )]
    public bool SecondHeader { get; set; }

    public bool ExitRequest()
    {
        return false;
    }

    public bool RuntimeValidation(ref string error)
    {
        if ( CharWidth <= 0 )
        {
            error = "The chart's width must be greater than 0!";
            return false;
        }
        if ( CharHeight <= 0 )
        {
            error = "The chart's height must be greater than 0!";
            return false;
        }
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            error = "This module requires Windows NT 6.1 or above!";
            return false;
        }
        return true;
    }

    public void Start()
    {
        float[] first = ReadFile( FirstFile );
        float[] second = ReadFile( SecondFile );

        GenerateChart( OutputFile, first, second, "Start Time", "Percent of 24hr Frequency" );
    }

    private static void AddData(float[] data, Chart chart, Series secondSeries)
    {
        for ( int i = 0; i < data.Length; i++ )
        {
            secondSeries.Points.Add( new DataPoint( i, data[i] ) { AxisLabel = i.ToString() } );
        }
        chart.Series.Add( secondSeries );
    }

    private void GenerateChart(string fileName, float[] values, float[] otherValues, string xAxisName, string yAxisName)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            return;
        }
        using Chart chart = new();
        chart.Width = CharWidth;
        chart.Height = CharHeight;

        using ChartArea area = new("Start Times");
        using Series firstSeries = new();
        using Series secondSeries = new();
        AddData(values, chart, firstSeries);
        AddData(otherValues, chart, secondSeries);
        firstSeries.ChartType = secondSeries.ChartType = SeriesChartType.Column;
        area.AxisX.Title = xAxisName;// "Start Time";
        area.AxisY.Title = yAxisName;// "#Episodes";
        area.AxisX.Interval = 2;
        area.Visible = true;
        chart.ChartAreas.Add(area);
        firstSeries.Name = FirstSeriesName;
        secondSeries.Name = SecondSeriesName;
        firstSeries.Color = System.Drawing.Color.RoyalBlue;
        firstSeries.BorderColor = System.Drawing.Color.Black;
        firstSeries.BorderWidth = 1;
        secondSeries.Color = System.Drawing.Color.Red;
        secondSeries.BorderColor = System.Drawing.Color.Black;
        secondSeries.BorderWidth = 1;
        using Legend legend = new();
        chart.Legends.Add(legend);
        chart.SaveImage(fileName, ChartImageFormat.Png);
    }

    private float[] ReadFile(string fileName)
    {
        float[] ret = new float[29];
        using ( CsvReader reader = new( System.IO.Path.Combine( InputBaseDirectory, fileName ) ) )
        {
            if ( FirstHeader )
            {
                reader.LoadLine();
            }

            while ( !reader.EndOfFile )
            {
                if ( reader.LoadLine() < 2 )
                {
                    continue;
                }
                reader.Get( out int currentTime, FirstColumn );
                if ( currentTime < 29 && currentTime >= 0 )
                {
                    reader.Get( out float currentPercent, SecondColumn );
                    ret[currentTime] += currentPercent;
                }
            }
        }
        return ret;
    }
}