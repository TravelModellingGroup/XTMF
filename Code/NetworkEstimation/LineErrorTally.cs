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
using System.Linq;
using TMG.Emme;
using XTMF;

namespace TMG.NetworkEstimation
{
    public class LineErrorTally : IErrorTally
    {
        [RunParameter( "Absolute Error Weight", 0f, "The weight for absolute error." )]
        public float MabsWeight;

        [RunParameter( "Percent Error", false, "Use the percent of error instead of boardings." )]
        public bool PercentError;

        [RunParameter( "Root Mean Square Error Weight", 1f, "The weight for root mean square error." )]
        public float RmseWeight;

        [RunParameter( "Total Error Weight", 0f, "The weight for total error." )]
        public float TerrorWeight;

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
            get { return null; }
        }

        public float ComputeError(ParameterSetting[] parameters, TransitLine[] truth, TransitLine[] predicted)
        {
            var numberOfLines = predicted.Length;
            float rmse = 0;
            float mabs = 0;
            float terror = 0;
            float[] aggToTruth = new float[truth.Length];
            for ( int i = 0; i < numberOfLines; i++ )
            {
                for ( int j = 0; j < truth.Length; j++ )
                {
                    bool found = false;
                    foreach ( var line in predicted[i].Id )
                    {
                        if ( truth[j].Id.Contains( line ) )
                        {
                            found = true;
                            break;
                        }
                    }
                    if ( found )
                    {
                        aggToTruth[j] += predicted[i].Bordings;
                        break;
                    }
                }
            }
            for ( int i = 0; i < truth.Length; i++ )
            {
                float error = PercentError ? Math.Abs( aggToTruth[i] - truth[i].Bordings ) / truth[i].Bordings : aggToTruth[i] - truth[i].Bordings;
                rmse += error * error;
                mabs += Math.Abs( error );
                terror += error;
            }
            return ( rmse * RmseWeight ) + ( mabs * MabsWeight ) + ( terror * TerrorWeight );
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}