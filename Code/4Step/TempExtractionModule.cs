using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XTMF;
using Tasha.Common;
using TMG.Emme;
using TMG.Input;
using TMG.Functions;
using System.IO;
using TMG;

namespace James.UTDM
{

    public class TempExtractionModule : IPostIteration, ISelfContainedModule
    {
        [SubModelInformation(Required = true, Description = "The directory to start from.")]
        public FileLocation RootDirectory;

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [RootModule]
        public ITravelDemandModel Root;

        public void Execute(int iterationNumber, int totalIterations)
        {
            var directories = Directory.GetDirectories(RootDirectory);
            var timePeriods = new string[] { "AM", "MD", "PM", "EV" };
            var zones = Root.ZoneSystem.ZoneArray.GetFlatData();
            foreach(var directory in directories)
            {
                var shortName = Path.GetFileName(directory);
                //create the local directory
                Directory.CreateDirectory(shortName);
                //for each time period
                for(int i = 0; i < timePeriods.Length; i++)
                {
                    // copy the raw travel times
                    CopyMatrix(zones, Path.Combine(directory, "RawTransitTimes", "tivtt-" + timePeriods[i] + ".mtx"), Path.Combine(shortName, timePeriods[i], "TransitInVehicletime.csv"));
                    // copy the regular walk and wait
                    CopyMatrix(zones, Path.Combine(directory, "LOS Matrices", timePeriods[i], "twait.mtx"), Path.Combine(shortName, timePeriods[i], "TransitWaitTime.csv"));
                    CopyMatrix(zones, Path.Combine(directory, "LOS Matrices", timePeriods[i], "twalk.mtx"), Path.Combine(shortName, timePeriods[i], "TransitWalkTime.csv"));

                    CopySummedMatrix(zones, new string[]
                    {
                        Path.Combine(directory, "RawTransitTimes", "tivtt-" + timePeriods[i] + ".mtx"),
                        Path.Combine(directory, "LOS Matrices", timePeriods[i], "twait.mtx"),
                        Path.Combine(directory, "LOS Matrices", timePeriods[i], "twalk.mtx")
                    }, Path.Combine(shortName, timePeriods[i], "CombinedTransitTime.csv"));
                }
            }
        }

        private static void CopyMatrix(IZone[] zones, string origin, string destination)
        {
            try
            {
                BinaryHelpers.ExecuteReader((reader =>
                {
                    var matrix = new EmmeMatrix(reader);
                    SaveData.SaveMatrix(zones, matrix.FloatData, destination);
                }), origin);
            }
            catch (IOException)
            {

            }
        }

        private static void CopySummedMatrix(IZone[] zones, string[] origins, string destination)
        {
            var array = new float[zones.Length * zones.Length];
            try
            {
                for(int i = 0; i < origins.Length; i++)
                {
                    BinaryHelpers.ExecuteReader((reader =>
                    {
                        var matrix = new EmmeMatrix(reader);
                        AddToArray(array, matrix.FloatData);
                    }), origins[i]);
                }
                SaveData.SaveMatrix(zones, array, destination);
            }
            catch (IOException)
            {

            }
        }

        private static void AddToArray(float[] array, float[] floatData)
        {
            for(int i = 0; i < array.Length; i++)
            {
                array[i] += floatData[i];
            }
        }

        public void Load(IConfiguration config, int totalIterations)
        {
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void Start()
        {
            Execute(0, 0);
        }
    }

}
