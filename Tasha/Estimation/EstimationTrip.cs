using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Estimation
{
    public sealed class EstimationTrip : Attachable, ITrip
    {
        private Time _tripTime;
        public Time ActivityStartTime { get => _tripTime; set => _tripTime = value; }
        public Time TripStartTime { get => _tripTime; set => _tripTime = value; }

        public IZone DestinationZone { get; set; }

        public IZone IntermediateZone { get; set; }
        public ITashaMode Mode { get; set; }

        public ITashaMode[] ModesChosen { get; set; }

        public IZone OriginalZone { get; set; }

        public List<ITashaPerson> Passengers { get; set; }
        public Activity Purpose { get; set; }
        public ITashaPerson SharedModeDriver { get; set; }

        public Time TravelTime { get => Time.Zero; }

        public ITripChain TripChain { get; set; }

        public int TripNumber { get; set; }

        public ITrip Clone()
        {
            throw new NotImplementedException();
        }

        public void Recycle()
        {
            
        }
    }
}
