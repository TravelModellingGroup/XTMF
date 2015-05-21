using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using TMG.Input;
using Datastructure;
using TMG;
using TMG.DataUtility;
using Tasha.Common;
using XTMF;
using Tasha.XTMFModeChoice;



namespace Tasha.Validation.PerformanceMeasures
{
    public class AccessibilityCalculations : ISelfContainedModule
    {
        [RootModule]
        public ITashaRuntime Root;
        
        [RunParameter("Population Zones to Analyze", "1-1000", typeof(RangeSet), "The zones that you want to do the accessibility calculations for")]
        public RangeSet PopZoneRange;

        [RunParameter("Employment Zones to Analyze", "1-9999", typeof(RangeSet), "Which employment zones do you want to do accessibility calculations for")]
        public RangeSet EmpZoneRange;

        //[SubModelInformation(Required = true, Description = "File containing the NIA data")]
        //public IResource NIAData;

        [SubModelInformation(Required = true, Description = "File containing the employment data")]
        public IResource EmploymentData;

        [SubModelInformation(Required = true, Description = "The auto time matrix")]
        public IResource AutoTimeMatrix;

        [SubModelInformation(Required = true, Description = "The transit IVTT matrix")]
        public IResource TransitIVTTMatrix;

        [SubModelInformation(Required = true, Description = "The resource that will add all three transit time matrices")]
        public IResource TotalTransitTimeMatrix;

        [RunParameter("Accessibility Times to Analyze", "10", typeof(NumberList), "A comma separated list of accessibility times to execute this against.")]
        public NumberList AccessibilityTimes;

        [SubModelInformation(Required = true, Description = "Results file in .CSV format ")]
        public FileLocation ResultsFile;

        Dictionary<int, float> AutoAccessibilityResults = new Dictionary<int, float>();
        Dictionary<int, float> TransitIVTTAccessibilityResults = new Dictionary<int, float>();
        Dictionary<int, float> TransitAccessibilityResults = new Dictionary<int, float>();

        public void Start()
        {
            var zoneSystem = Root.ZoneSystem.ZoneArray;
            var zones = zoneSystem.GetFlatData();
            var popByZone = zones.Select(z => z.Population).ToArray();
            //var NIApop = NIAData.AquireResource<SparseArray<float>>().GetFlatData();
            var employmentByZone = EmploymentData.AquireResource<SparseArray<float>>().GetFlatData();            
            var AutoTimes = AutoTimeMatrix.AquireResource<SparseTwinIndex<float>>().GetFlatData();
            var TransitIVTT = TransitIVTTMatrix.AquireResource<SparseTwinIndex<float>>().GetFlatData();
            var TotalTransitTimes = TotalTransitTimeMatrix.AquireResource<SparseTwinIndex<float>>().GetFlatData();

            int[] analyzedZonePopulation = (from z in Root.ZoneSystem.ZoneArray.GetFlatData()                                           
                                           select z.Population).ToArray();

            float analizedpopulationSum = (from z in Root.ZoneSystem.ZoneArray.GetFlatData()
                                           where PopZoneRange.Contains(z.ZoneNumber)
                                            select z.Population).Sum();

            float employmentSum = (from z in Root.ZoneSystem.ZoneArray.GetFlatData()
                                   where EmpZoneRange.Contains(z.ZoneNumber)
                                   select employmentByZone[zoneSystem.GetFlatIndex(z.ZoneNumber)]).Sum();

            float accessiblePopulation;

            foreach (var accessTime in AccessibilityTimes)
            {
                for (int i = 0; i < analyzedZonePopulation.Length; i++)
                {
                    if (PopZoneRange.Contains(zones[i].ZoneNumber))
                    {
                        for (int j = 0; j < employmentByZone.Length; j++)
                        {
                            if (EmpZoneRange.Contains(zones[j].ZoneNumber))
                            {
                                if (AutoTimes[i][j] < accessTime)
                                {
                                    accessiblePopulation = (analyzedZonePopulation[i] * employmentByZone[j]);
                                    AddToResults(accessiblePopulation, accessTime, AutoAccessibilityResults);
                                }
                                if (TransitIVTT[i][j] < accessTime)
                                {
                                    accessiblePopulation = analyzedZonePopulation[i] * employmentByZone[j];
                                    AddToResults(accessiblePopulation, accessTime, TransitIVTTAccessibilityResults);
                                }
                                if (TotalTransitTimes[i][j] < accessTime)
                                {
                                    accessiblePopulation = analyzedZonePopulation[i] * employmentByZone[j];
                                    AddToResults(accessiblePopulation, accessTime, TransitAccessibilityResults);
                                }
                            }
                        }
                    }
                }
            }                        

            var denominator = 1.0f / (analizedpopulationSum * employmentSum);

            using(StreamWriter writer = new StreamWriter(ResultsFile))
            {
                writer.WriteLine("Auto Accessibility");
                writer.WriteLine("Time(mins), Percentage Accessible");
                foreach(var pair in AutoAccessibilityResults)
                 {
                    var percentageAccessible = AutoAccessibilityResults[pair.Key] * denominator;
                    writer.WriteLine("{0},{1}", pair.Key, percentageAccessible);
                }

                writer.WriteLine("Transit IVTT Accessibility");
                writer.WriteLine("Time(mins), Percentage Accessible");
                foreach (var pair in TransitIVTTAccessibilityResults)
                {
                    var percentageAccessible = TransitIVTTAccessibilityResults[pair.Key] * denominator;
                    writer.WriteLine("{0},{1}", pair.Key, percentageAccessible);
                }

                writer.WriteLine("Total Transit Time Accessibility");
                writer.WriteLine("Time(mins), Percentage Accessible");
                foreach (var pair in TransitAccessibilityResults)
                {
                    var percentageAccessible = TransitAccessibilityResults[pair.Key] * denominator;
                    writer.WriteLine("{0},{1}", pair.Key, percentageAccessible);
                }
            }
        }


        public void AddToResults(float population, int accessTime, Dictionary<int, float> results)
        {
            if(results.ContainsKey(accessTime))
            {
                results[accessTime] += population;
            }
            else 
            {
                results.Add(accessTime, population);
            }
        }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>(120, 25, 100); }
        }

        public bool RuntimeValidation(ref string error)
        {
            if (!EmploymentData.CheckResourceType<SparseArray<float>>())
            {
                error = "In '" + Name + "' the ODEmployment was not of type SparseArray<float>!";
                return false;
            }

            else if (!AutoTimeMatrix.CheckResourceType<SparseTwinIndex<float>>())
            {
                error = "In '" + Name + "' the AutoTimeMatrix was not of type SparseTwinIndex<float>!";
                return false;
            }

            else if (!TransitIVTTMatrix.CheckResourceType<SparseTwinIndex<float>>())
            {
                error = "In '" + Name + "' the AutoTimeMatrix was not of type SparseTwinIndex<float>!";
                return false;
            }

            return true;  
        }
    }
}
