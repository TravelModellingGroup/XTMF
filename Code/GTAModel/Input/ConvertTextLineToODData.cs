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
using TMG.Input;
using XTMF;

namespace TMG.GTAModel.Input
{
    [ModuleInformation(
        Description =
@"This module is designed to provide a conversion for any <b>IDataLineSource<float[]></b> module to be able to produce <b>IReadODData<float></b> data."
        )]
    public class ConvertTextLineToODData : IReadODData<float>
    {
        [RunParameter( "Destination Index", 1, "The 0 based index of the destination column." )]
        public int IndexOfD;

        [RunParameter( "Data Index", 2, "The 0 based index of the data column." )]
        public int IndexOfData;

        [RunParameter( "Origin Index", 0, "The 0 based index of the origin column." )]
        public int IndexOfO;

        [SubModelInformation( Description = "The module that will read in the raw line data.", Required = true )]
        public IDataLineSource<float[]> LineReader;

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public IEnumerable<ODData<float>> Read()
        {
            foreach ( var line in LineReader.Read() )
            {
                if ( ValidateLine( line ) )
                {
                    yield return new ODData<float> { O = (int)line[IndexOfO], D = (int)line[IndexOfD], Data = line[IndexOfData] };
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private bool ValidateLine(float[] line)
        {
            if ( line == null ) return false;
            var length = line.Length;
            return length > IndexOfO & length > IndexOfD & length > IndexOfData;
        }
    }
}