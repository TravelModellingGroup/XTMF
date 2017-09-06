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
    public sealed class RegionCheckedTripComponentData : RegionUtilityComponent
    {
        [RunParameter( "boarding", 0f, "The factor to apply against the boarding factor between the zones" )]
        public float Boarding;

        [RunParameter( "cost", 0f, "The factor to apply against the cost of travelling between the zones" )]
        public float Cost;

        [RunParameter( "ivtt", 0f, "The factor to apply against the in vehicle travel time between the zones" )]
        public float IVTT;

        [RunParameter( "wait time", 0f, "The factor to apply against the waiting time between the zones" )]
        public float Wait;

        [RunParameter( "walk time", 0f, "The factor to apply against the walk time between the zones" )]
        public float Walk;

        private ITripComponentData NetworkData;

        [RunParameter( "Network Name", "Auto", "The name of the network data to use." )]
        public string NetworkType { get; set; }

        public override float CalculateV(IZone origin, IZone destination, Time time)
        {
            if (IsContained(origin, destination))
            {
                if (NetworkData.GetAllData(origin, destination, time, out Time ivtt, out Time walkTime, out Time waitTime, out Time boarding, out float cost))
                {
                    return IVTT * ivtt.ToMinutes() + Wait * waitTime.ToMinutes()
                        + Walk * walkTime.ToMinutes() + Boarding * boarding.ToMinutes() + Cost * cost;
                }
            }
            return 0f;
        }

        protected override bool SubRuntimeValidation(ref string error)
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
                if (dataSource is ITripComponentData ds && dataSource.NetworkType == NetworkType)
                {
                    NetworkData = ds;
                    return;
                }
            }
        }
    }
}