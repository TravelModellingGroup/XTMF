using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XTMF;
using Tasha.Common;
namespace Tasha.Data
{
    public class UnloadResourceIPostIteration : IPostIteration
    {
        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [SubModelInformation(Required = true, Description = "The resource to unload")]
        public ResourceLookup Resource;

        public void Execute(int iterationNumber, int totalIterations)
        {
            Resource.ReleaseResource();
        }

        public void Load(IConfiguration config, int totalIterations)
        {
            
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
