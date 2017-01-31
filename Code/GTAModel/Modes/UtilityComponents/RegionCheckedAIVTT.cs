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
using XTMF;

namespace TMG.GTAModel.Modes.UtilityComponents
{
    public sealed class RegionCheckedAIVTT : RegionUtilityComponent
    {
        [RunParameter( "aivtt", 0f, "The factor to apply against the travel time between the zones" )]
        public float Aivtt;

        private INetworkData NetworkData;

        [RunParameter( "Network Name", "Auto", "The name of the network data to use." )]
        public string NetworkType { get; set; }

        override public float CalculateV(IZone origin, IZone destination, Time time)
        {
            if ( IsContained( origin, destination ) )
            {
                return Aivtt * NetworkData.TravelTime( origin, destination, time ).ToMinutes();
            }
            return 0;
        }

        override protected bool SubRuntimeValidation(ref string error)
        {
            // Load in the network data
            LoadNetworkData();
            if ( NetworkData == null )
            {
                error = "In '" + Name + "' we were unable to find any network data called '" + NetworkType + "'!";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Find and Load in the network data
        /// </summary>
        private void LoadNetworkData()
        {
            foreach ( var dataSource in Root.NetworkData )
            {
                if ( dataSource.NetworkType == NetworkType )
                {
                    NetworkData = dataSource;
                    return;
                }
            }
        }
    }
}