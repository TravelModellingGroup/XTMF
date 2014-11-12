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
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms.DataVisualization.Charting;
using Datastructure;
using XTMF;

namespace TMG.NetworkEstimation
{
    public class NetworkAnalysisReporter : IModelSystemTemplate
    {
        [RunParameter( "Limit Space", false, "Use the Best Space Radius to limit the space to be around the best point." )]
        public bool AlphaSpace;

        [RunParameter( "Value Col", 1, "The 0 indexed column number where the value is." )]
        public int ColourAxisCol;

        [RunParameter( "First Data Col", 2, "The 0 indexed column number where data starts." )]
        public int FirstDataCol;

        [RunParameter( "Chart Height", 600, "The height in pixels you would like your chart to be." )]
        public int Height;

        [RunParameter( "Input File", "../ParameterEvaluation.csv", "The parameter file to analyse" )]
        public string InputName;

        [RunParameter( "Last Data Col", 3, "The 0 indexed column number where data ends starts." )]
        public int LastDataCol;

        [RunParameter( "Chart Width", 800, "The width  in pixels you would like your chart to be." )]
        public int Width;

        private static Tuple<byte, byte, byte> ProgressColourT = new Tuple<byte, byte, byte>( 50, 150, 50 );

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
            get { return ProgressColourT; }
        }

        public bool ExitRequest()
        {
            throw new NotImplementedException();
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            var start = this.FirstDataCol;
            var end = this.LastDataCol;
            var halfWay = (int)Math.Ceiling( end / 2f );
            this.Progress = 0;
            var delta = ( end - start + 1 );
            var individualIncrease = 1f / ( ( ( delta * delta ) + delta ) / 2 );
            if ( this.AlphaSpace )
            {
                string[] headers = null;
                int bestIndex;
                var data = this.LoadData( ref headers, out bestIndex ).ToArray();
                try
                {
                    Parallel.For( 0, delta,
                    delegate(int i)
                    //for(int i = start; i <= end; i++)
                    {
                        System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.BelowNormal;
                        for ( int j = i; j < delta; j++ )
                        {
                            using ( Chart chart = new Chart() )
                            {
                                chart.Width = this.Width;
                                chart.Height = this.Height;
                                ChartArea ca;
                                chart.ChartAreas.Add( ca = new ChartArea() );
                                Series ourSeries = new Series();

                                ourSeries.ChartType = SeriesChartType.Point;
                                this.ProcessData( data, bestIndex, ca, ourSeries, i, j );
                                chart.Series.Add( ourSeries );
                                if ( headers != null )
                                {
                                    ca.AxisX.Title = headers[i];
                                    ca.AxisY.Title = headers[j];
                                    chart.SaveImage( String.Format( "{0}-{1}.png", headers[i], headers[j] ), ChartImageFormat.Png );
                                }
                                else
                                {
                                    chart.SaveImage( String.Format( "{0}-{1}.png", i + start, j + start ), ChartImageFormat.Png );
                                }
                            }
                            this.Progress += individualIncrease;
                        }
                    } );
                }
                catch ( AggregateException aggex )
                {
                    if ( aggex != null )
                    {
                        if ( aggex.InnerException != null )
                        {
                            throw new XTMFRuntimeException( aggex.InnerException.Message );
                        }
                        throw new XTMFRuntimeException( "Error trying to save the graph image file!" );
                    }
                }
            }
            else
            {
                try
                {
                    Parallel.For( start, end + 1,
                    delegate(int i)
                    {
                        System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.BelowNormal;
                        for ( int j = i; j <= end; j++ )
                        {
                            using ( Chart chart = new Chart() )
                            {
                                chart.Width = this.Width;
                                chart.Height = this.Height;
                                ChartArea ca;
                                chart.ChartAreas.Add( ca = new ChartArea() );
                                Series ourSeries = new Series();
                                ourSeries.ChartType = SeriesChartType.Point;
                                this.AddData( ca, ourSeries, i, j );
                                chart.Series.Add( ourSeries );
                                chart.SaveImage( String.Format( "{0}-{1}.png", i, j ), ChartImageFormat.Png );
                            }
                            this.Progress += individualIncrease;
                        }
                    } );
                }
                catch ( AggregateException aggex )
                {
                    if ( aggex != null )
                    {
                        if ( aggex.InnerException != null )
                        {
                            throw new XTMFRuntimeException( aggex.InnerException.Message );
                        }
                        throw new XTMFRuntimeException( "Error trying to save the graph image file!" );
                    }
                }
            }
            this.Progress = 1;
        }

        private void AddData(ChartArea area, Series ourSeries, int first, int second)
        {
            List<DataPoint> points = new List<DataPoint>();
            List<double> pointHeight = new List<double>();
            using ( StreamReader reader = new StreamReader( this.InputName ) )
            {
                int minNumberOfColumns = first + 1;
                string line = null;
                string[] parts = null;
                double minHeight = double.MaxValue;
                double maxHeight = double.MinValue;
                minNumberOfColumns = Math.Max( minNumberOfColumns, this.LastDataCol + 1 );
                minNumberOfColumns = Math.Max( minNumberOfColumns, this.ColourAxisCol + 1 );
                do
                {
                    try
                    {
                        while ( ( line = reader.ReadLine() ) != null && ( ( parts = line.Split( ',' ) ).Length >= minNumberOfColumns ) )
                        {
                            double height = double.Parse( parts[this.ColourAxisCol] );
                            DataPoint point = new DataPoint();
                            point.XValue = double.Parse( parts[first] );
                            point.YValues = new double[] { double.Parse( parts[second] ) };
                            if ( height > maxHeight ) maxHeight = height;
                            if ( height < minHeight ) minHeight = height;
                            points.Add( point );
                            pointHeight.Add( height );
                            parts = null;
                        }
                    }
                    catch
                    {
                        if ( parts != null )
                        {
                            area.AxisX.Title = parts[first];
                            area.AxisY.Title = parts[second];
                        }
                    }
                } while ( line != null );
            }
            // now process the colours
            Color red = Color.DarkBlue;
            Color blue = Color.LightGreen;
            var numberOfPoints = points.Count;
            int[] rank = new int[numberOfPoints];
            // pass 1, assign values
            for ( int i = 0; i < numberOfPoints; i++ )
            {
                rank[i] = i;
            }
            // pass 2 sort
            Array.Sort( rank, new Comparison<int>( delegate(int f, int s)
                {
                    var res = ( pointHeight[f] - pointHeight[s] );
                    if ( res < 0 ) return -1; if ( res > 0 ) return 1; return 0;
                } ) );

            for ( int i = 0; i < numberOfPoints; i++ )
            {
                if ( i == 0 )
                {
                    points[rank[i]].Label = "Best";
                    points[rank[i]].Color = Color.MediumPurple;
                }
                else if ( i == numberOfPoints - 1 )
                {
                    points[rank[i]].Label = "Worst";
                    points[rank[i]].Color = Color.IndianRed;
                }
                else
                {
                    points[rank[i]].Color = Lerp( blue, red, (double)i / ( numberOfPoints - 1 ) );
                }
                ourSeries.Points.Add( points[rank[i]] );
            }
        }

        private float CalculateCloseness(double[] XValues, double[] BestValues, int first, int second)
        {
            int dim = XValues.Length;
            double distance = 0;
            for ( int i = 0; i < dim; i++ )
            {
                if ( i != first && i != second )
                {
                    distance += Math.Abs( BestValues[i] - XValues[i] );
                }
            }
            return (float)distance;
        }

        private Color Lerp(Color lowColour, Color highColour, double distance)
        {
            return Color.FromArgb( Lerp( lowColour.R, highColour.R, distance ), Lerp( lowColour.G, highColour.G, distance ), Lerp( lowColour.B, highColour.B, distance ) );
        }

        private int Lerp(byte l, byte h, double distance)
        {
            return (byte)( ( h - l ) * distance + l );
        }

        private List<Pair<double[], double>> LoadData(ref string[] headers, out int bestIndex)
        {
            List<Pair<double[], double>> Data = new List<Pair<double[], double>>();
            bestIndex = -1;
            using ( StreamReader reader = new StreamReader( this.InputName ) )
            {
                int minNumberOfColumns = this.FirstDataCol + 1;
                string line = null;
                string[] parts = null;
                var dataColumns = this.LastDataCol - this.FirstDataCol + 1;
                double minHeight = double.MaxValue;
                minNumberOfColumns = Math.Max( minNumberOfColumns, this.LastDataCol + 1 );
                minNumberOfColumns = Math.Max( minNumberOfColumns, this.ColourAxisCol + 1 );
                do
                {
                    try
                    {
                        while ( ( line = reader.ReadLine() ) != null && ( ( parts = line.Split( ',' ) ).Length >= minNumberOfColumns ) )
                        {
                            double height = double.Parse( parts[this.ColourAxisCol] );
                            double[] data = new double[dataColumns];
                            for ( int i = 0; i < dataColumns; i++ )
                            {
                                data[i] = double.Parse( parts[i + this.FirstDataCol] );
                            }
                            if ( height < minHeight )
                            {
                                minHeight = height;
                                bestIndex = Data.Count;
                            }
                            Data.Add( new Pair<double[], double>( data, height ) );
                            parts = null;
                        }
                    }
                    catch
                    {
                        if ( parts != null )
                        {
                            if ( headers == null )
                            {
                                headers = new string[dataColumns];
                                for ( int i = 0; i < dataColumns; i++ )
                                {
                                    headers[i] = parts[i + this.FirstDataCol];
                                }
                            }
                        }
                    }
                } while ( line != null );
            }
            return Data;
        }

        private void ProcessData(Pair<double[], double>[] data, int bestIndex, ChartArea ca, Series ourSeries, int first, int second)
        {
            // now process the colours
            Color red = Color.DarkBlue;
            Color blue = Color.LightGreen;
            var numberOfPoints = data.Length;
            DataPoint[] points = new DataPoint[numberOfPoints];
            int[] goodnessOfFitRank = new int[numberOfPoints];
            int[] distanceToBestRank = new int[numberOfPoints];
            double[] distanceFromBest = new double[numberOfPoints];
            // pass 1, assign values
            for ( int i = 0; i < numberOfPoints; i++ )
            {
                points[i] = new DataPoint( data[i].First[first], data[i].First[second] );
                goodnessOfFitRank[i] = i;
                distanceToBestRank[i] = i;
                distanceFromBest[i] = this.CalculateCloseness( data[i].First, data[bestIndex].First, first, second );
            }
            // pass 2 sort
            Array.Sort( goodnessOfFitRank, new Comparison<int>( delegate(int f, int s)
            {
                var res = ( data[f].Second - data[s].Second );
                if ( res < 0 ) return -1; if ( res > 0 ) return 1; return 0;
            } ) );
            Array.Sort( distanceToBestRank, new Comparison<int>( delegate(int f, int s)
            {
                var res = ( distanceFromBest[f] - distanceFromBest[s] );
                if ( res < 0 ) return -1; if ( res > 0 ) return 1; return 0;
            } ) );
            for ( int i = 0; i < numberOfPoints; i++ )
            {
                if ( i == 0 )
                {
                    points[goodnessOfFitRank[i]].Label = "Best";
                    points[goodnessOfFitRank[i]].Color = Color.MediumPurple;
                }
                else if ( i == numberOfPoints - 1 )
                {
                    points[goodnessOfFitRank[i]].Label = "Worst";
                    points[goodnessOfFitRank[i]].Color = Color.IndianRed;
                }
                else
                {
                    points[goodnessOfFitRank[i]].Color = Lerp( blue, red, (double)i / ( numberOfPoints - 1 ) );
                }
                ourSeries.Points.Add( points[goodnessOfFitRank[i]] );
            }
            for ( int i = 0; i < numberOfPoints; i++ )
            {
                points[distanceToBestRank[i]].Color = Color.FromArgb( (byte)( 255 * ( 1 - ( (float)i / numberOfPoints ) ) ),
                    points[distanceToBestRank[i]].Color.R, points[distanceToBestRank[i]].Color.G, points[distanceToBestRank[i]].Color.B );
            }
        }
    }
}