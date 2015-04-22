using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tasha.Common;
using TMG;
using TMG.Input;
using XTMF;
using Datastructure;


namespace Tasha.Validation.SmartTrackPerformance
{
    public class TripLengthMeasure : IPostHousehold
    {
        public string Name
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public float Progress
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public void Execute(ITashaHousehold household, int iteration)
        {
            throw new NotImplementedException();
        }

        public void IterationFinished(int iteration)
        {
            throw new NotImplementedException();
        }

        public void IterationStarting(int iteration)
        {
            throw new NotImplementedException();
        }

        public void Load(int maxIterations)
        {
            throw new NotImplementedException();
        }

        public bool RuntimeValidation(ref string error)
        {
            throw new NotImplementedException();
        }
    }
}
