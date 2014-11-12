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
using TMG.Modes;
using XTMF;

namespace TMG.GTAModel.Modes.UtilityComponents
{
    public class OriginNearStation : IUtilityComponent
    {
        [RunParameter( "Constant", 0f, "The utility to add if there is a station at the destination." )]
        public float Constant;

        [SubModelInformation( Description = "The source for the station information.", Required = true )]
        public IDataLineSource<float[]> DataSource;

        [RunParameter( "Station Index", 0, "The 0 indexed index for the station column." )]
        public int StationIndex;

        [RunParameter( "Zone Index", 0, "The 0 indexed index for the zone column." )]
        public int ZoneIndex;

        private Dictionary<int, bool> Data;

        private volatile bool Loaded = false;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        [RunParameter( "Component Name", "UniqueName", "The unique name for this utility component." )]
        public string UtilityComponentName
        {
            get;
            set;
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            if ( !this.Loaded )
            {
                lock ( this )
                {
                    Load();
                    this.Loaded = true;
                }
            }
            bool station;
            if ( this.Data.TryGetValue( origin.ZoneNumber, out station ) & station )
            {
                return this.Constant;
            }
            return 0f;
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        private void Load()
        {
            var data = new Dictionary<int, bool>();
            foreach ( var line in this.DataSource.Read() )
            {
                data[(int)line[this.ZoneIndex]] = ( line[this.StationIndex] > 0 );
            }
            this.Data = data;
        }
    }
}