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
using System.Linq;
using System.Text;
using XTMF;
using TMG.Input;
using TMG.Estimation;
using Datastructure;
namespace James.UTDM
{
    public class LineEstimation : IModelSystemTemplate
    {
        [RunParameter("A", 0f, "Ax^3 + Bx^2 + Cx + D")]
        public float A;

        [RunParameter("B", 0f, "Ax^3 + Bx^2 + Cx + D")]
        public float B;

        [RunParameter("C", 0f, "Ax^3 + Bx^2 + Cx + D")]
        public float C;

        [RunParameter("D", 0f, "Ax^3 + Bx^2 + Cx + D")]
        public float D;

        [SubModelInformation(Required = true, Description = "The file that contains the data points.")]
        public FileLocation PointsFile;

        [RunParameter("Input Base Directory", "../../Input", "The directory to read the input from.")]
        public string InputBaseDirectory { get; set; }

        public string OutputBaseDirectory { get; set; }

        public bool ExitRequest()
        {
            return true;
        }

        class Point
        {
            internal float X;
            internal float Y;
        }

        private Point[] DataPoints;

        [RootModule]
        public IEstimationClientModelSystem Root;

        public void Start()
        {
            if ( this.DataPoints == null )
            {
                InitializeModelSystem();
            }
        }

        private void InitializeModelSystem()
        {
            var client = this.Root as IEstimationClientModelSystem;
            if ( client != null )
            {
                LoadData();
                IntegrateIntoClient( client );
            }
        }

        private void IntegrateIntoClient(IEstimationClientModelSystem client)
        {
            client.RetrieveValue = () =>
                {
                    var currentError = 0.0f;
                    for ( int i = 0; i < this.DataPoints.Length; i++ )
                    {
                        var x = this.DataPoints[i].X;
                        var diff = this.DataPoints[i].Y - ( this.A * ( x * x * x ) + this.B * ( x * x ) + this.C * x + this.D );
                        currentError += diff * diff;
                    }
                    return (float)Math.Sqrt( currentError );
                };
        }

        private void LoadData()
        {
            List<Point> points = new List<Point>();
            using (var reader = new CsvReader( this.PointsFile.GetFilePath() ))
            {
                // burn header
                reader.LoadLine();
                while ( !reader.EndOfFile )
                {
                    if ( reader.LoadLine() > 1 )
                    {
                        float x, y;
                        reader.Get( out x, 0 );
                        reader.Get( out y, 1 );
                        points.Add( new Point() { X = x, Y = y } );
                    }
                }
            }
            this.DataPoints = points.ToArray();
        }

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
