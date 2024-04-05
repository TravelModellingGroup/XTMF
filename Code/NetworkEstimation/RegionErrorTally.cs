/*
    Copyright 2014-2017 Travel Modelling Group, Department of Civil Engineering, University of Toronto

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
using static System.String;

namespace TMG.NetworkEstimation
{
    public class RegionErrorTally : IErrorTally
    {
        [RunParameter( "Absolute Error Weight", 0f, "The weight for absolute error." )]
        public float MabsWeight;

        [RunParameter( "Region Error File", "RegionError.csv", "The file name to save the region error into." )]
        public string RegionErrorFile;

        [RunParameter( "Region Percent Error", false, "Should we use error in terms of percent when doing the calculations?" )]
        public bool RegionPercentError;

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

        public float ComputeError(ParameterSetting[] parameters, TransitLine[] transitLine, TransitLine[] predicted)
        {
            List<Pair<char, char>> foundModes = [];
            var ttsLines = transitLine.Length;
            var predictedLines = predicted.Length;
            for ( int i = 0; i < ttsLines; i++ )
            {
                if ( i > transitLine.Length )
                {
                    throw new XTMFRuntimeException(this,
                        $"i = {i}, ttsLines = {ttsLines}, transitLine.Length = {transitLine.Length}");
                }
                var mode = transitLine[i].Mode;
                if ( transitLine[i].Id == null )
                {
                    throw new XTMFRuntimeException(this, "There is a TTS line without an ID!" );
                }
                if ( transitLine[i].Id.Length < 1 )
                {
                    throw new XTMFRuntimeException(this, "There is a TTS line without an ID!" );
                }
                if ( transitLine[i].Id[0].Length < 1 )
                {
                    throw new XTMFRuntimeException(this, "There is an invalid ID for the TTS line #" + i + "!" );
                }
                var firstLetter = transitLine[i].Id[0][0];
                var compairePair = new Pair<char, char>(mode, firstLetter);
                if ( !foundModes.Contains( compairePair ) )
                {
                    foundModes.Add( new Pair<char, char>( mode, firstLetter ) );
                }
            }
            var numberOfModesFirstLetters = foundModes.Count;
            float[] aggTtsToMode = new float[numberOfModesFirstLetters];
            float[] aggPredToMode = new float[numberOfModesFirstLetters];
            // first pass agg all of the tts numbers
            for ( int i = 0; i < ttsLines; i++ )
            {
                var testPair = new Pair<char, char>(transitLine[i].Mode, transitLine[i].Id[0][0]);
                var indexOfTransitLine = foundModes.IndexOf( testPair );
                if ( indexOfTransitLine == -1 )
                {
                    continue;
                }
                aggTtsToMode[indexOfTransitLine] += transitLine[i].Bordings;
            }
            // second pass agg all of the predicted numbers
            for ( int i = 0; i < predictedLines; i++ )
            {
                var testPair = new Pair<char, char>(predicted[i].Mode, predicted[i].Id[0][0]);
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
                float error = RegionPercentError ? Math.Abs( aggPredToMode[i] - aggTtsToMode[i] ) / aggTtsToMode[i] : aggTtsToMode[i] - aggPredToMode[i];
                rmse += error * error;
                mabs += Math.Abs( error );
                terror += error;
            }
            var finalError = (float)( ( rmse * RmseWeight ) + ( mabs * MabsWeight ) + ( terror * TerrorWeight ) );
            if ( !IsNullOrWhiteSpace( RegionErrorFile ) )
            {
                bool exists = File.Exists( RegionErrorFile );
                using var writer = new StreamWriter(RegionErrorFile, true);
                if (!exists)
                {
                    for (int i = 0; i < numberOfModesFirstLetters; i++)
                    {
                        writer.Write(foundModes[i].Second);
                        writer.Write(':');
                        writer.Write(foundModes[i].First);
                        if (i == numberOfModesFirstLetters - 1)
                        {
                            if (RegionPercentError)
                            {
                                writer.WriteLine(",%Error");
                            }
                            else
                            {
                                writer.WriteLine(",Error");
                            }
                        }
                        else
                        {
                            writer.Write(',');
                        }
                    }
                }
                for (int i = 0; i < numberOfModesFirstLetters; i++)
                {
                    if (RegionPercentError)
                    {
                        writer.Write(Math.Abs(aggPredToMode[i] - aggTtsToMode[i]) / aggTtsToMode[i]);
                    }
                    else
                    {
                        writer.Write(aggPredToMode[i] - aggTtsToMode[i]);
                    }
                    writer.Write(',');
                }
                writer.WriteLine(finalError);
            }
            return finalError;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}