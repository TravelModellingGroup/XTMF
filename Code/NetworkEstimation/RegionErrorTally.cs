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
using Datastructure;
using TMG.Emme;
using XTMF;

namespace TMG.NetworkEstimation
{
    public class RegionErrorTally : IErrorTally
    {
        [RunParameter( "Absolute Error Weight", 0f, "The weight for absolute error." )]
        public float MABSWeight;

        [RunParameter( "Region Error File", "RegionError.csv", "The file name to save the region error into." )]
        public string RegionErrorFile;

        [RunParameter( "Region Percent Error", false, "Should we use error in terms of percent when doing the calculations?" )]
        public bool RegionPercentError;

        [RunParameter( "Root Mean Square Error Weight", 1f, "The weight for root mean square error." )]
        public float RMSEWeight;

        [RunParameter( "Total Error Weight", 0f, "The weight for total error." )]
        public float TERRORWeight;

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

        public float ComputeError(ParameterSetting[] parameters, TransitLine[] transitLine, TransitLine[] predicted)
        {
            List<Pair<char, char>> foundModes = new List<Pair<char, char>>();
            var ttsLines = transitLine.Length;
            var predictedLines = predicted.Length;
            var compairePair = new Pair<char, char>();
            for ( int i = 0; i < ttsLines; i++ )
            {
                if ( i > transitLine.Length )
                {
                    throw new XTMFRuntimeException( String.Format( "i = {0}, ttsLines = {1}, transitLine.Length = {2}",
                        i, ttsLines, transitLine.Length ) );
                }
                var mode = transitLine[i].Mode;
                if ( transitLine[i].ID == null )
                {
                    throw new XTMFRuntimeException( "There is a TTS line without an ID!" );
                }
                else if ( transitLine[i].ID.Length < 1 )
                {
                    throw new XTMFRuntimeException( "There is a TTS line without an ID!" );
                }
                else if ( transitLine[i].ID[0].Length < 1 )
                {
                    throw new XTMFRuntimeException( "There is an invalid ID for the TTS line #" + i + "!" );
                }
                var firstLetter = transitLine[i].ID[0][0];
                compairePair.First = mode;
                compairePair.Second = firstLetter;
                if ( !foundModes.Contains( compairePair ) )
                {
                    foundModes.Add( new Pair<char, char>( mode, firstLetter ) );
                }
            }
            var numberOfModesFirstLetters = foundModes.Count;
            float[] aggTTSToMode = new float[numberOfModesFirstLetters];
            float[] aggPredToMode = new float[numberOfModesFirstLetters];
            // first pass agg all of the tts numbers
            var testPair = new Pair<char, char>();
            for ( int i = 0; i < ttsLines; i++ )
            {
                testPair.First = transitLine[i].Mode;
                testPair.Second = transitLine[i].ID[0][0];
                var indexOfTransitLine = foundModes.IndexOf( testPair );
                if ( indexOfTransitLine == -1 )
                {
                    continue;
                }
                aggTTSToMode[indexOfTransitLine] += transitLine[i].Bordings;
            }
            // second pass agg all of the predicted numbers
            for ( int i = 0; i < predictedLines; i++ )
            {
                testPair.First = predicted[i].Mode;
                testPair.Second = predicted[i].ID[0][0];
                var indexOfPredictedMode = foundModes.IndexOf( testPair );
                if ( indexOfPredictedMode == -1 )
                {
                    continue;
                }
                aggPredToMode[indexOfPredictedMode] += predicted[i].Bordings;
            }
            double rmse = 0;
            double mabs = 0;
            double terror = 0;
            for ( int i = 0; i < numberOfModesFirstLetters; i++ )
            {
                float error = this.RegionPercentError ? (float)( Math.Abs( aggPredToMode[i] - aggTTSToMode[i] ) / aggTTSToMode[i] ) : aggTTSToMode[i] - aggPredToMode[i];
                rmse += error * error;
                mabs += Math.Abs( error );
                terror += error;
            }
            var finalError = (float)( ( rmse * this.RMSEWeight ) + ( mabs * this.MABSWeight ) + ( terror * this.TERRORWeight ) );
            if ( !String.IsNullOrWhiteSpace( this.RegionErrorFile ) )
            {
                bool exists = File.Exists( this.RegionErrorFile );
                using ( var writer = new StreamWriter( this.RegionErrorFile, true ) )
                {
                    if ( !exists )
                    {
                        for ( int i = 0; i < numberOfModesFirstLetters; i++ )
                        {
                            writer.Write( foundModes[i].Second );
                            writer.Write( ':' );
                            writer.Write( foundModes[i].First );
                            if ( i == numberOfModesFirstLetters - 1 )
                            {
                                if ( this.RegionPercentError )
                                {
                                    writer.WriteLine( ",%Error" );
                                }
                                else
                                {
                                    writer.WriteLine( ",Error" );
                                }
                            }
                            else
                            {
                                writer.Write( ',' );
                            }
                        }
                    }
                    for ( int i = 0; i < numberOfModesFirstLetters; i++ )
                    {
                        if ( this.RegionPercentError )
                        {
                            writer.Write( (float)( Math.Abs( aggPredToMode[i] - aggTTSToMode[i] ) / aggTTSToMode[i] ) );
                        }
                        else
                        {
                            writer.Write( aggPredToMode[i] - aggTTSToMode[i] );
                        }
                        writer.Write( ',' );
                    }
                    writer.WriteLine( finalError );
                }
            }
            return finalError;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}