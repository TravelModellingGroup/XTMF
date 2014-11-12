using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XTMF;
using TMG;
using TMG.Input;
using Tasha.Common;

namespace James.UTDM
{
    public class DemoSparseArray : IPostHousehold
    {
        [RootModule]
        public ITashaRuntime Root;

        [RunParameter( "Network Name", "Auto", "The name of the network to use." )]
        public string NetworkName;


        public string Name { get; set; }

        public float Progress
        {
            get { throw new NotImplementedException(); }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { throw new NotImplementedException(); }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Execute(ITashaHousehold household, int iteration)
        {
            var autoNetworkData = this.Root.NetworkData.FirstOrDefault( (network) => network.NetworkType == this.NetworkName );
            var zones = this.Root.ZoneSystem.ZoneArray.GetFlatData();
            if ( autoNetworkData == null )
            {
                throw new XTMFRuntimeException( "We could not find the '" + this.NetworkName + "' network!" );
            }
            foreach ( var person in household.Persons )
            {
                foreach ( var tripChain in person.TripChains )
                {
                    for ( int i = 0; i < tripChain.Trips.Count - 1; i++ )
                    {
                        var trip = tripChain.Trips[i];
                        var nextTrip = tripChain.Trips[i + 1];
                        if ( trip.Purpose == Activity.Market && nextTrip.Purpose == Activity.Market )
                        {
                            var originIndex = this.Root.ZoneSystem.ZoneArray.GetFlatIndex( trip.OriginalZone.ZoneNumber );
                            // run my logic here
                            for(int j = 0; j < zones.Length; j++)
                            {
                                var travelTime = autoNetworkData.TravelTime( originIndex, j, trip.TripStartTime );
                                
                            }
                        }
                    }

                }
            }
        }

        public void FinishIteration(int iteration)
        {

        }

        public void Load(int maxIterations)
        {

        }

        public void StartIteration(int iteration)
        {

        }
    }
}
