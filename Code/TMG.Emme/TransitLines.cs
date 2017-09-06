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
using System.Text;

namespace TMG.Emme
{
    public struct TransitLine
    {
        public float Bordings;
        public float EnergyConsumption;
        public float HoursTraveled;
        public string[] Id;
        public float KmTraveled;

        /// <summary>
        /// In KM
        /// </summary>
        public float Length;

        public float LoadAverage;
        public float LoadMax;
        public float MaxVolume;
        public float MinHdwy;
        public char Mode;
        public int NumberOfVehicals;
        public float OperationCosts;
        public float Time;
        public int VehicalType;
    }

    public class TransitLines
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="file621Name"></param>
        public TransitLines(string file621Name)
        {
            using ( StreamReader reader = new StreamReader( file621Name ) )
            {
                var transitLines = new List<TransitLine>( 1000 );
                char[] split = new char[] { ',', ' ', '\t' };
                string line;
                // Process each line
                while ( ( line = reader.ReadLine() ) != null )
                {
                    string[] parts = line.Split( split, StringSplitOptions.RemoveEmptyEntries );
                    if ( parts.Length == 15 )
                    {
                        var current = new TransitLine();
                        try
                        {
                            current.Id = new[] { parts[0] };
                            current.Mode = parts[1][0];
                            current.VehicalType = int.Parse( parts[2] );
                            current.NumberOfVehicals = int.Parse( parts[3] );
                            current.MinHdwy = float.Parse( parts[4] );
                            current.Length = float.Parse( parts[5] );
                            current.Time = float.Parse( parts[6] );
                            current.Bordings = float.Parse( parts[7] );
                            if ( !float.TryParse( parts[8], out current.KmTraveled ) )
                            {
                                current.KmTraveled = float.PositiveInfinity;
                            }
                            if ( float.TryParse( parts[9], out current.HoursTraveled ) )
                            {
                                current.HoursTraveled = float.PositiveInfinity;
                            }
                            current.LoadAverage = float.Parse( parts[10] );
                            current.LoadMax = float.Parse( parts[11] );
                            current.MaxVolume = float.Parse( parts[12] );
                            current.OperationCosts = float.Parse( parts[13] );
                            current.EnergyConsumption = float.Parse( parts[14] );
                            transitLines.Add( current );
                        }
                        catch
                        {
                            StringBuilder errorMessage = new StringBuilder();
                            errorMessage.Append( "We had problems loading the transit lines file \"" );
                            errorMessage.Append( file621Name );
                            errorMessage.AppendLine( "\"." );
                            if ( current.Id != null && current.Id.Length > 0 )
                            {
                                errorMessage.Append( "The problem occured while trying to read " );
                                errorMessage.Append( current.Id[0] );
                                errorMessage.AppendLine( "." );
                            }
                            throw new XTMF.XTMFRuntimeException(null, errorMessage.ToString() );
                        }
                    }
                }
                Lines = transitLines.ToArray();
            }
            GC.Collect();
        }

        public TransitLine[] Lines { get; private set; }
    }
}