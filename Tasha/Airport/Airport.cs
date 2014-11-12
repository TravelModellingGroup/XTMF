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
using XTMF;

namespace Tasha.Airport
{
    public class Airport : IModule
    {
        [RunParameter( "Base", 0f, "The number of boardings in the base year." )]
        public float Base;

        [RunParameter( "Base Time Period Trips", 0f, "The number of [Time-Period] boardings in the base year. (AM/PM/OP)\r\nOnly used for primary airports." )]
        public float BaseTimePeriod;

        [RunParameter( "Future Prediction", 0f, "The number of boardings expected in this year. (0 means ignore)" )]
        public float FuturePrediction;

        [RunParameter( "Zone Number", 0, "The zone number that this airport belongs to. (0 means ignore)" )]
        public int ZoneNumber;

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}