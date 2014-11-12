using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XTMF;
using TMG;
using System.Diagnostics;
namespace James.UTDM
{
    public class TestTwinZoneSystemPerformance : IModelSystemTemplate
    {
        [RunParameter( "Input Directory", "../../Input", "The input directory for the model system template." )]
        public string InputBaseDirectory { get; set; }

        [SubModelInformation( Required = true, Description = "The zone system to test with." )]
        public IZoneSystem ZoneSystem;

        [RunParameter( "Times to run", 10, "The number of times to run the experiment" )]
        public int TimeToRun;

        public string OutputBaseDirectory
        {
            get;
            set;
        }

        public bool ExitRequest()
        {
            return false;
        }

        public void Start()
        {
            this.ZoneSystem.LoadData();
            for ( int i = 0; i < this.TimeToRun; i++ )
            {
                RunExperement();
            }
            this.ZoneSystem.UnloadData();
        }

        private void RunExperement()
        {
            var useMe = this.ZoneSystem.ZoneArray.CreateSquareTwinArray<int>();
            var zoneNumbers = this.ZoneSystem.ZoneArray.ValidIndexArray();
            Stopwatch watch = Stopwatch.StartNew();
            for ( int i = 0; i < zoneNumbers.Length; i++ )
            {
                for ( int j = 0; j < zoneNumbers.Length; j++ )
                {
                    var bob = useMe[zoneNumbers[i],zoneNumbers[j]];
                }
            }
            watch.Stop();
            Console.WriteLine( watch.ElapsedMilliseconds + "ms" );
        }

        public string Name { get; set; }

        public float Progress
        {
            get { return 0f; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>( 50, 150, 50 ); }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
