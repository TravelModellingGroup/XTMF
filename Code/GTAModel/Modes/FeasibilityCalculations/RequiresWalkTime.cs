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
using Datastructure;
using XTMF;

namespace TMG.GTAModel.Modes.FeasibilityCalculations
{
    public class RequiresWalkTime : ICalculation<Pair<IZone, IZone>, bool>
    {
        [RootModule]
        public ITravelDemandModel Root;

        [RunParameter( "Trip Time", "7:00AM", typeof( Time ), "The time to test to see if walk is available for." )]
        public Time TripTime;

        private ITripComponentData NetworkData;

        public string Name { get; set; }

        [RunParameter( "Network Name", "Transit", "The name of the network data to use." )]
        public string NetworkType { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public void Load()
        {
        }

        public bool ProduceResult(Pair<IZone, IZone> data)
        {
            return this.NetworkData.WalkTime( data.First, data.Second, this.TripTime ) > Time.Zero;
        }

        public bool RuntimeValidation(ref string error)
        {
            // Load in the network data
            return LoadNetworkData( ref error );
        }

        public void Unload()
        {
        }

        /// <summary>
        /// Find and Load in the network data
        /// </summary>
        private bool LoadNetworkData(ref string error)
        {
            foreach ( var dataSource in this.Root.NetworkData )
            {
                if ( dataSource.NetworkType == this.NetworkType )
                {
                    ITripComponentData advancedData = dataSource as ITripComponentData;
                    if ( advancedData != null )
                    {
                        this.NetworkData = advancedData;
                        return true;
                    }
                    else
                    {
                        error = "In '" + this.Name + "' the given network data '" + this.NetworkType + "' is not ITripComponentData compliant!";
                        return false;
                    }
                }
            }
            error = "In '" + this.Name + "' we were unable to find any network data with the name '" + this.NetworkType + "'!";
            return false;
        }
    }
}